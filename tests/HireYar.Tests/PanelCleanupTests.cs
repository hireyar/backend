using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Auth;
using HireYar.Application.Billing;
using HireYar.Application.Common;
using HireYar.Application.InterviewTemplates;
using HireYar.Application.Interviews;
using HireYar.Application.JobPositions;
using HireYar.Domain.Billing;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class PanelCleanupTests(ApiFixture f)
{
    private async Task<(Account Owner, JobDto Job, TemplateDto Template)> Setup(bool funded = false)
    {
        var owner = await f.RegisterOwner();
        if (funded) await f.FundInterviewsAsync(owner);
        var jobResponse = await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/positions", new WizardPositionRequest("Synthetic cleanup", "Synthetic job"));
        Assert.Equal(HttpStatusCode.OK, jobResponse.StatusCode);
        var job = (await jobResponse.Content.ReadFromJsonAsync<JobDto>(BillingJson.Options))!;
        var templateResponse = await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates", new WizardTemplateRequest(job.Id, "Synthetic template", "general",
            Enumerable.Range(1, 5).Select(x => "Synthetic question " + x).ToList(), "Welcome", "Goodbye", 5));
        Assert.Equal(HttpStatusCode.OK, templateResponse.StatusCode);
        var template = (await templateResponse.Content.ReadFromJsonAsync<TemplateDto>(BillingJson.Options))!;
        Assert.All(template.Questions, question => Assert.Equal("ارزیابی شغلی", question.Competency));
        return (owner, job, template);
    }
    private async Task<HttpResponseMessage> Invite(Account owner, Guid job, Guid template)
    {
        using var hr = f.CreateClient(); hr.DefaultRequestHeaders.Authorization = new("Bearer", owner.Client.AccessToken);
        using var form = new MultipartFormDataContent();
        foreach (var field in new Dictionary<string, string> { ["jobPositionId"] = job.ToString(), ["templateId"] = template.ToString(),
            ["firstName"] = "Synthetic", ["lastName"] = "Candidate", ["email"] = "synthetic@example.test", ["phone"] = "09123456789" })
            form.Add(new StringContent(field.Value), field.Key);
        form.Add(new ByteArrayContent("%PDF-1.7\nSynthetic resume"u8.ToArray()), "resume", "resume.pdf");
        return await hr.PostAsync("/api/panel-wizard/invitations", form);
    }
    private async Task<PanelInvitationResult> Invitation(Account owner, Guid job, Guid template)
    {
        var response = await Invite(owner, job, template); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PanelInvitationResult>(BillingJson.Options))!;
    }
    private HttpClient Candidate(PanelInvitationResult invitation)
    {
        var client = f.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invitation.Invitation.Token); return client;
    }

    [PostgresFact]
    public async Task Removed_email_and_team_routes_are_absent_from_runtime_and_persian_openapi()
    {
        var owner = await f.RegisterOwner();
        var routes = new[] { "/api/auth/register", "/api/auth/login", "/api/auth/confirm-email", "/api/auth/resend-confirmation",
            "/api/auth/forgot-password", "/api/auth/reset-password", "/api/auth/change-password", "/api/auth/invitations/accept", "/api/members/invitations" };
        foreach (var route in routes) Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.Send(HttpMethod.Post, route, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.Send(HttpMethod.Get, "/api/members")).StatusCode);
        var doc = await (await owner.Client.Send(HttpMethod.Get, "/openapi/v1.json")).Content.ReadFromJsonAsync<JsonElement>();
        var paths = doc.GetProperty("paths"); foreach (var route in routes) Assert.False(paths.TryGetProperty(route, out _));
        foreach (var path in new[] { "/api/job-positions/{id}", "/api/interview-templates/{id}", "/api/interviews/{id}" })
            Assert.Contains("حذف", paths.GetProperty(path).GetProperty("delete").GetProperty("summary").GetString());
    }

    [PostgresFact]
    public async Task Single_active_owner_is_enforced_by_postgres_and_inactive_account_cannot_enter()
    {
        var owner = await f.RegisterOwner(); Guid org = default;
        await f.Db(async db => org = (await db.Users.FindAsync(owner.Id))!.OrganizationId);
        foreach (var role in new[] { OrganizationRole.Owner, OrganizationRole.Recruiter })
        {
            await f.Db(async db =>
            {
                db.Users.Add(new ApplicationUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString(), FullName = "Synthetic", OrganizationId = org,
                    Role = role, IsActive = true, PhoneNumberConfirmed = true, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
                var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.Equal(role == OrganizationRole.Owner ? PostgresErrorCodes.UniqueViolation : PostgresErrorCodes.CheckViolation, ((PostgresException)error.InnerException!).SqlState);
            });
        }
        await f.Db(async db => { (await db.Users.FindAsync(owner.Id))!.IsActive = false; await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.Client.Send(HttpMethod.Get, "/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
        var browser = f.Browser(); await browser.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest("09999999999"));
        await f.Db(async db =>
        {
            // A legacy recruiter is retained for history, but can never get an OTP session.
            db.Users.Add(new ApplicationUser { Id = Guid.NewGuid(), UserName = "09999999999", PhoneNumber = "09999999999", FullName = "Synthetic legacy",
                OrganizationId = org, Role = OrganizationRole.Recruiter, IsActive = false, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Send(HttpMethod.Post, "/api/auth/otp/verify", new VerifyOtpRequest("09999999999", f.PhoneOtp.Codes["09999999999"]))).StatusCode);
    }

    [PostgresFact]
    public async Task Email_confirmation_alone_does_not_authorize_access_or_refresh()
    {
        var owner = await f.RegisterOwner();
        await f.Db(async db => { var user = (await db.Users.FindAsync(owner.Id))!; user.EmailConfirmed = true; user.PhoneNumberConfirmed = false; await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.Client.Send(HttpMethod.Get, "/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
        await f.SignInAsync(f.Browser(), owner.Mobile);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Get, "/api/auth/me")).StatusCode);
    }

    [PostgresFact]
    public async Task Deleting_position_hides_its_templates_and_prevents_reuse_without_destroying_history()
    {
        var (owner, job, template) = await Setup(true); var invitation = await Invitation(owner, job.Id, template.Id); var stranger = await f.RegisterOwner();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Delete, $"/api/job-positions/{job.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Delete, $"/api/job-positions/{job.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.Send(HttpMethod.Get, $"/api/job-positions/{job.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.Send(HttpMethod.Delete, $"/api/job-positions/{job.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.Send(HttpMethod.Get, $"/api/interview-templates/{template.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Invite(owner, job.Id, template.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.Send(HttpMethod.Put, $"/api/panel-wizard/positions/{job.Id}", new { position = new WizardPositionRequest("Restore", "No"), rowVersion = job.RowVersion })).StatusCode);
        var jobs = await (await owner.Client.Send(HttpMethod.Get, "/api/job-positions")).Content.ReadFromJsonAsync<Page<JobDto>>(); Assert.Empty(jobs!.Items);
        var templates = await (await owner.Client.Send(HttpMethod.Get, "/api/interview-templates")).Content.ReadFromJsonAsync<Page<TemplateListDto>>(); Assert.Empty(templates!.Items);
        using var candidate = Candidate(invitation);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{invitation.Invitation.InterviewId}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Get, $"/api/interviews/{invitation.Invitation.InterviewId}")).StatusCode);
        await f.Db(async db =>
        {
            Assert.NotNull((await db.JobPositions.FindAsync(job.Id))!.DeletedAtUtc); Assert.NotNull((await db.InterviewTemplates.FindAsync(template.Id))!.DeletedAtUtc);
            var session = await db.InterviewSessions.SingleAsync(x => x.Id == invitation.Invitation.InterviewId);
            Assert.Equal("ارزیابی شغلی", Assert.Single(session.Skills).Name);
        });
    }

    [PostgresFact]
    public async Task Template_deletion_is_scoped_and_snapshot_invitation_survives()
    {
        var (owner, job, template) = await Setup(true); var invitation = await Invitation(owner, job.Id, template.Id); var stranger = await f.RegisterOwner();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Delete, $"/api/interview-templates/{template.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Delete, $"/api/interview-templates/{template.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Invite(owner, job.Id, template.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.Send(HttpMethod.Put, $"/api/panel-wizard/templates/{template.Id}", new UpdateWizardTemplateRequest(
            new(job.Id, "Restore", "general", Enumerable.Range(1, 5).Select(x => "Question " + x).ToList(), "Hello", "Bye"), template.RowVersion))).StatusCode);
        using var candidate = Candidate(invitation);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{invitation.Invitation.InterviewId}/start", null)).StatusCode);
        var history = await (await owner.Client.Send(HttpMethod.Get, "/api/interviews")).Content.ReadFromJsonAsync<Page<PanelInterviewDto>>(); Assert.Single(history!.Items);
    }

    [PostgresFact]
    public async Task Deleting_unstarted_invitation_hides_it_and_revokes_candidate_access()
    {
        var (owner, job, template) = await Setup(true); var invitation = await Invitation(owner, job.Id, template.Id); var id = invitation.Invitation.InterviewId;
        var stranger = await f.RegisterOwner();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Delete, $"/api/interviews/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Delete, $"/api/interviews/{id}")).StatusCode);
        foreach (var suffix in new[] { "", "/resume" }) Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.Send(HttpMethod.Get, $"/api/interviews/{id}{suffix}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.Send(HttpMethod.Post, $"/api/interviews/{id}/resend", new ResendInterviewRequest())).StatusCode);
        using var candidate = Candidate(invitation); Assert.Equal(HttpStatusCode.Unauthorized, (await candidate.PostAsync($"/api/candidate/interviews/{id}/start", null)).StatusCode);
        var dashboard = await (await owner.Client.Send(HttpMethod.Get, "/api/dashboard")).Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(0, dashboard.GetProperty("pendingInterviews").GetInt32()); Assert.Empty(dashboard.GetProperty("recentInterviews").EnumerateArray());
        await f.Db(async db => { var session = await db.InterviewSessions.SingleAsync(x => x.Id == id); Assert.NotNull(session.DeletedAtUtc); Assert.NotNull(session.AccessRevokedAtUtc); Assert.True(await db.CandidateApplications.AnyAsync(x => x.Id == session.CandidateApplicationId)); });
    }

    private async Task TestBilledDeletion(bool completeFirst)
    {
        f.InterviewAi.Reset(); var (owner, job, template) = await Setup(true); var invitation = await Invitation(owner, job.Id, template.Id); var id = invitation.Invitation.InterviewId;
        using var candidate = Candidate(invitation);
        var start = await candidate.PostAsync($"/api/candidate/interviews/{id}/start", null); Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var current = (await start.Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        f.Clock.Offset = TimeSpan.FromSeconds(20);
        try
        {
            using var answer = new MultipartFormDataContent(); answer.Add(new StringContent(current.Question!.Id.ToString()), "questionId");
            var audio = new ByteArrayContent(new byte[] { 0x1a, 0x45, 0xdf, 0xa3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }); audio.Headers.ContentType = new("audio/webm"); answer.Add(audio, "audio", "answer.webm");
            Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{id}/answers", answer)).StatusCode);
            if (completeFirst) Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{id}/complete", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Delete, $"/api/interviews/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.Send(HttpMethod.Delete, $"/api/interviews/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await candidate.GetAsync($"/api/candidate/interviews/{id}/current")).StatusCode);
            var panel = (await (await owner.Client.Send(HttpMethod.Get, "/api/billing/panel")).Content.ReadFromJsonAsync<BillingPanelDto>(BillingJson.Options))!; Assert.Equal(0, panel.Wallet.ReservedCredit);
            var history = (await (await owner.Client.Send(HttpMethod.Get, "/api/billing/financial-history?transactionType=usage")).Content.ReadFromJsonAsync<FinancialHistoryDto>(BillingJson.Options))!;
            var usage = Assert.Single(history.Transactions.Items); Assert.Equal(id, usage.InterviewId); Assert.True(usage.BillableSeconds >= 20); Assert.Equal("Synthetic Candidate", usage.CandidateName);
            await f.Db(async db =>
            {
                var bill = await db.InterviewBillings.SingleAsync(x => x.InterviewSessionId == id);
                Assert.Equal(BillingMath.Cost(bill.BillableSeconds, bill.RatePerMinuteUsd), bill.BilledAmountUsd);
                Assert.Equal(InterviewBillingStatus.Settled, bill.Status);
                Assert.Equal(1, await db.AiCreditTransactions.CountAsync(x => x.ReferenceType == "Interview" && x.ReferenceId == id));
                Assert.True(await db.InterviewAnswers.AnyAsync(x => x.InterviewSessionId == id));
            });
        }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
    [PostgresFact] public Task Active_interview_deletion_releases_reservation_and_bills_persisted_answers_once() => TestBilledDeletion(false);
    [PostgresFact] public Task Completed_interview_deletion_preserves_immutable_settlement() => TestBilledDeletion(true);

    [PostgresFact]
    public async Task Deletion_during_ai_processing_revokes_the_worker_lease_and_prevents_late_evidence()
    {
        f.InterviewAi.Reset(); var (owner, job, template) = await Setup(true); var invitation = await Invitation(owner, job.Id, template.Id); var id = invitation.Invitation.InterviewId;
        using var candidate = Candidate(invitation);
        var current = (await (await candidate.PostAsync($"/api/candidate/interviews/{id}/start", null)).Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.InterviewAi.Entered = entered; f.InterviewAi.Release = release;
        using var form = new MultipartFormDataContent(); form.Add(new StringContent(current.Question!.Id.ToString()), "questionId");
        var audio = new ByteArrayContent(new byte[] { 0x1a, 0x45, 0xdf, 0xa3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }); audio.Headers.ContentType = new("audio/webm"); form.Add(audio, "audio", "answer.webm");
        var pending = candidate.PostAsync($"/api/candidate/interviews/{id}/answers", form);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Delete, $"/api/interviews/{id}")).StatusCode);
            release.TrySetResult(); Assert.Equal(HttpStatusCode.Unauthorized, (await pending).StatusCode);
            await f.Db(async db =>
            {
                var session = await db.InterviewSessions.SingleAsync(x => x.Id == id);
                Assert.Null(session.ProcessingLeaseId); Assert.NotNull(session.DeletedAtUtc); Assert.Empty(session.Evidence);
                Assert.False(await db.AiCreditReservations.AnyAsync(x => x.InterviewSessionId == id && x.Status == ReservationStatus.Active));
            });
        }
        finally { release.TrySetResult(); f.InterviewAi.Reset(); await pending; }
    }

    [PostgresFact]
    public async Task Concurrent_template_removal_and_invitation_never_allow_subsequent_reuse()
    {
        var (owner, job, template) = await Setup(true);
        var results = await Task.WhenAll(Invite(owner, job.Id, template.Id), owner.Client.Send(HttpMethod.Delete, $"/api/interview-templates/{template.Id}"));
        Assert.Contains(results[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }); Assert.Equal(HttpStatusCode.NoContent, results[1].StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Invite(owner, job.Id, template.Id)).StatusCode);
        await f.Db(async db => Assert.Equal(results[0].StatusCode == HttpStatusCode.OK ? 1 : 0, await db.InterviewSessions.CountAsync(x => x.JobPositionId == job.Id)));
    }
}
