using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Organizations;
using HireYar.Application.Auth;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Members;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Tests;

public sealed partial class OrganizationAccessTests
{
    private static InviteTeamMemberRequest CapacityInvite(string? mobile = null, string role = "viewer") =>
        new(mobile ?? Mobile(), "نام", "آزمایشی", role, "invite@example.test");
    private static async Task<TeamInvitationCapacityDto> Capacity(Browser browser)
    {
        var response = await browser.Send(HttpMethod.Get, "/api/team/invitation-capacity");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<TeamInvitationCapacityDto>(TestJson.Options))!;
    }
    [PostgresFact]
    public async Task Capacity_counts_owner_active_members_and_pending_only_and_refreshes_after_each_lifecycle_change()
    {
        var owner = await f.RegisterOwner(explicitSignup: true, provisionMembership: false);
        var initial = await Capacity(owner.Client);
        Assert.True(initial.HasActiveSubscription); Assert.False(initial.UnlimitedTeam); Assert.Equal(1, initial.ActiveMembers);
        Assert.Equal(0, initial.PendingInvitations); Assert.Equal(initial.TeamLimit - 1, initial.AvailableSlots);
        Assert.Equal(initial.AvailableSlots, initial.MaxBatchSize);
        var request = CapacityInvite();
        var response = await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", request);
        var invite = (await response.Content.ReadFromJsonAsync<TeamInvitationDto>(TestJson.Options))!;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var pending = await Capacity(owner.Client);
        Assert.Equal(1, pending.PendingInvitations); Assert.Equal(initial.AvailableSlots - 1, pending.AvailableSlots);
        var member = f.Browser(); await member.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(invite.Mobile));
        Assert.Equal(HttpStatusCode.OK, (await member.Send(HttpMethod.Post, $"/api/auth/team-invitations/{invite.Id}/accept", new VerifyOtpRequest(invite.Mobile, f.PhoneOtp.Codes[invite.Mobile]))).StatusCode);
        var active = await Capacity(owner.Client);
        Assert.Equal(2, active.ActiveMembers); Assert.Equal(0, active.PendingInvitations); Assert.Equal(pending.AvailableSlots, active.AvailableSlots);
        Guid memberId = Guid.Empty; await f.Db(async db => memberId = await db.Users.Where(x => x.PhoneNumber == invite.Mobile).Select(x => x.Id).SingleAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Post, $"/api/team/members/{memberId}/deactivate")).StatusCode);
        Assert.Equal(initial.AvailableSlots, (await Capacity(owner.Client)).AvailableSlots);
        var cancellation = (await (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", CapacityInvite())).Content.ReadFromJsonAsync<TeamInvitationDto>(TestJson.Options))!;
        await owner.Client.Send(HttpMethod.Delete, $"/api/team/invitations/{cancellation.Id}");
        Assert.Equal(initial.AvailableSlots, (await Capacity(owner.Client)).AvailableSlots);
        var expired = (await (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", CapacityInvite())).Content.ReadFromJsonAsync<TeamInvitationDto>(TestJson.Options))!;
        await f.Db(async db => { var row = await db.MobileTeamInvitations.SingleAsync(x => x.Id == expired.Id); row.CreatedAtUtc = f.Clock.GetUtcNow().AddDays(-4); row.ExpiresAtUtc = f.Clock.GetUtcNow().AddDays(-1); await db.SaveChangesAsync(); });
        Assert.Equal(initial.AvailableSlots, (await Capacity(owner.Client)).AvailableSlots);
    }
    [PostgresFact]
    public async Task Batch_rejects_more_rows_than_available_before_creating_any_then_accepts_exact_limit()
    {
        var owner = await f.RegisterOwner(explicitSignup: true, provisionMembership: false);
        var initial = await Capacity(owner.Client); Assert.InRange(initial.MaxBatchSize, 1, 49);
        var tooMany = Enumerable.Range(0, initial.MaxBatchSize + 1).Select(_ => CapacityInvite()).ToArray();
        var rejected = await owner.Client.Send(HttpMethod.Post, "/api/team/invitations/batch", new BatchTeamInvitationsRequest(tooMany));
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        await f.Db(async db => { Assert.Empty(await db.MobileTeamInvitations.ToListAsync()); Assert.False(await db.AccessAudits.AnyAsync(x => x.Action == "InvitationCreated")); });
        var response = await owner.Client.Send(HttpMethod.Post, "/api/team/invitations/batch", new BatchTeamInvitationsRequest(tooMany.Take(initial.MaxBatchSize).ToArray()));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = (await response.Content.ReadFromJsonAsync<List<BatchTeamInvitationResult>>(TestJson.Options))!;
        Assert.Equal(initial.MaxBatchSize, results.Count); Assert.All(results, x => { Assert.Equal(201, x.Status); Assert.Equal("NotConfigured", x.Invitation!.EmailDelivery); });
        var full = await Capacity(owner.Client); Assert.Equal(0, full.AvailableSlots); Assert.Equal(0, full.MaxBatchSize);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", CapacityInvite())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations/batch", new BatchTeamInvitationsRequest([CapacityInvite()]))).StatusCode);
        var other = await f.RegisterOwner(); var otherCapacity = await Capacity(other.Client);
        Assert.Equal(1, otherCapacity.OccupiedSlots); Assert.True(otherCapacity.UnlimitedTeam); Assert.Null(otherCapacity.AvailableSlots); Assert.Equal(50, otherCapacity.MaxBatchSize);
    }
    [PostgresFact]
    public async Task Concurrent_batches_cannot_overbook_the_same_organization()
    {
        var owner = await f.RegisterOwner(explicitSignup: true, provisionMembership: false);
        var capacity = await Capacity(owner.Client);
        BatchTeamInvitationsRequest Batch() => new(Enumerable.Range(0, capacity.MaxBatchSize).Select(_ => CapacityInvite()).ToArray());
        var outcomes = await Task.WhenAll(owner.Client.Send(HttpMethod.Post, "/api/team/invitations/batch", Batch()), owner.Client.Send(HttpMethod.Post, "/api/team/invitations/batch", Batch()));
        Assert.Single(outcomes, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(outcomes, x => x.StatusCode == HttpStatusCode.Conflict);
        await f.Db(async db => Assert.Equal(capacity.MaxBatchSize, await db.MobileTeamInvitations.CountAsync()));
        Assert.Equal(0, (await Capacity(owner.Client)).MaxBatchSize);
    }
    [PostgresFact]
    public async Task Email_is_required_in_single_and_every_batch_row_and_validation_has_no_partial_effects()
    {
        var owner = await f.RegisterOwner();
        var missing = new { mobile = Mobile(), firstName = "نام", lastName = "تست", accessRoleCode = "viewer" };
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", missing)).StatusCode);
        foreach (var email in new string?[] { null, "", "   ", "invalid" }) {
            var invalid = CapacityInvite() with { Email = email };
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", invalid)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations/batch", new BatchTeamInvitationsRequest([CapacityInvite(), invalid]))).StatusCode);
        }
        await f.Db(async db => { Assert.Empty(await db.MobileTeamInvitations.ToListAsync()); Assert.False(await db.AccessAudits.AnyAsync(x => x.Action == "InvitationCreated")); });
        Assert.Equal(HttpStatusCode.Created, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", CapacityInvite())).StatusCode);
    }
    [PostgresFact]
    public async Task Capacity_is_zero_without_active_subscription_and_team_read_does_not_allow_sending()
    {
        var owner = await f.RegisterOwner(provisionMembership: false);
        var empty = await Capacity(owner.Client); Assert.False(empty.HasActiveSubscription); Assert.Equal(0, empty.AvailableSlots); Assert.Equal(0, empty.MaxBatchSize);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations/batch", new BatchTeamInvitationsRequest([CapacityInvite()]))).StatusCode);
        var system = await f.RegisterAdmin(); var active = await f.RegisterOwner();
        var role = await SaveRole(system, "team_reader", new SectionPermission("team", SectionAccess.Read));
        var (reader, _) = await Member(active, role.Code);
        Assert.Equal(2, (await Capacity(reader)).ActiveMembers);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.Send(HttpMethod.Post, "/api/team/invitations/batch", new BatchTeamInvitationsRequest([CapacityInvite()]))).StatusCode);
        var (viewer, _) = await Member(active, "viewer");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.Send(HttpMethod.Get, "/api/team/invitation-capacity")).StatusCode);
    }
    [PostgresFact]
    public async Task Batch_savepoints_preserve_successes_and_undo_expired_invitation_changes_on_forbidden_reactivation()
    {
        var system = await f.RegisterAdmin(); var owner = await f.RegisterOwner();
        var low = await SaveRole(system, "low_jobs", new SectionPermission("jobs", SectionAccess.Read));
        var high = await SaveRole(system, "high_jobs", new SectionPermission("jobs", SectionAccess.Write));
        var delegated = await SaveRole(system, "delegate_team", new("team", SectionAccess.Write), new("jobs", SectionAccess.Read));
        var (_, inactiveId) = await Member(owner, high.Code);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Post, $"/api/team/members/{inactiveId}/deactivate")).StatusCode);
        string mobile = ""; Guid expiredId = Guid.Empty;
        await f.Db(async db => {
            var target = await db.Users.SingleAsync(x => x.Id == inactiveId); mobile = target.PhoneNumber!;
            var row = new MobileTeamInvitation { OrganizationId = target.OrganizationId, Mobile = mobile, FirstName = "نام", LastName = "تست", Email = "invite@example.test", AccessRoleCode = low.Code, CreatedAtUtc = f.Clock.GetUtcNow().AddDays(-4), ExpiresAtUtc = f.Clock.GetUtcNow().AddDays(-1) };
            db.MobileTeamInvitations.Add(row); await db.SaveChangesAsync(); expiredId = row.Id;
        });
        var (actor, _) = await Member(owner, delegated.Code);
        var response = await actor.Send(HttpMethod.Post, "/api/team/invitations/batch", new BatchTeamInvitationsRequest([CapacityInvite(role: low.Code), CapacityInvite(mobile, low.Code), CapacityInvite(role: low.Code)]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<List<BatchTeamInvitationResult>>(TestJson.Options))!;
        Assert.Equal(new[] { 201, 403, 201 }, result.Select(x => x.Status));
        var successfulMobiles = result.Where(x => x.Status == 201).Select(x => x.Mobile).ToArray();
        await f.Db(async db => {
            Assert.Null((await db.MobileTeamInvitations.SingleAsync(x => x.Id == expiredId)).CancelledAtUtc);
            Assert.False((await db.Users.SingleAsync(x => x.Id == inactiveId)).IsActive);
            Assert.Equal(2, await db.MobileTeamInvitations.CountAsync(x => successfulMobiles.Contains(x.Mobile)));
        });
    }
}
