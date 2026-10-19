using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HireYar.Application.Support;
using HireYar.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class TicketTests : IAsyncLifetime
{
    private readonly ApiFixture f = new();
    public Task InitializeAsync() => f.InitializeAsync();
    public Task DisposeAsync() => ((IAsyncLifetime)f).DisposeAsync();
    private HttpClient Client(Account a)
    {
        var client = f.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", a.Client.AccessToken); return client;
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string route = "/api/tickets", string body = "پیام آزمایشی",
        params (string Name, string Type, byte[] Bytes)[] files)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(body), "body");
        if (route == "/api/tickets") { form.Add(new StringContent("درخواست پشتیبانی"), "subject"); form.Add(new StringContent("High"), "priority"); }
        foreach (var file in files)
        {
            var bytes = new ByteArrayContent(file.Bytes); bytes.Headers.ContentType = new(file.Type); form.Add(bytes, "files", file.Name);
        }
        return await client.PostAsync(route, form);
    }
    private static async Task<TicketDto> Ticket(HttpResponseMessage r, HttpStatusCode status = HttpStatusCode.OK)
    {
        Assert.Equal(status, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<TicketDto>(TestJson.Options))!;
    }
    [PostgresFact]
    public async Task Conversation_staff_replies_attachments_and_tenant_isolation_work_over_http()
    {
        var owner = await f.RegisterOwner(); var stranger = await f.RegisterOwner(); var admin = await f.RegisterAdmin();
        using var client = Client(owner); using var staff = Client(admin);
        var bytes = Encoding.UTF8.GetBytes("فایل ساختگی، بدون داده واقعی");
        var ticket = await Ticket(await Post(client, files: [("آزمایش.txt", "text/plain", bytes)]), HttpStatusCode.Created);
        Assert.Equal(TicketStatus.Open, ticket.Status); Assert.Equal(1, ticket.Revision);
        Assert.Equal(owner.Id, ticket.CreatedByUserId);
        var messages = (await (await owner.Client.Send(HttpMethod.Get, $"/api/tickets/{ticket.Id}/messages")).Content.ReadFromJsonAsync<TicketPage<TicketMessageDto>>(TestJson.Options))!;
        var first = Assert.Single(messages.Items); Assert.False(first.IsStaff);
        var attachment = Assert.Single(first.Attachments);
        var download = $"/api/tickets/{ticket.Id}/attachments/{attachment.Id}";
        var result = await client.GetAsync(download);
        Assert.Equal(bytes, await result.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/octet-stream", result.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", result.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("nosniff", Assert.Single(result.Headers.GetValues("X-Content-Type-Options")));
        Assert.Contains("no-store", result.Headers.CacheControl!.ToString());
        Assert.Contains("sandbox", Assert.Single(result.Headers.GetValues("Content-Security-Policy")));
        var reads = f.TicketS3.Reads;
        foreach (var path in new[] { $"/api/tickets/{ticket.Id}", $"/api/tickets/{ticket.Id}/messages", download })
            Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Get, path)).StatusCode);
        Assert.Equal(reads, f.TicketS3.Reads);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Browser().Send(HttpMethod.Get, download)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.Client.Send(HttpMethod.Get, "/api/admin/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.Client.Send(HttpMethod.Get, "/api/tickets")).StatusCode);
        var page = (await (await admin.Client.Send(HttpMethod.Get, "/api/admin/tickets?status=Open&priority=High&pageSize=1")).Content.ReadFromJsonAsync<TicketPage<TicketDto>>(TestJson.Options))!;
        Assert.Contains(page.Items, x => x.Id == ticket.Id);
        ticket = await Ticket(await Post(staff, $"/api/admin/tickets/{ticket.Id}/messages", "پاسخ پشتیبانی", ("reply.txt", "text/plain", bytes)));
        Assert.Equal(TicketStatus.Answered, ticket.Status);
        ticket = await Ticket(await Post(client, $"/api/tickets/{ticket.Id}/messages", "پیگیری کاربر"));
        Assert.Equal(TicketStatus.Open, ticket.Status); Assert.Equal(3, ticket.Revision);
        var all = (await (await admin.Client.Send(HttpMethod.Get, $"/api/admin/tickets/{ticket.Id}/messages")).Content.ReadFromJsonAsync<TicketPage<TicketMessageDto>>(TestJson.Options))!;
        Assert.Equal(new[] { false, true, false }, all.Items.Select(x => x.IsStaff));
        Assert.Equal(admin.Id, all.Items[1].AuthorUserId);
        Assert.Equal(bytes, await (await staff.GetAsync($"/api/admin/tickets/{ticket.Id}/attachments/{attachment.Id}")).Content.ReadAsByteArrayAsync());
        await f.Db(async db => {
            var stored = await db.TicketAttachments.Where(x => x.TicketId == ticket.Id).ToArrayAsync();
            Assert.All(stored, x => Assert.StartsWith($"tickets/{ticket.OrganizationId:N}/", x.StorageKey));
            Assert.All(stored, x => Assert.True(f.TicketS3.Objects.ContainsKey(x.StorageKey)));
            Assert.Null(db.Model.FindEntityType(typeof(TicketAttachment))!.FindProperty("Content"));
            Assert.False(db.Database.HasPendingModelChanges());
        });
        Assert.Equal("hireyar-ticketing", f.TicketS3.LastBucket); Assert.Equal(Amazon.S3.S3CannedACL.Private, f.TicketS3.LastAcl);
        Assert.DoesNotContain("storageKey", JsonSerializer.Serialize(all, TestJson.Options), StringComparison.OrdinalIgnoreCase);
    }
    [PostgresFact]
    public async Task Status_changes_are_versioned_closed_tickets_reject_replies_and_admin_token_cannot_be_forged_with_form_fields()
    {
        var owner = await f.RegisterOwner(); var stranger = await f.RegisterOwner(); var admin = await f.RegisterAdmin();
        using var client = Client(owner);
        var ticket = await Ticket(await Post(client), HttpStatusCode.Created);
        var route = $"/api/tickets/{ticket.Id}/status";
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.Client.Send(HttpMethod.Put, route, new TicketStatusRequest(TicketStatus.Answered, ticket.Revision))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Put, route, new TicketStatusRequest(TicketStatus.Closed, ticket.Revision))).StatusCode);
        ticket = await Ticket(await owner.Client.Send(HttpMethod.Put, route, new TicketStatusRequest(TicketStatus.Closed, ticket.Revision)));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, $"/api/tickets/{ticket.Id}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Client.Send(HttpMethod.Put, $"/api/admin/tickets/{ticket.Id}/status", new TicketStatusRequest(TicketStatus.Open, 1))).StatusCode);
        ticket = await Ticket(await admin.Client.Send(HttpMethod.Put, $"/api/admin/tickets/{ticket.Id}/status", new TicketStatusRequest(TicketStatus.Open, ticket.Revision)));
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/tickets?pageSize=51")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/tickets?status=99")).StatusCode);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("اختبار"), "subject"); form.Add(new StringContent("پیام"), "body");
        form.Add(new StringContent(stranger.Id.ToString()), "organizationId"); form.Add(new StringContent("true"), "administrator");
        var forged = await Ticket(await client.PostAsync("/api/tickets", form), HttpStatusCode.Created);
        Assert.Equal(ticket.OrganizationId, forged.OrganizationId);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Get, $"/api/tickets/{forged.Id}")).StatusCode);
    }
    [PostgresFact]
    public async Task Upload_validation_rejects_executables_traversal_spoofing_empty_oversize_and_excess_files_before_S3()
    {
        var owner = await f.RegisterOwner(); using var client = Client(owner);
        var invalid = new (string Name, string Type, byte[] Bytes)[] {
            ("run.exe","application/octet-stream","MZtest"u8.ToArray()),
            ("../note.txt","text/plain","test"u8.ToArray()),
            ("name:stream.txt","text/plain","test"u8.ToArray()),
            ("image.svg","image/svg+xml","<svg/>"u8.ToArray()),
            ("archive.zip","application/zip","PKtest"u8.ToArray()),
            ("fake.png","image/png","<script>alert(1)</script>"u8.ToArray()),
            ("fake.pdf","application/pdf","<html>test</html>"u8.ToArray()),
            ("note.txt","image/png","test"u8.ToArray()),
            ("note.txt","text/plain",[0,1,2,3]),
            ("note.txt","text/plain",[255,255]),
            ("note.txt","text/plain",[]),
            ("note\u202Etxt.txt","text/plain","test"u8.ToArray())
        };
        foreach (var item in invalid)
        {
            var r = await Post(client, files: [item]);
            Assert.True(r.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge, $"{item.Name}: {r.StatusCode}");
        }
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await Post(client, files: [("large.txt","text/plain", new byte[TicketFileRules.MaxFileBytes + 1])])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, files: Enumerable.Range(0,4).Select(i => ($"file{i}.txt", "text/plain", "test"u8.ToArray())).ToArray())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, body: "   ")).StatusCode);
        Assert.Empty(f.TicketS3.Objects);
        await f.Db(async db => Assert.Equal(0, await db.SupportTickets.CountAsync()));
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nsynthetic\n%%EOF\n");
        Assert.Equal(HttpStatusCode.Created, (await Post(client, files: [("document.pdf", "application/pdf", pdf)])).StatusCode);
    }
    [PostgresFact]
    public async Task Public_bucket_and_lost_PUT_responses_fail_closed_without_metadata_or_orphan()
    {
        var owner = await f.RegisterOwner(); using var client = Client(owner);
        f.TicketS3.PublicAcl = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Post(client, files: [("a.txt","text/plain","test"u8.ToArray())])).StatusCode);
        f.TicketS3.PublicAcl = false;
        f.TicketS3.Policy = """{"Statement":[{"Effect":"Allow","Principal":"*","Action":"s3:GetObject","Resource":"*"}]}""";
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Post(client, files: [("a.txt","text/plain","test"u8.ToArray())])).StatusCode);
        f.TicketS3.Policy = null; f.TicketS3.FailPut = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Post(client, files: [("a.txt","text/plain","test"u8.ToArray())])).StatusCode);
        Assert.Empty(f.TicketS3.Objects);
        await f.Db(async db => Assert.Equal(0, await db.SupportTickets.CountAsync()));
        f.TicketS3.FailPut = false;
        var ticket = await Ticket(await Post(client, files: [("a.txt","text/plain","test"u8.ToArray())]), HttpStatusCode.Created);
        var messages = (await (await owner.Client.Send(HttpMethod.Get, $"/api/tickets/{ticket.Id}/messages")).Content.ReadFromJsonAsync<TicketPage<TicketMessageDto>>(TestJson.Options))!;
        var file = Assert.Single(Assert.Single(messages.Items).Attachments);
        f.TicketS3.PublicAcl = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync($"/api/tickets/{ticket.Id}/attachments/{file.Id}")).StatusCode);
    }
    [PostgresFact]
    public async Task Closing_during_upload_rechecks_state_and_cleans_uploaded_object_without_holding_DB_lock()
    {
        var owner = await f.RegisterOwner(); var admin = await f.RegisterAdmin(); using var client = Client(owner);
        var ticket = await Ticket(await Post(client), HttpStatusCode.Created);
        f.TicketS3.OnPut = async () => {
            Assert.Equal(HttpStatusCode.OK, (await admin.Client.Send(HttpMethod.Put, $"/api/admin/tickets/{ticket.Id}/status", new TicketStatusRequest(TicketStatus.Closed, ticket.Revision))).StatusCode);
        };
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, $"/api/tickets/{ticket.Id}/messages", "پیام", ("a.txt","text/plain","test"u8.ToArray()))).StatusCode);
        Assert.Empty(f.TicketS3.Objects);
        await f.Db(async db => { Assert.Equal(1, await db.TicketMessages.CountAsync(x => x.TicketId == ticket.Id)); Assert.Equal(0, await db.TicketAttachments.CountAsync()); });
    }
    [PostgresFact]
    public async Task Concurrent_replies_are_serialized_and_cross_organization_FKs_and_migration_down_protect_data()
    {
        var owner = await f.RegisterOwner(); var other = await f.RegisterOwner(); using var client = Client(owner);
        var ticket = await Ticket(await Post(client), HttpStatusCode.Created);
        var replies = await Task.WhenAll(Enumerable.Range(0,5).Select(i => Post(client, $"/api/tickets/{ticket.Id}/messages", $"پیام {i}")));
        Assert.All(replies, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        ticket = (await (await owner.Client.Send(HttpMethod.Get, $"/api/tickets/{ticket.Id}")).Content.ReadFromJsonAsync<TicketDto>(TestJson.Options))!;
        Assert.Equal(6, ticket.Revision);
        await f.Db(async db => {
            var otherOrg = await db.Users.Where(x => x.Id == other.Id).Select(x => x.OrganizationId).SingleAsync();
            db.TicketMessages.Add(new() { OrganizationId = otherOrg, TicketId = ticket.Id, AuthorUserId = other.Id, Body = "invalid", CreatedAtUtc = f.Clock.GetUtcNow() });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            var migrations=db.Database.GetMigrations().ToArray();
            var ticketIndex=Array.FindIndex(migrations,m=>m.EndsWith("_AddSupportTickets",StringComparison.Ordinal));
            Assert.True(ticketIndex>0);
            var previous=migrations[ticketIndex-1];
            await Assert.ThrowsAnyAsync<Exception>(() => db.GetService<IMigrator>().MigrateAsync(previous));
            Assert.True(await db.SupportTickets.AnyAsync(x => x.Id == ticket.Id));
        });
    }
    [PostgresFact]
    public async Task Ticket_limits_are_enforced_and_failed_quota_uploads_are_removed()
    {
        var owner = await f.RegisterOwner(); using var client = Client(owner);
        TicketDto? first = null;
        for (var i=0; i<20; i++) { var t = await Ticket(await Post(client), HttpStatusCode.Created); first ??= t; }
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, files: [("a.txt","text/plain","test"u8.ToArray())])).StatusCode);
        Assert.Empty(f.TicketS3.Objects);
        await owner.Client.Send(HttpMethod.Put, $"/api/tickets/{first!.Id}/status", new TicketStatusRequest(TicketStatus.Closed, first.Revision));
        Assert.Equal(HttpStatusCode.Created, (await Post(client)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.Send(HttpMethod.Put, $"/api/tickets/{first.Id}/status", new TicketStatusRequest(TicketStatus.Open, 2))).StatusCode);
    }

    [PostgresFact]
    public async Task Recruiter_and_owner_share_only_their_organizations_tickets_without_a_subscription_gate()
    {
        var owner = await f.RegisterOwner(explicitSignup: true);
        var mobile = "09" + Random.Shared.NextInt64(100_000_000, 1_000_000_000).ToString("D9");
        var invitation = await owner.Client.Send(HttpMethod.Post, "/api/team/invitations",
            new HireYar.Application.Organizations.InviteTeamMemberRequest(mobile, "عضو", "آزمایشی"));
        Assert.Equal(HttpStatusCode.Created, invitation.StatusCode);
        var invite = (await invitation.Content.ReadFromJsonAsync<HireYar.Application.Organizations.TeamInvitationDto>(TestJson.Options))!;
        var member = f.Browser();
        Assert.Equal(HttpStatusCode.OK, (await member.Send(HttpMethod.Post, "/api/auth/otp/request", new HireYar.Application.Auth.RequestOtpRequest(mobile))).StatusCode);
        var accepted = await member.Send(HttpMethod.Post, $"/api/auth/team-invitations/{invite.Id}/accept",
            new HireYar.Application.Auth.VerifyOtpRequest(mobile, f.PhoneOtp.Codes[mobile]));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        member.AccessToken = (await accepted.Content.ReadFromJsonAsync<HireYar.Application.Auth.AccessResponse>(TestJson.Options))!.AccessToken;
        using var client = f.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", member.AccessToken);
        var ticket = await Ticket(await Post(client), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Get, $"/api/tickets/{ticket.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/tickets/{ticket.Id}/messages")).StatusCode);
        var stranger = await f.RegisterOwner();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Get, $"/api/tickets/{ticket.Id}/messages")).StatusCode);
    }
    [PostgresFact]
    public async Task Size_boundary_ticket_storage_quota_message_budget_and_status_races_are_enforced()
    {
        var owner = await f.RegisterOwner(); using var client = Client(owner);
        var bytes = Enumerable.Repeat((byte)'a', TicketFileRules.MaxFileBytes).ToArray();
        var ticket = await Ticket(await Post(client, files: [("boundary.txt", "text/plain", bytes)]), HttpStatusCode.Created);
        await f.Db(async db => {
            var message = await db.TicketMessages.SingleAsync(x => x.TicketId == ticket.Id);
            // Synthetic quota metadata avoids allocating 100 MiB of unrelated fixture content.
            for (var i=0; i<19; i++) db.TicketAttachments.Add(new TicketAttachment {
                OrganizationId = ticket.OrganizationId, TicketId = ticket.Id, MessageId = message.Id,
                FileName = "quota.txt", ContentType = "text/plain", Length = TicketFileRules.MaxFileBytes,
                StorageKey = $"tickets/{ticket.OrganizationId:N}/{Guid.NewGuid():N}" });
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, $"/api/tickets/{ticket.Id}/messages", "quota", ("a.txt","text/plain","a"u8.ToArray()))).StatusCode);
        Assert.Single(f.TicketS3.Objects);
        await f.Db(async db => {
            for (var i=0; i<4; i++)
            {
                var t = new SupportTicket { OrganizationId = ticket.OrganizationId, CreatedByUserId = owner.Id, Subject = "quota",
                    Status = TicketStatus.Closed, CreatedAtUtc = f.Clock.GetUtcNow(), UpdatedAtUtc = f.Clock.GetUtcNow() };
                var m = new TicketMessage { OrganizationId = ticket.OrganizationId, TicketId = t.Id, AuthorUserId = owner.Id, Body = "quota", CreatedAtUtc = f.Clock.GetUtcNow() };
                db.SupportTickets.Add(t); db.TicketMessages.Add(m);
                for (var j=0; j<20; j++) db.TicketAttachments.Add(new TicketAttachment {
                    OrganizationId = t.OrganizationId, TicketId = t.Id, MessageId = m.Id, FileName = "quota.txt",
                    ContentType = "text/plain", Length = TicketFileRules.MaxFileBytes, StorageKey = $"tickets/{t.OrganizationId:N}/{Guid.NewGuid():N}" });
            }
            await db.SaveChangesAsync();
        });
        var organizationLimit = await Post(client, files: [("a.txt","text/plain","a"u8.ToArray())]);
        Assert.Equal(HttpStatusCode.Conflict, organizationLimit.StatusCode);
        using (var problem = JsonDocument.Parse(await organizationLimit.Content.ReadAsStringAsync()))
            Assert.Equal("ticket_storage_limit", problem.RootElement.GetProperty("code").GetString());
        Assert.Single(f.TicketS3.Objects);
        var admin = await f.RegisterAdmin();
        var concurrent = await Task.WhenAll(
            owner.Client.Send(HttpMethod.Put, $"/api/tickets/{ticket.Id}/status", new TicketStatusRequest(TicketStatus.Closed, 1)),
            admin.Client.Send(HttpMethod.Put, $"/api/admin/tickets/{ticket.Id}/status", new TicketStatusRequest(TicketStatus.Answered, 1)));
        Assert.Contains(concurrent, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Contains(concurrent, x => x.StatusCode == HttpStatusCode.Conflict);
        ticket = await Ticket(await owner.Client.Send(HttpMethod.Put, $"/api/tickets/{ticket.Id}/status", new TicketStatusRequest(TicketStatus.Open, 2)));
        await f.Db(async db => {
            for (var i=0; i<199; i++) db.TicketMessages.Add(new TicketMessage {
                OrganizationId = ticket.OrganizationId, TicketId = ticket.Id, AuthorUserId = owner.Id, Body = "budget", CreatedAtUtc = f.Clock.GetUtcNow() });
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, $"/api/tickets/{ticket.Id}/messages", "budget", ("a.txt","text/plain","a"u8.ToArray()))).StatusCode);
        Assert.Single(f.TicketS3.Objects);
    }
    [PostgresFact]
    public async Task OpenApi_has_separate_authentication_and_multipart_files_and_rate_limits_apply()
    {
        var owner = await f.RegisterOwner();
        using var client = Client(owner);
        using var openapi = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = openapi.RootElement.GetProperty("paths");
        var admin = paths.GetProperty("/api/admin/tickets").GetProperty("get");
        Assert.True(admin.GetProperty("security")[0].TryGetProperty("SuperAdmin", out _));
        Assert.True(paths.GetProperty("/api/tickets").GetProperty("get").GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.True(paths.GetProperty("/api/tickets").GetProperty("post").GetProperty("requestBody").GetProperty("content").TryGetProperty("multipart/form-data", out _));
        for (var i=0; i<60; i++) Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/tickets")).StatusCode);
    }

}
