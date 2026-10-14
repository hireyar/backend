using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Auth;
using HireYar.Application.Common;
using HireYar.Application.Organizations;
using HireYar.Application.Interviews;
using HireYar.Application.JobPositions;
using HireYar.Application.InterviewTemplates;
using HireYar.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
namespace HireYar.Tests;

[Collection("Postgres")]
public sealed partial class MobileTeamTests : IAsyncLifetime
{
    private readonly ApiFixture f = new();
    public Task InitializeAsync() => f.InitializeAsync();
    public Task DisposeAsync() => ((IAsyncLifetime)f).DisposeAsync();
    private static string Mobile() => "09" + Random.Shared.NextInt64(100_000_000, 1_000_000_000).ToString("D9");
    private async Task<TeamInvitationDto> Invite(Account owner, string mobile, string firstName = "نام", string lastName = "آزمایشی")
    {
        var r = await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(mobile, firstName, lastName));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<TeamInvitationDto>(BillingJson.Options))!;
    }
    private async Task<Browser> Accept(TeamInvitationDto invite)
    {
        var b = f.Browser();
        Assert.Equal(HttpStatusCode.OK, (await b.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(invite.Mobile))).StatusCode);
        var r = await b.Send(HttpMethod.Post, $"/api/auth/team-invitations/{invite.Id}/accept", new VerifyOtpRequest(invite.Mobile, f.PhoneOtp.Codes[invite.Mobile]));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        b.AccessToken = (await r.Content.ReadFromJsonAsync<AccessResponse>())!.AccessToken;
        return b;
    }
    private static async Task<CurrentUserDto> Me(Browser b) => (await (await b.Send(HttpMethod.Get, "/api/auth/me")).Content.ReadFromJsonAsync<CurrentUserDto>(BillingJson.Options))!;

    [PostgresFact]
    public async Task Acceptance_creates_scoped_recruiter_without_new_organization_or_trial_and_enforces_permissions()
    {
        var owner = await f.RegisterOwner(); var other = await f.RegisterOwner(); var mobile = Mobile();
        var invitation = await Invite(owner, mobile); var member = await Accept(invitation);
        var me = await Me(member); var hr = await Me(owner.Client);
        Assert.Equal(OrganizationRole.Recruiter, me.Role); Assert.Equal(hr.OrganizationId, me.OrganizationId);
        await f.Db(async db => {
            Assert.Equal(2, await db.Organizations.CountAsync()); Assert.Equal(0, await db.RegistrationTrials.CountAsync());
            Assert.NotNull((await db.MobileTeamInvitations.SingleAsync(x => x.Id == invitation.Id)).AcceptedAtUtc);
            Assert.Equal(2, await db.Users.CountAsync(x => x.OrganizationId == me.OrganizationId && x.IsActive));
        });
        Assert.Equal(HttpStatusCode.OK, (await member.Send(HttpMethod.Get, "/api/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.Send(HttpMethod.Get, "/api/organization")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.Send(HttpMethod.Put, "/api/auth/me", new NameRequest("Member name"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Send(HttpMethod.Put, "/api/auth/me", new NameRequest("Should not save", "Forbidden org"))).StatusCode);
        Assert.Equal("Member name", (await Me(member)).FullName);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Send(HttpMethod.Put, "/api/organization", new OrganizationRequest("Forbidden"))).StatusCode);
        foreach (var path in new[] { "/api/team/members", "/api/team/invitations", "/api/billing/panel", "/api/billing/subscription", "/api/billing/financial-history", "/api/billing/ai-credit" })
            Assert.Equal(HttpStatusCode.Forbidden, (await member.Send(HttpMethod.Get, path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(Mobile(), "نام", "آزمایشی"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Send(HttpMethod.Post, $"/api/team/members/{owner.Id}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Send(HttpMethod.Post, "/api/billing/subscription/purchases", new { packagePeriodId = Guid.NewGuid() })).StatusCode);
        var randomId = Guid.NewGuid();
        foreach (var path in new[] { $"/api/interviews/{randomId}/evaluation-report", $"/api/interviews/{randomId}/review", $"/api/interviews/{randomId}/evaluation-report/print" })
            Assert.Equal(HttpStatusCode.NotFound, (await member.Send(HttpMethod.Get, path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Send(HttpMethod.Post, $"/api/interviews/{randomId}/evaluation-report")).StatusCode);
        var jobResponse = await member.Send(HttpMethod.Post, "/api/panel-wizard/positions", new WizardPositionRequest("Member job", "Synthetic job"));
        Assert.Equal(HttpStatusCode.OK, jobResponse.StatusCode);
        var job = (await jobResponse.Content.ReadFromJsonAsync<JobDto>(BillingJson.Options))!;
        var templateResponse = await member.Send(HttpMethod.Post, "/api/panel-wizard/templates", new WizardTemplateRequest(job.Id, "Member template", "general", Enumerable.Range(1, 5).Select(x => "Synthetic question " + x).ToList(), "Welcome", "Goodbye"));
        Assert.Equal(HttpStatusCode.OK, templateResponse.StatusCode);
        var template = (await templateResponse.Content.ReadFromJsonAsync<TemplateDto>(BillingJson.Options))!;
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Get, $"/api/job-positions/{job.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Get, $"/api/interview-templates/{template.Id}")).StatusCode);
        await f.FundInterviewsAsync(owner);
        using var client = f.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", member.AccessToken);
        using var form = new MultipartFormDataContent();
        foreach (var field in new Dictionary<string, string> { ["jobPositionId"] = job.Id.ToString(), ["templateId"] = template.Id.ToString(), ["firstName"] = "Synthetic", ["lastName"] = "Candidate", ["email"] = "candidate@example.test", ["phone"] = Mobile() })
            form.Add(new StringContent(field.Value), field.Key);
        form.Add(new StringContent("Synthetic candidate work history and projects"), "resumeText");
        form.Add(new ByteArrayContent("%PDF-1.7\nSynthetic resume"u8.ToArray()), "resume", "resume.pdf");
        var sent = await client.PostAsync("/api/panel-wizard/invitations", form);
        if (!sent.IsSuccessStatusCode) Assert.Fail(await sent.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var interview = (await sent.Content.ReadFromJsonAsync<PanelInvitationResult>(BillingJson.Options))!.Invitation;
        Assert.Equal(HttpStatusCode.OK, (await member.Send(HttpMethod.Get, $"/api/interviews/{interview.InterviewId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Get, $"/api/interviews/{interview.InterviewId}")).StatusCode);
        using var candidate = f.CreateClient(); candidate.DefaultRequestHeaders.Authorization = new("Bearer", interview.Token);
        var start = await candidate.PostAsync($"/api/candidate/interviews/{interview.InterviewId}/start", null);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var state = (await start.Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        using var answer = new MultipartFormDataContent();
        answer.Add(new StringContent(state.Question!.Id.ToString()), "questionId");
        var audio = new ByteArrayContent(Convert.FromHexString("1A45DFA3000000000000000000000000"));
        audio.Headers.ContentType = new("audio/webm"); answer.Add(audio, "audio", "answer.webm");
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{interview.InterviewId}/answers", answer)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{interview.InterviewId}/complete", null)).StatusCode);
        await f.ProcessReportAsync(interview.InterviewId);
        var reportResponse = await member.Send(HttpMethod.Get, $"/api/interviews/{interview.InterviewId}/evaluation-report");
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);
        var report = (await reportResponse.Content.ReadFromJsonAsync<InterviewReportDto>())!;
        Assert.Equal("report-v3", report.PromptVersion);
        Assert.Equal(HttpStatusCode.OK, (await member.Send(HttpMethod.Get, $"/api/interviews/{interview.InterviewId}/evaluation-report/print")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Get, $"/api/interviews/{interview.InterviewId}/evaluation-report")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
        await f.SignInAsync(f.Browser(), mobile);
    }

    [PostgresFact]
    public async Task Deactivation_revokes_access_refresh_and_otp_and_old_access_stays_invalid_after_reinvite()
    {
        var owner = await f.RegisterOwner(); var mobile = Mobile(); var old = await Accept(await Invite(owner, mobile)); var me = await Me(old);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Post, $"/api/team/members/{me.Id}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await old.Send(HttpMethod.Get, "/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await old.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
        await f.Db(async db => { (await db.OtpChallenges.SingleAsync(x => x.Mobile == mobile)).CreatedAtUtc = f.Clock.GetUtcNow().AddMinutes(-2); await db.SaveChangesAsync(); });
        var b = f.Browser(); await b.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(mobile));
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Post, "/api/auth/otp/verify", new VerifyOtpRequest(mobile, f.PhoneOtp.Codes[mobile]))).StatusCode);
        var invitation = await Invite(owner, mobile);
        var accepted = await b.Send(HttpMethod.Post, $"/api/auth/team-invitations/{invitation.Id}/accept", new VerifyOtpRequest(mobile, f.PhoneOtp.Codes[mobile]));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        b.AccessToken = (await accepted.Content.ReadFromJsonAsync<AccessResponse>())!.AccessToken;
        Assert.Equal(me.Id, (await Me(b)).Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await old.Send(HttpMethod.Get, "/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Post, $"/api/team/members/{owner.Id}/deactivate")).StatusCode);
        var other = await f.RegisterOwner();
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Post, $"/api/team/members/{me.Id}/deactivate")).StatusCode);
    }

    [PostgresFact]
    public async Task Pending_invitation_requires_explicit_acceptance_same_mobile_otp_and_csrf_without_partial_signup()
    {
        var owner = await f.RegisterOwner(); var invitation = await Invite(owner, Mobile()); var b = f.Browser();
        await b.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(invitation.Mobile));
        var code = f.PhoneOtp.Codes[invitation.Mobile]; var request = new VerifyOtpRequest(invitation.Mobile, code);
        Assert.Equal(HttpStatusCode.Conflict, (await b.Send(HttpMethod.Post, "/api/auth/otp/verify", request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Send(HttpMethod.Post, $"/api/auth/team-invitations/{invitation.Id}/accept", request, csrf: false)).StatusCode);
        var wrong = code == "000000" ? "111111" : "000000";
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Post, $"/api/auth/team-invitations/{invitation.Id}/accept", new VerifyOtpRequest(invitation.Mobile, wrong))).StatusCode);
        var otherMobile = Mobile(); var other = f.Browser(); await other.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(otherMobile));
        Assert.Equal(HttpStatusCode.NotFound, (await other.Send(HttpMethod.Post, $"/api/auth/team-invitations/{invitation.Id}/accept", new VerifyOtpRequest(otherMobile, f.PhoneOtp.Codes[otherMobile]))).StatusCode);
        await f.Db(async db => { Assert.Equal(1, await db.Users.CountAsync()); Assert.Equal(1, await db.Organizations.CountAsync()); Assert.Null((await db.OtpChallenges.SingleAsync(x => x.Mobile == invitation.Mobile)).ConsumedAtUtc); });
        var response = await b.Send(HttpMethod.Post, $"/api/auth/team-invitations/{invitation.Id}/accept", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Post, $"/api/auth/team-invitations/{invitation.Id}/accept", request)).StatusCode);
        var list = (await (await owner.Client.Send(HttpMethod.Get, "/api/team/invitations")).Content.ReadFromJsonAsync<Page<TeamInvitationDto>>(BillingJson.Options))!;
        Assert.Equal("Accepted", Assert.Single(list.Items).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.Send(HttpMethod.Delete, $"/api/team/invitations/{invitation.Id}")).StatusCode);
    }

    [PostgresFact]
    public async Task Cancel_expiry_duplicate_and_existing_other_tenant_mobile_are_safe()
    {
        var owner = await f.RegisterOwner(); var other = await f.RegisterOwner(); var invitation = await Invite(owner, Mobile());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(invitation.Mobile, "نام", "آزمایشی"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(other.Mobile, "نام", "آزمایشی"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(owner.Mobile, "نام", "آزمایشی"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new { mobile = Mobile(), firstName = "نام", lastName = "آزمایشی", role = "Owner" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest("123", "نام", "آزمایشی"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Delete, $"/api/team/invitations/{invitation.Id}")).StatusCode);
        var otherInvites = (await (await other.Client.Send(HttpMethod.Get, "/api/team/invitations")).Content.ReadFromJsonAsync<Page<TeamInvitationDto>>(BillingJson.Options))!;
        Assert.Empty(otherInvites.Items);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Delete, $"/api/team/invitations/{invitation.Id}")).StatusCode);
        var b = f.Browser(); await b.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(invitation.Mobile));
        Assert.Equal(HttpStatusCode.Conflict, (await b.Send(HttpMethod.Post, $"/api/auth/team-invitations/{invitation.Id}/accept", new VerifyOtpRequest(invitation.Mobile, f.PhoneOtp.Codes[invitation.Mobile]))).StatusCode);
        var expired = await Invite(owner, Mobile());
        await f.Db(async db => { var row = await db.MobileTeamInvitations.SingleAsync(x => x.Id == expired.Id); row.CreatedAtUtc = f.Clock.GetUtcNow().AddDays(-4); row.ExpiresAtUtc = f.Clock.GetUtcNow().AddDays(-1); await db.SaveChangesAsync(); });
        var c = f.Browser(); await c.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(expired.Mobile));
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, $"/api/auth/team-invitations/{expired.Id}/accept", new VerifyOtpRequest(expired.Mobile, f.PhoneOtp.Codes[expired.Mobile]))).StatusCode);
        var fresh = await Invite(owner, expired.Mobile); Assert.NotEqual(expired.Id, fresh.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Browser().Send(HttpMethod.Get, "/api/team/members")).StatusCode);
    }

    [PostgresFact]
    public async Task Concurrent_invites_and_acceptance_have_only_one_winner()
    {
        var owner = await f.RegisterOwner(); var other = await f.RegisterOwner(); var mobile = Mobile();
        var results = await Task.WhenAll(owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(mobile, "نام", "آزمایشی")), other.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(mobile, "نام", "آزمایشی")));
        var winner = Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        var invitation = (await winner.Content.ReadFromJsonAsync<TeamInvitationDto>(BillingJson.Options))!;
        var b = f.Browser(); var c = f.Browser(); await b.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(mobile));
        var request = new VerifyOtpRequest(mobile, f.PhoneOtp.Codes[mobile]);
        var accepted = await Task.WhenAll(b.Send(HttpMethod.Post, $"/api/auth/team-invitations/{invitation.Id}/accept", request), c.Send(HttpMethod.Post, $"/api/auth/team-invitations/{invitation.Id}/accept", request));
        Assert.Single(accepted, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(accepted, x => x.StatusCode == HttpStatusCode.Unauthorized);
        await f.Db(async db => { Assert.Equal(1, await db.Users.CountAsync(x => x.PhoneNumber == mobile)); Assert.Equal(1, await db.MobileTeamInvitations.CountAsync(x => x.Mobile == mobile && x.AcceptedAtUtc != null)); });
    }

    [PostgresFact]
    public async Task Invitation_and_normal_signup_race_cannot_create_cross_tenant_membership()
    {
        var owner = await f.RegisterOwner(); var mobile = Mobile(); var browser = f.Browser();
        await browser.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(mobile));
        var results = await Task.WhenAll(owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(mobile, "نام", "آزمایشی")),
            browser.Send(HttpMethod.Post, "/api/auth/otp/verify", new VerifyOtpRequest(mobile, f.PhoneOtp.Codes[mobile])));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, results.Count(r => r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK));
        await f.Db(async db => {
            var account = await db.Users.SingleOrDefaultAsync(x => x.PhoneNumber == mobile);
            var invitation = await db.MobileTeamInvitations.SingleOrDefaultAsync(x => x.Mobile == mobile);
            Assert.True((account is null) != (invitation is null));
            if (account is not null) { Assert.Equal(OrganizationRole.Owner, account.Role); Assert.NotEqual((await Me(owner.Client)).OrganizationId, account.OrganizationId); }
            if (invitation is not null) Assert.Equal((await Me(owner.Client)).OrganizationId, invitation.OrganizationId);
        });
    }
    [PostgresFact]
    public async Task Organization_features_share_current_paid_and_trial_entitlements_without_financial_data()
    {
        var owner = await f.RegisterOwner(); var member = await Accept(await Invite(owner, Mobile()));
        var trialOwner = await f.RegisterOwner(signupTrial: true);
        var trialMember = await Accept(await Invite(trialOwner, Mobile()));
        var inactiveResponse = await member.Send(HttpMethod.Get, "/api/organization/features");
        Assert.Equal(HttpStatusCode.OK, inactiveResponse.StatusCode);
        var inactive = (await inactiveResponse.Content.ReadFromJsonAsync<OrganizationFeaturesDto>(BillingJson.Options))!;
        Assert.False(inactive.HasActiveSubscription); Assert.Empty(inactive.Features); Assert.Null(inactive.ExpiresAtUtc);
        await f.FundInterviewsAsync(owner);
        foreach (var pair in new[] { (Owner: owner, Member: member), (Owner: trialOwner, Member: trialMember) })
        {
            var response = await pair.Member.Send(HttpMethod.Get, "/api/organization/features?organizationId=" + Guid.NewGuid());
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
            var features = (await response.Content.ReadFromJsonAsync<OrganizationFeaturesDto>(BillingJson.Options))!;
            var subscription = (await (await pair.Owner.Client.Send(HttpMethod.Get, "/api/billing/subscription")).Content.ReadFromJsonAsync<HireYar.Application.Billing.SubscriptionOverviewDto>(BillingJson.Options))!.Current!;
            Assert.True(features.HasActiveSubscription);
            Assert.Equal(subscription.IsTrial, features.IsTrial); Assert.Equal(subscription.ExpiresAtUtc, features.ExpiresAtUtc);
            Assert.Equal(subscription.Features.Select(x => (x.Code, x.Name, x.DataType, x.Value)), features.Features.Select(x => (x.Code, x.Name, x.DataType, x.Value)));
            Assert.Contains(features.Features, x => x.Code == "technical_interview");
            Assert.Contains(features.Features, x => x.Code == "advanced_ai_workflow");
            var ownerFeatures = (await (await pair.Owner.Client.Send(HttpMethod.Get, "/api/organization/features")).Content.ReadFromJsonAsync<OrganizationFeaturesDto>(BillingJson.Options))!;
            Assert.Equal(features.Features, ownerFeatures.Features);
            var body = await (await pair.Member.Send(HttpMethod.Get, "/api/organization/features")).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(new[] { "expiresAtUtc", "features", "hasActiveSubscription", "isTrial", "startedAtUtc" }, body.EnumerateObject().Select(x => x.Name).OrderBy(x => x).ToArray());
            Assert.Equal(HttpStatusCode.Forbidden, (await pair.Member.Send(HttpMethod.Get, "/api/billing/subscription")).StatusCode);
        }
        Assert.False((await (await member.Send(HttpMethod.Get, "/api/organization/features")).Content.ReadFromJsonAsync<OrganizationFeaturesDto>(BillingJson.Options))!.IsTrial);
        Assert.True((await (await trialMember.Send(HttpMethod.Get, "/api/organization/features")).Content.ReadFromJsonAsync<OrganizationFeaturesDto>(BillingJson.Options))!.IsTrial);
        f.Clock.Offset = TimeSpan.FromDays(400);
        try
        {
            var expiredResponse = await member.Send(HttpMethod.Get, "/api/organization/features");
            Assert.Equal(HttpStatusCode.OK, expiredResponse.StatusCode);
            var expired = (await expiredResponse.Content.ReadFromJsonAsync<OrganizationFeaturesDto>(BillingJson.Options))!;
            Assert.False(expired.HasActiveSubscription); Assert.Empty(expired.Features); Assert.Null(expired.ExpiresAtUtc);
        }
        finally { f.Clock.Offset = TimeSpan.Zero; }
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Browser().Send(HttpMethod.Get, "/api/organization/features")).StatusCode);

    }
    [PostgresFact]
    public async Task Migration_keeps_one_active_owner_and_refuses_downgrade_with_team_history()
    {
        var owner = await f.RegisterOwner(); await Invite(owner, Mobile());
        await f.Db(async db => {
            Assert.False(db.Database.HasPendingModelChanges());
            var org = (await db.Users.SingleAsync(x => x.Id == owner.Id)).OrganizationId;
            db.Users.Add(new HireYar.Infrastructure.Auth.ApplicationUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString(), FullName = "Duplicate owner", OrganizationId = org, Role = OrganizationRole.Owner, IsActive = true });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)ex.InnerException!).SqlState);
            db.ChangeTracker.Clear();
            const string previous = "20261012193519_AddInterviewRecordingSegments";
            var blocked = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(previous));
            Assert.Equal(PostgresErrorCodes.RaiseException, blocked.SqlState);
            Assert.Equal(1, await db.MobileTeamInvitations.CountAsync());
        });
    }
}
