using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HireYar.Application.Notifications;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireYar.Tests;

public sealed partial class InterviewFlowTests
{
    private async Task<WebSocket> NotificationSocket(string token)
    {
        var client = f.Server.CreateWebSocketClient();
        var socket = await client.ConnectAsync(new Uri("ws://localhost/hubs/notifications?access_token=" + token), CancellationToken.None);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[4096];
        var read = await socket.ReceiveAsync(buffer.AsMemory(), timeout.Token);
        Assert.Contains("{}", Encoding.UTF8.GetString(buffer, 0, read.Count));
        return socket;
    }
    private static async Task NotificationChanged(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer.AsMemory(), ct);
            var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
            if (text.Contains("NotificationsChanged", StringComparison.Ordinal)) return;
        }
    }
    private static async Task<NotificationListDto> Notifications(Account a, string query = "") =>
        (await (await a.Client.Send(HttpMethod.Get, "/api/notifications" + query)).Content.ReadFromJsonAsync<NotificationListDto>(TestJson.Options))!;

    private async Task<Account> NotificationMember(Account owner)
    {
        var mobile = "09" + Random.Shared.NextInt64(100_000_000, 1_000_000_000).ToString("D9");
        var sent = await owner.Client.Send(HttpMethod.Post, "/api/team/invitations",
            new HireYar.Application.Organizations.InviteTeamMemberRequest(mobile, "نام", "آزمایشی"));
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        var invitation = (await sent.Content.ReadFromJsonAsync<HireYar.Application.Organizations.TeamInvitationDto>(TestJson.Options))!;
        var browser = f.Browser();
        Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Post, "/api/auth/otp/request",
            new HireYar.Application.Auth.RequestOtpRequest(mobile))).StatusCode);
        var accepted = await browser.Send(HttpMethod.Post, $"/api/auth/team-invitations/{invitation.Id}/accept",
            new HireYar.Application.Auth.VerifyOtpRequest(mobile, f.PhoneOtp.Codes[mobile]));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        browser.AccessToken = (await accepted.Content.ReadFromJsonAsync<HireYar.Application.Auth.AccessResponse>())!.AccessToken;
        Guid id = default;
        await f.Db(async db => id = (await db.Users.SingleAsync(u => u.PhoneNumber == mobile)).Id);
        return new(browser, mobile, id);
    }

    [PostgresFact]
    public async Task Notifications_reach_team_members_with_independent_reads_and_live_updates()
    {
        var (owner, invite) = await Invite();
        var member = await NotificationMember(owner);
        var second = await NotificationMember(owner);
        var other = await f.RegisterOwner();
        using var ownerSocket = await NotificationSocket(owner.Client.AccessToken!);
        using var memberSocket = await NotificationSocket(member.Client.AccessToken!);
        using var candidate = Candidate(invite);
        await Start(candidate, invite.InterviewId);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await NotificationChanged(ownerSocket, timeout.Token);
        await NotificationChanged(memberSocket, timeout.Token);
        var own = Assert.Single((await Notifications(owner)).Items);
        var mine = Assert.Single((await Notifications(member)).Items);
        Assert.Equal(own.Title, mine.Title); Assert.NotEqual(own.Id, mine.Id);
        Assert.Single((await Notifications(second)).Items);
        Assert.Empty((await Notifications(other)).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await member.Client.Send(HttpMethod.Put,
            $"/api/notifications/{own.Id}/read", new NotificationReadRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Put,
            $"/api/notifications/{mine.Id}/read", new NotificationReadRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await member.Client.Send(HttpMethod.Put,
            $"/api/notifications/{mine.Id}/read", new NotificationReadRequest(true))).StatusCode);
        await NotificationChanged(memberSocket, timeout.Token);
        Assert.Equal(0, (await Notifications(member)).UnreadCount);
        Assert.Equal(1, (await Notifications(owner)).UnreadCount);
        Assert.Equal(1, (await Notifications(second)).UnreadCount);
        Assert.Equal(0, (await (await member.Client.Send(HttpMethod.Get, "/api/notifications/unread-count"))
            .Content.ReadFromJsonAsync<NotificationCountDto>())!.UnreadCount);
        await Start(candidate, invite.InterviewId);
        Assert.Single((await Notifications(member)).Items);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null)).StatusCode);
        await NotificationChanged(memberSocket, timeout.Token);
        Assert.Single((await Notifications(member)).Items, n => n.Type == "InterviewCompleted" && n.Target == "InterviewReport");
        Assert.Equal(2, (await Notifications(second)).Items.Count);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Post, $"/api/team/members/{member.Id}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.Client.Send(HttpMethod.Get, "/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.Client.Send(HttpMethod.Post, "/hubs/notifications/negotiate?negotiateVersion=1")).StatusCode);
    }

    [PostgresFact]
    public async Task Reminders_reach_active_members_only_and_deduplicate_per_recipient()
    {
        var owner = await f.RegisterOwner(explicitSignup: true, provisionMembership: false);
        var member = await NotificationMember(owner);
        var inactive = await NotificationMember(owner);
        var unconfirmed = await NotificationMember(owner);
        Guid org = default;
        await f.Db(async db =>
        {
            org = (await db.Users.SingleAsync(u => u.Id == owner.Id)).OrganizationId;
            (await db.Users.SingleAsync(u => u.Id == inactive.Id)).IsActive = false;
            (await db.Users.SingleAsync(u => u.Id == unconfirmed.Id)).PhoneNumberConfirmed = false;
            await db.SaveChangesAsync();
        });
        var (hr, invite) = await Invite(owner);
        using var candidate = Candidate(invite);
        await Start(candidate, invite.InterviewId);
        using var memberSocket = await NotificationSocket(member.Client.AccessToken!);
        await Remind(org); await Remind(org);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await NotificationChanged(memberSocket, timeout.Token);
        foreach (var account in new[] { hr, member })
        {
            var items = (await Notifications(account)).Items;
            Assert.Single(items, n => n.Type == "InterviewStarted");
            Assert.Single(items, n => n.Type == "SubscriptionExpiring");
            Assert.Single(items, n => n.Type == "InterviewCreditsLow");
        }
        await f.Db(async db => Assert.False(await db.Notifications.AnyAsync(n =>
            n.RecipientUserId == inactive.Id || n.RecipientUserId == unconfirmed.Id)));
    }

    [PostgresFact]
    public async Task Notifications_push_over_websocket_and_rest_is_scoped_with_read_filters()
    {
        var (hr, invite) = await Invite(); var other = await f.RegisterOwner();
        using var ownSocket = await NotificationSocket(hr.Client.AccessToken!);
        using var otherSocket = await NotificationSocket(other.Client.AccessToken!);
        Assert.Empty((await Notifications(hr)).Items);
        using var candidate = Candidate(invite);
        await Start(candidate, invite.InterviewId);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await NotificationChanged(ownSocket, timeout.Token);
        var list = await Notifications(hr); var n = Assert.Single(list.Items);
        Assert.Equal("InterviewStarted", n.Type); Assert.Equal("Interview", n.Target); Assert.Equal(invite.InterviewId, n.InterviewId);
        Assert.Contains("Test Candidate", n.Title); Assert.Null(n.ReadAtUtc); Assert.Equal(1, list.UnreadCount);
        Assert.Empty((await Notifications(other)).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Put, $"/api/notifications/{n.Id}/read", new NotificationReadRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await hr.Client.Send(HttpMethod.Put, $"/api/notifications/{n.Id}/read", new NotificationReadRequest(true))).StatusCode);
        await NotificationChanged(ownSocket, timeout.Token);
        var readAt = Assert.Single((await Notifications(hr, "?status=read")).Items).ReadAtUtc;
        Assert.Empty((await Notifications(hr, "?status=unread")).Items);
        await hr.Client.Send(HttpMethod.Put, $"/api/notifications/{n.Id}/read", new NotificationReadRequest(true));
        Assert.Equal(readAt, Assert.Single((await Notifications(hr)).Items).ReadAtUtc);
        await hr.Client.Send(HttpMethod.Put, $"/api/notifications/{n.Id}/read", new NotificationReadRequest(false));
        Assert.Equal(1, (await (await hr.Client.Send(HttpMethod.Get, "/api/notifications/unread-count")).Content.ReadFromJsonAsync<NotificationCountDto>())!.UnreadCount);
        Assert.Equal(HttpStatusCode.BadRequest, (await hr.Client.Send(HttpMethod.Get, "/api/notifications?status=unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await hr.Client.Send(HttpMethod.Get, "/api/notifications?pageSize=101")).StatusCode);
        await Start(candidate, invite.InterviewId); Assert.Single((await Notifications(hr)).Items);
        using var noEvent = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NotificationChanged(otherSocket, noEvent.Token));
        using var anon = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await candidate.GetAsync("/api/notifications")).StatusCode);
    }

    [PostgresFact]
    public async Task Interview_completion_notification_is_once_and_targets_report()
    {
        var (hr, invite) = await Invite(); using var candidate = Candidate(invite);
        await Start(candidate, invite.InterviewId);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null)).StatusCode);
        var n = Assert.Single((await Notifications(hr)).Items, n => n.Type == "InterviewCompleted");
        Assert.Equal("InterviewReport", n.Target); Assert.Equal(invite.InterviewId, n.InterviewId);
    }

    private sealed class FailingNotificationPublisher : INotificationPublisher
    {
        public Task ChangedAsync(Guid org, Guid recipient, CancellationToken ct) => throw new IOException("Synthetic transport failure");
    }
    private sealed class FailingNotificationRecipients : INotificationRecipients
    {
        public Task<List<Guid>> ActiveMembersAsync(Guid org, CancellationToken ct) => throw new InvalidOperationException("Synthetic persistence failure");
    }
    [PostgresFact]
    public async Task Notification_save_rolls_back_with_start_but_delivery_failure_preserves_committed_start()
    {
        var (hr, invite) = await Invite();
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var notifier = ActivatorUtilities.CreateInstance<NotificationService>(scope.ServiceProvider, new FailingNotificationRecipients());
            var service = ActivatorUtilities.CreateInstance<CandidateInterviewService>(scope.ServiceProvider, new TestAccess(invite.InterviewId), notifier);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(invite.InterviewId, default));
        }
        await f.Db(async db =>
        {
            Assert.Equal(HireYar.Domain.Interviews.InterviewStatus.Created, (await db.InterviewSessions.SingleAsync(n => n.Id == invite.InterviewId)).Status);
            Assert.Equal("Reserved", (await db.InterviewQuotaReservations.SingleAsync(n => n.InterviewSessionId == invite.InterviewId)).State);
            Assert.False(await db.Notifications.AnyAsync(n => n.InterviewSessionId == invite.InterviewId));
        });
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var notifier = ActivatorUtilities.CreateInstance<NotificationService>(scope.ServiceProvider, new FailingNotificationPublisher());
            var service = ActivatorUtilities.CreateInstance<CandidateInterviewService>(scope.ServiceProvider, new TestAccess(invite.InterviewId), notifier);
            await service.StartAsync(invite.InterviewId, default);
        }
        Assert.Single((await Notifications(hr)).Items);
    }

    private async Task Remind(Guid org)
    {
        await using var scope = f.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<NotificationReminderService>().GenerateAsync(org, default);
    }
    [PostgresFact]
    public async Task Subscription_reminders_use_effective_expiry_and_low_credit_rearms_after_topup()
    {
        var hr = await f.RegisterOwner(); Guid org = default; DateTimeOffset end = default;
        await f.Db(async db =>
        {
            org = (await db.Users.SingleAsync(u => u.Id == hr.Id)).OrganizationId;
            end = (await db.OrganizationMemberships.SingleAsync(m => m.OrganizationId == org)).ExpiresAtUtc;
        });
        // Clock changes exercise genuine immutable membership dates without modifying financial history.
        var original = f.Clock.Offset;
        try
        {
            f.Clock.Offset = end - DateTimeOffset.UtcNow - TimeSpan.FromDays(7.5);
            await Remind(org);
            await f.Db(async db => Assert.False(await db.Notifications.AnyAsync(n => n.OrganizationId == org && n.Type == "SubscriptionExpiring")));
            f.Clock.Offset = end - DateTimeOffset.UtcNow - TimeSpan.FromDays(6.5);
            await Remind(org); await Remind(org);
            await f.Db(async db => Assert.Single(await db.Notifications.Where(n => n.OrganizationId == org && n.Type == "SubscriptionExpiring").ToListAsync()));
            f.Clock.Offset = end - DateTimeOffset.UtcNow - TimeSpan.FromDays(2.5);
            await Remind(org); await Remind(org);
            await f.Db(async db => Assert.Equal(2, await db.Notifications.CountAsync(n => n.OrganizationId == org && n.Type == "SubscriptionExpiring")));
            f.Clock.Offset = end - DateTimeOffset.UtcNow - TimeSpan.FromDays(0.5);
            await Remind(org); await Remind(org);
            await f.Db(async db => Assert.Equal(3, await db.Notifications.CountAsync(n => n.OrganizationId == org && n.Type == "SubscriptionExpiring")));
        }
        finally { f.Clock.Offset = original; }
        var empty = await f.RegisterOwner(provisionMembership: false);
        var admin = await f.RegisterAdmin();
        var detail = (await (await admin.Client.Send(HttpMethod.Get, $"/api/admin/customers/{empty.Id}")).Content.ReadFromJsonAsync<HireYar.Application.Administration.AdminCustomerDetail>(TestJson.Options))!;
        var set = new HireYar.Application.Administration.AdminSubscriptionSetRequest(Guid.NewGuid(), detail.SubscriptionVersion!, f.Clock.GetUtcNow(), 1, 0, "Synthetic subscription");
        Assert.Equal(HttpStatusCode.OK, (await admin.Client.Send(HttpMethod.Post, $"/api/admin/customers/{empty.Id}/subscription/set", set)).StatusCode);
        var emptyOrg = detail.Customer.OrganizationId!.Value;
        await Remind(emptyOrg); await Remind(emptyOrg);
        Assert.Single((await Notifications(empty)).Items, n => n.Type == "InterviewCreditsLow");
        Assert.Equal(HttpStatusCode.OK, (await admin.Client.Send(HttpMethod.Post, $"/api/admin/customers/{empty.Id}/interview-credit",
            new HireYar.Application.Administration.AdminCustomerCreditRequest(Guid.NewGuid(), 3, "dana", "Synthetic refill"))).StatusCode);
        await Remind(emptyOrg);
        await Invite(empty); await Remind(emptyOrg);
        Assert.Single((await Notifications(empty)).Items, n => n.Type == "InterviewCreditsLow");
        await Invite(empty);
        await Remind(emptyOrg); await Remind(emptyOrg);
        Assert.Equal(2, (await Notifications(empty)).Items.Count(n => n.Type == "InterviewCreditsLow"));

    }

    [PostgresFact]
    public async Task Expiry_reminders_use_seven_three_one_day_windows_for_each_member_without_catchup()
    {
        var owner = await f.RegisterOwner();
        var member = await NotificationMember(owner);
        var late = await f.RegisterOwner();
        Guid org = default, lateOrg = default;
        DateTimeOffset end = default, lateEnd = default;
        await f.Db(async db =>
        {
            org = (await db.Users.SingleAsync(u => u.Id == owner.Id)).OrganizationId;
            lateOrg = (await db.Users.SingleAsync(u => u.Id == late.Id)).OrganizationId;
            end = (await db.OrganizationMemberships.SingleAsync(m => m.OrganizationId == org)).ExpiresAtUtc;
            lateEnd = (await db.OrganizationMemberships.SingleAsync(m => m.OrganizationId == lateOrg)).ExpiresAtUtc;
        });
        var original = f.Clock.Offset;
        try
        {
            // One-minute margins avoid wall-clock drift at the exact threshold.
            foreach (var (remaining, expectedCount, expectedThreshold) in new[]
            {
                (TimeSpan.FromDays(7).Add(TimeSpan.FromMinutes(1)), 0, 0),
                (TimeSpan.FromDays(7).Subtract(TimeSpan.FromMinutes(1)), 1, 7),
                (TimeSpan.FromDays(3).Add(TimeSpan.FromMinutes(1)), 1, 7),
                (TimeSpan.FromDays(3).Subtract(TimeSpan.FromMinutes(1)), 2, 3),
                (TimeSpan.FromDays(1).Add(TimeSpan.FromMinutes(1)), 2, 3),
                (TimeSpan.FromDays(1).Subtract(TimeSpan.FromMinutes(1)), 3, 1),
                (TimeSpan.FromMinutes(-1), 3, 1)
            })
            {
                f.Clock.Offset = end - DateTimeOffset.UtcNow - remaining;
                await Remind(org); await Remind(org);
                await f.Db(async db =>
                {
                    foreach (var recipient in new[] { owner.Id, member.Id })
                    {
                        var notices = await db.Notifications.Where(n => n.OrganizationId == org &&
                            n.RecipientUserId == recipient && n.Type == "SubscriptionExpiring").ToListAsync();
                        Assert.Equal(expectedCount, notices.Count);
                        if (expectedCount > 0)
                        {
                            var latest = notices.OrderByDescending(n => n.CreatedAtUtc).First();
                            Assert.Equal($"{end.ToUnixTimeSeconds()}:{expectedThreshold}", latest.SourceKey);
                            Assert.Contains($"{expectedThreshold} روز", latest.Title);
                            Assert.Equal("Subscription", latest.Target);
                        }
                    }
                });
            }
            f.Clock.Offset = lateEnd - DateTimeOffset.UtcNow - TimeSpan.FromHours(12);
            await Remind(lateOrg); await Remind(lateOrg);
            await f.Db(async db =>
            {
                var notice = Assert.Single(await db.Notifications.Where(n => n.OrganizationId == lateOrg &&
                    n.Type == "SubscriptionExpiring").ToListAsync());
                Assert.Equal($"{lateEnd.ToUnixTimeSeconds()}:1", notice.SourceKey);
            });
        }
        finally { f.Clock.Offset = original; }
    }

    [PostgresFact]
    public async Task Notifications_include_organizations_without_subscription()
    {
        var owner = await f.RegisterOwner(provisionMembership: false);
        Guid org = default;
        await f.Db(async db => org = (await db.Users.SingleAsync(u => u.Id == owner.Id)).OrganizationId);
        await Remind(org); await Remind(org);
        var items = (await Notifications(owner)).Items;
        Assert.Single(items, n => n.Type == "InterviewCreditsLow");
        Assert.DoesNotContain(items, n => n.Type == "SubscriptionExpiring");
    }

    [PostgresFact]
    public async Task Trial_receives_notifications_and_database_rejects_cross_organization_recipient()
    {
        var demo = await f.RegisterOwner(explicitSignup: true, provisionMembership: false);
        var (hr, invite) = await Invite(demo);
        using var candidate = Candidate(invite);
        await Start(candidate, invite.InterviewId);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null)).StatusCode);
        Guid demoOrg = default;
        await f.Db(async db => demoOrg = (await db.Users.SingleAsync(u => u.Id == hr.Id)).OrganizationId);
        await Remind(demoOrg);
        await Remind(demoOrg);
        var items = (await Notifications(hr)).Items;
        Assert.Single(items, n => n.Type == "InterviewStarted");
        Assert.Single(items, n => n.Type == "InterviewCompleted" && n.Target == "InterviewReport");
        Assert.Single(items, n => n.Type == "SubscriptionExpiring");
        Assert.Single(items, n => n.Type == "InterviewCreditsLow");
        var other = await f.RegisterOwner();
        await f.Db(async db =>
        {
            var org = (await db.Users.SingleAsync(u => u.Id == hr.Id)).OrganizationId;
            db.Notifications.Add(new() { OrganizationId = org, RecipientUserId = other.Id, Type = "InterviewStarted", SourceKey = invite.InterviewId.ToString(), Target = "Interview", InterviewSessionId = invite.InterviewId, Title = "Synthetic", CreatedAtUtc = f.Clock.GetUtcNow() });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }
}

[Collection("Postgres")]
public sealed class NotificationMigrationTests : IAsyncLifetime
{
    private readonly ApiFixture f = new();
    public Task InitializeAsync() => f.InitializeAsync();
    public Task DisposeAsync() => ((IAsyncLifetime)f).DisposeAsync();
    [PostgresFact]
    public async Task Migration_keeps_notification_history_on_attempted_down()
    {
        var owner = await f.RegisterOwner();
        await f.Db(async db =>
        {
            var org = (await db.Users.SingleAsync(u => u.Id == owner.Id)).OrganizationId;
            db.Notifications.Add(new() { OrganizationId = org, RecipientUserId = owner.Id, Type = "InterviewCreditsLow",
                SourceKey = "synthetic", Target = "InterviewCredits", Title = "Synthetic", CreatedAtUtc = f.Clock.GetUtcNow() });
            await db.SaveChangesAsync();
            var migrator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(db);
            var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migrator.MigrateAsync("20261020133348_AddCustomerAdministration"));
            Assert.Contains("Notification history exists", error.MessageText);
            Assert.Equal(1, await db.Notifications.CountAsync());
        });
    }
}
