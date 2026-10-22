using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Auth;
using HireYar.Application.Organizations;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Members;
using HireYar.Infrastructure.Organizations;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Tests;

[Collection("Postgres")]
public sealed partial class OrganizationAccessTests : IAsyncLifetime
{
    private readonly ApiFixture f = new();
    public Task InitializeAsync() => f.InitializeAsync();
    public Task DisposeAsync() => ((IAsyncLifetime)f).DisposeAsync();
    private static string Mobile() => "09" + Random.Shared.NextInt64(100_000_000, 1_000_000_000).ToString("D9");
    private async Task<(Browser Browser, Guid Id)> Member(Account owner, string role)
    {
        var mobile = Mobile();
        var response = await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(mobile, "عضو", "تست", role, "team@example.test"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invite = (await response.Content.ReadFromJsonAsync<TeamInvitationDto>(TestJson.Options))!;
        Assert.Equal("NotConfigured", invite.EmailDelivery);
        Assert.Equal(role, invite.AccessRoleCode);
        var b = f.Browser();
        await b.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(mobile));
        var accept = await b.Send(HttpMethod.Post, $"/api/auth/team-invitations/{invite.Id}/accept", new VerifyOtpRequest(mobile, f.PhoneOtp.Codes[mobile]));
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        b.AccessToken = (await accept.Content.ReadFromJsonAsync<AccessResponse>(TestJson.Options))!.AccessToken;
        var me = (await (await b.Send(HttpMethod.Get, "/api/auth/me")).Content.ReadFromJsonAsync<CurrentUserDto>(TestJson.Options))!;
        Assert.Equal(role, me.AccessRole!.Code);
        Assert.Equal(mobile, me.Mobile);
        Assert.NotNull(me.Permissions);
        return (b, me.Id);
    }
    [PostgresFact]
    public async Task Viewer_is_read_only_across_regular_wizard_report_and_ticket_endpoints()
    {
        var owner = await f.RegisterOwner(); var (viewer, _) = await Member(owner, "viewer");
        foreach (var path in new[] { "/api/dashboard", "/api/job-positions", "/api/interview-templates", "/api/interviews", "/api/tickets" })
            Assert.Equal(HttpStatusCode.OK, (await viewer.Send(HttpMethod.Get, path)).StatusCode);
        foreach (var (verb, path) in new[] { (HttpMethod.Post,"/api/job-positions"), (HttpMethod.Post,"/api/panel-wizard/positions"),
            (HttpMethod.Post,"/api/panel-wizard/templates"), (HttpMethod.Post,"/api/panel-wizard/question-suggestions"),
            (HttpMethod.Post,$"/api/interviews/{Guid.NewGuid()}/report/review"), (HttpMethod.Put,"/api/organization") })
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.Send(verb, path, new {})).StatusCode);
        using var raw = f.CreateClient(); raw.DefaultRequestHeaders.Authorization = new("Bearer", viewer.AccessToken);
        foreach (var path in new[] { "/api/panel-wizard/invitations", "/api/tickets" }) {
            using var body = new MultipartFormDataContent();
            Assert.Equal(HttpStatusCode.Forbidden, (await raw.PostAsync(path, body)).StatusCode);
        }
        foreach (var path in new[] { "/api/organization", "/api/team/members", "/api/subscriptions/current" })
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.Send(HttpMethod.Get, path)).StatusCode);
    }
    [PostgresFact]
    public async Task Access_changes_apply_to_existing_token_and_remain_tenant_scoped()
    {
        var owner = await f.RegisterOwner(); var other = await f.RegisterOwner(); var (member, id) = await Member(owner, "interview_manager");
        var token = member.AccessToken;
        Assert.Equal(HttpStatusCode.NotFound, (await member.Send(HttpMethod.Post, $"/api/interviews/{Guid.NewGuid()}/report/review", new { requestId = Guid.NewGuid(), decision = "review", reportFingerprint = new string('a',64) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Put, $"/api/team/members/{id}/access", new ChangeMemberAccessRequest("viewer"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Put, $"/api/team/members/{id}/access", new ChangeMemberAccessRequest("viewer"))).StatusCode);
        Assert.Equal(token, member.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Send(HttpMethod.Post, "/api/panel-wizard/positions", new {title="blocked",description="blocked"})).StatusCode);
        var me = (await (await member.Send(HttpMethod.Get,"/api/auth/me")).Content.ReadFromJsonAsync<CurrentUserDto>(TestJson.Options))!;
        Assert.Equal("viewer", me.AccessRole!.Code);
        await f.Db(async db => Assert.True(await db.AccessAudits.AnyAsync(x => x.SubjectId == id && x.Action == "MemberAccessChanged")));
    }
    [PostgresFact]
    public async Task Only_owner_can_assign_or_modify_administrators_and_owner_is_protected()
    {
        var owner = await f.RegisterOwner(); var (admin, id) = await Member(owner,"admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.Send(HttpMethod.Get,"/api/team/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Send(HttpMethod.Post,"/api/team/invitations", new InviteTeamMemberRequest(Mobile(),"نام","تست","admin", Email: "invite@example.test"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Send(HttpMethod.Put,$"/api/team/members/{owner.Id}/access",new ChangeMemberAccessRequest("viewer"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Send(HttpMethod.Put,$"/api/team/members/{id}/access",new ChangeMemberAccessRequest("viewer"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Send(HttpMethod.Post,$"/api/team/members/{id}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Put,$"/api/team/members/{id}/access",new ChangeMemberAccessRequest("viewer"))).StatusCode);
    }
    [PostgresFact]
    public async Task Superadmin_edits_catalog_with_revision_and_live_effect_and_hr_cannot_edit_it()
    {
        var system = await f.RegisterAdmin(); var owner = await f.RegisterOwner(); var (viewer, _) = await Member(owner,"viewer");
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.Client.Send(HttpMethod.Get,"/api/admin/access/roles")).StatusCode);
        var roles = (await (await system.Client.Send(HttpMethod.Get,"/api/admin/access/roles")).Content.ReadFromJsonAsync<List<AccessRoleDto>>(TestJson.Options))!;
        var role = roles.Single(x => x.Code == "viewer");
        var permissions = role.Sections.Select(x => x.Code == "jobs" ? x with { Access = SectionAccess.Write } : x).ToArray();
        var change = new SaveAccessRoleRequest(role.Code,role.Name,role.Description,true,false,role.Revision,permissions);
        var saved = await system.Client.Send(HttpMethod.Put,"/api/admin/access/roles",change);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var latest = (await saved.Content.ReadFromJsonAsync<AccessRoleDto>(TestJson.Options))!;
        Assert.Equal(HttpStatusCode.Conflict, (await system.Client.Send(HttpMethod.Put,"/api/admin/access/roles",change)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.Send(HttpMethod.Post,"/api/panel-wizard/positions",new {title="allowed live",description="Synthetic"})).StatusCode);
        var sections = (await (await system.Client.Send(HttpMethod.Get,"/api/admin/access/sections")).Content.ReadFromJsonAsync<List<AccessSectionDto>>(TestJson.Options))!;
        var jobs = sections.Single(x => x.Code == "jobs");
        Assert.Equal(HttpStatusCode.OK, (await system.Client.Send(HttpMethod.Put,"/api/admin/access/sections/jobs",new UpdateAccessSectionRequest("موقعیت‌ها","ویرایش بخش",false,jobs.Revision))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.Send(HttpMethod.Get,"/api/job-positions")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Get,"/api/job-positions")).StatusCode);
        var elevated = new SaveAccessRoleRequest("viewer",role.Name,role.Description,true,true,latest.Revision,permissions);
        Assert.Equal(HttpStatusCode.OK, (await system.Client.Send(HttpMethod.Put,"/api/admin/access/roles",elevated)).StatusCode);
    }
    [PostgresFact]
    public async Task Mobile_is_required_and_only_matching_mobile_otp_can_accept_unexpired_invitation()
    {
        var owner = await f.RegisterOwner();
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.Client.Send(HttpMethod.Post,"/api/team/invitations",new {firstName="نام",lastName="تست",email="test@example.test",accessRoleCode="viewer"})).StatusCode);
        var mobile=Mobile();
        var response=await owner.Client.Send(HttpMethod.Post,"/api/team/invitations",new InviteTeamMemberRequest(mobile,"نام","تست","viewer", Email: "invite@example.test"));
        var invite=(await response.Content.ReadFromJsonAsync<TeamInvitationDto>(TestJson.Options))!;
        Assert.Equal(TimeSpan.FromHours(72),invite.ExpiresAtUtc-invite.CreatedAtUtc);
        var b=f.Browser();var wrong=Mobile();await b.Send(HttpMethod.Post,"/api/auth/otp/request",new RequestOtpRequest(wrong));
        Assert.Equal(HttpStatusCode.NotFound,(await b.Send(HttpMethod.Post,$"/api/auth/team-invitations/{invite.Id}/accept",new VerifyOtpRequest(wrong,f.PhoneOtp.Codes[wrong]))).StatusCode);
        f.Clock.Offset=TimeSpan.FromHours(73);
        await b.Send(HttpMethod.Post,"/api/auth/otp/request",new RequestOtpRequest(mobile));
        Assert.Equal(HttpStatusCode.Conflict,(await b.Send(HttpMethod.Post,$"/api/auth/team-invitations/{invite.Id}/accept",new VerifyOtpRequest(mobile,f.PhoneOtp.Codes[mobile]))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await owner.Client.Send(HttpMethod.Post,$"/api/team/invitations/{invite.Id}/resend")).StatusCode);
    }
    [PostgresFact]
    public async Task Viewer_never_receives_candidate_access_credentials_from_read_endpoints()
    {
        var owner=await f.RegisterOwner();var (viewer,_)=await Member(owner,"viewer");
        var position=await owner.Client.Send(HttpMethod.Post,"/api/panel-wizard/positions",new HireYar.Application.Interviews.WizardPositionRequest("Synthetic position","Synthetic description"));
        var job=(await position.Content.ReadFromJsonAsync<HireYar.Application.JobPositions.JobDto>(TestJson.Options))!;
        var templateResponse=await owner.Client.Send(HttpMethod.Post,"/api/panel-wizard/templates",new HireYar.Application.Interviews.WizardTemplateRequest(job.Id,"Synthetic template","general",Enumerable.Range(1,5).Select(x=>"Synthetic question "+x).ToList(),"Welcome","Goodbye"));
        var template=(await templateResponse.Content.ReadFromJsonAsync<HireYar.Application.InterviewTemplates.TemplateDto>(TestJson.Options))!;
        using var raw=f.CreateClient();raw.DefaultRequestHeaders.Authorization=new("Bearer",owner.Client.AccessToken);
        using var form=new MultipartFormDataContent();
        foreach(var field in new Dictionary<string,string> { ["jobPositionId"]=job.Id.ToString(),["templateId"]=template.Id.ToString(),["firstName"]="Synthetic",["lastName"]="Candidate",["email"]="candidate@example.test",["phone"]=Mobile(),["resumeText"]="Synthetic candidate work history" })
            form.Add(new StringContent(field.Value),field.Key);
        form.Add(new ByteArrayContent("%PDF-1.7\nSynthetic resume"u8.ToArray()),"resume","resume.pdf");
        var invitationResponse=await raw.PostAsync("/api/panel-wizard/invitations",form);
        Assert.True(invitationResponse.IsSuccessStatusCode,await invitationResponse.Content.ReadAsStringAsync());
        var invitation=(await invitationResponse.Content.ReadFromJsonAsync<HireYar.Application.Interviews.PanelInvitationResult>(TestJson.Options))!.Invitation;
        var list=(await (await viewer.Send(HttpMethod.Get,"/api/interviews")).Content.ReadFromJsonAsync<HireYar.Application.Common.Page<HireYar.Application.Interviews.PanelInterviewDto>>(TestJson.Options))!;
        var row=Assert.Single(list.Items);Assert.Null(row.AccessCode);Assert.Null(row.InterviewUrl);Assert.False(row.CanResend);Assert.False(row.CanCancel);
        Assert.Equal(HttpStatusCode.Forbidden,(await viewer.Send(HttpMethod.Get,$"/api/interviews/{invitation.InterviewId}/access")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await owner.Client.Send(HttpMethod.Get,$"/api/interviews/{invitation.InterviewId}/access")).StatusCode);
    }
    [PostgresFact]
    public async Task Custom_roles_batch_partial_results_and_disabling_are_enforced()
    {
        var system=await f.RegisterAdmin();var owner=await f.RegisterOwner();
        var custom=new SaveAccessRoleRequest("custom_reader","ناظر سفارشی","فقط موقعیت شغلی",true,false,null,[new("jobs",SectionAccess.Read),new("dashboard",SectionAccess.Read)]);
        var created=await system.Client.Send(HttpMethod.Put,"/api/admin/access/roles",custom);
        Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var definition=(await created.Content.ReadFromJsonAsync<AccessRoleDto>(TestJson.Options))!;
        var (member,_)=await Member(owner,"custom_reader");
        Assert.Equal(HttpStatusCode.OK,(await member.Send(HttpMethod.Get,"/api/job-positions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await member.Send(HttpMethod.Get,"/api/interview-templates")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await system.Client.Send(HttpMethod.Put,"/api/admin/access/roles",custom with { Code="flexible_role",Sections=[new("team",SectionAccess.Read)] })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await system.Client.Send(HttpMethod.Put,"/api/admin/access/roles",custom with { Revision=definition.Revision,IsActive=false })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await member.Send(HttpMethod.Get,"/api/job-positions")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await member.Send(HttpMethod.Get,"/api/auth/me")).StatusCode);
        var mobile=Mobile(); var second=Mobile();
        var response=await owner.Client.Send(HttpMethod.Post,"/api/team/invitations/batch",new BatchTeamInvitationsRequest([
            new(mobile,"اول","تست","viewer","invite@example.test"),new(mobile,"تکراری","تست","viewer","invite@example.test"),new(second,"دوم","تست","viewer","invite@example.test")]));
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var rows=(await response.Content.ReadFromJsonAsync<List<BatchTeamInvitationResult>>(TestJson.Options))!;
        Assert.Equal(new[]{201,409,201},rows.Select(x=>x.Status));
        await f.Db(async db => Assert.Equal(2,await db.MobileTeamInvitations.CountAsync(x=>x.Mobile==mobile || x.Mobile==second)));
    }
    [PostgresFact]
    public async Task Upgrade_seeds_catalog_keeps_legacy_mobile_invites_and_audit_is_immutable()
    {
        await f.Db(async db => {
            var migrator=Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(db);
            const string previous="20261020143537_AddDashboardNotifications";
            await migrator.MigrateAsync(previous);
            var org=Guid.NewGuid();var invite=Guid.NewGuid();var now=f.Clock.GetUtcNow();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Organizations\" (\"Id\",\"Name\",\"CreatedAtUtc\",\"UpdatedAtUtc\") VALUES ({org},{"Legacy synthetic"},{now},{now})");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"MobileTeamInvitations\" (\"Id\",\"OrganizationId\",\"Mobile\",\"CreatedAtUtc\",\"ExpiresAtUtc\") VALUES ({invite},{org},{Mobile()},{now},{now.AddDays(3)})");
            var sql=migrator.GenerateScript(previous,null,Microsoft.EntityFrameworkCore.Migrations.MigrationsSqlGenerationOptions.Idempotent);
            await db.Database.ExecuteSqlRawAsync(sql);await db.Database.ExecuteSqlRawAsync(sql);
            Assert.Equal(3,await db.AccessRoles.CountAsync());Assert.Equal(10,await db.AccessSections.CountAsync());
            var legacy=await db.MobileTeamInvitations.SingleAsync(x=>x.Id==invite);
            Assert.Equal("interview_manager",legacy.AccessRoleCode);Assert.Equal("fa",legacy.Language);
            Assert.False(db.Database.HasPendingModelChanges());
        });
        var owner=await f.RegisterOwner();await Member(owner,"viewer");
        await f.Db(async db => {
            var ex=await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlRawAsync("UPDATE \"AccessAudits\" SET \"Action\"='Tampered'"));
            Assert.Equal(Npgsql.PostgresErrorCodes.CheckViolation,ex.SqlState);
            var migrator=Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(db);
            await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>migrator.MigrateAsync("20261020143537_AddDashboardNotifications"));
            Assert.True(await db.AccessAudits.AnyAsync());
        });
    }
    [PostgresFact]
    public async Task Team_email_is_delivered_to_loopback_smtp_as_utf8_html_and_plain_text()
    {
        var owner=await f.RegisterOwner();
        var response=await owner.Client.Send(HttpMethod.Post,"/api/team/invitations",new InviteTeamMemberRequest(Mobile(),"نام","تست","viewer", Email: "invite@example.test"));
        var invite=(await response.Content.ReadFromJsonAsync<TeamInvitationDto>(TestJson.Options))!;
        var listener=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);listener.Start();
        var port=((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var wire=new System.Text.StringBuilder();
        var server=Task.Run(async ()=> {
            using var socket=await listener.AcceptTcpClientAsync(timeout.Token);using var stream=socket.GetStream();
            using var reader=new StreamReader(stream);using var writer=new StreamWriter(stream){AutoFlush=true,NewLine="\r\n"};
            await writer.WriteLineAsync("220 localhost synthetic");
            while(await reader.ReadLineAsync(timeout.Token) is {} line) {
                if(line.StartsWith("DATA")) {await writer.WriteLineAsync("354 continue");while(await reader.ReadLineAsync(timeout.Token) is {} data && data!=".")wire.AppendLine(data);await writer.WriteLineAsync("250 accepted");}
                else if(line.StartsWith("QUIT")){await writer.WriteLineAsync("221 bye");break;}
                else await writer.WriteLineAsync("250 OK");
            }
        });
        try {
            await f.Db(async db=> {
                var row=await db.MobileTeamInvitations.SingleAsync(x=>x.Id==invite.Id);row.Email="synthetic@example.test";
                var options=Microsoft.Extensions.Options.Options.Create(new HireYar.Infrastructure.Interviews.InvitationNotificationOptions {Email=new(){Enabled=true,Host="127.0.0.1",Port=port,EnableSsl=false,FromAddress="sender@example.test"}});
                var security=Microsoft.Extensions.Options.Options.Create(new HireYar.Infrastructure.Auth.SecurityOptions {FrontendUrl="https://example.test"});
                var mail=new TeamInvitationEmail(db,options,security);
                Assert.Equal("Sent",await mail.SendAsync(row,timeout.Token));
            });
            await server;
            Assert.Contains("synthetic@example.test",wire.ToString());Assert.Contains("multipart/alternative",wire.ToString());
            Assert.Contains("text/plain",wire.ToString());Assert.Contains("text/html",wire.ToString());Assert.Contains("utf-8",wire.ToString(),StringComparison.OrdinalIgnoreCase);
        } finally {listener.Stop();}
    }

}
public sealed class TeamInvitationEmailTests
{
    [Theory]
    [InlineData("fa")]
    [InlineData("en")]
    [InlineData("ar")]
    public void Template_encodes_untrusted_content_and_preserves_mobile_login_and_plain_text(string lang)
    {
        var i=new MobileTeamInvitation { FirstName="<script>bad()</script>",LastName="تست",Language=lang,Message="<img src=x onerror=bad()>\nسلام" };
        var (html,text)=TeamInvitationEmail.Render(i,"<organization>","<role>","https://example.test/team-invitations/123");
        if (lang == "fa") {
            var preview = TeamInvitationEmail.Render(new MobileTeamInvitation { FirstName = "نگار", LastName = "شریفی", Language = "fa", Message = "سلام نگار، خوشحالیم که به تیم ما می‌پیوندی." }, "تیم هایریار", "مدیر مصاحبه", "https://example.test/team-invitations/synthetic-preview");
            var file = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/team-invitation-preview.html"));
            File.WriteAllText(file, preview.Html);
        }
        Assert.DoesNotContain("<script>",html); Assert.DoesNotContain("<img src=x",html);
        Assert.Contains("&lt;organization&gt;",html);Assert.Contains("#14b8a6",html);
        Assert.Contains("https://example.test/team-invitations/123",html);Assert.Contains("https://example.test/team-invitations/123",text);
        Assert.Contains("شماره موبایل",text);
        Assert.Contains("lang=\"fa\" dir=\"rtl\"",html);
        Assert.Contains("<body dir=\"rtl\"", html);
        Assert.Contains("<td dir=\"rtl\" align=\"right\"", html);
        Assert.Contains("font-family:Tahoma,Arial,sans-serif;font-size:14px;line-height:28px", html);
        Assert.Contains("<a dir=\"rtl\"", html);
        Assert.DoesNotContain("Accept invitation",html);
    }
}
