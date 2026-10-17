using HireYar.Api.Billing;
using System.Net;
using HireYar.Domain.Interviews;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Application.InterviewTemplates;
using HireYar.Application.JobPositions;
using HireYar.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed partial class PanelWizardTests(ApiFixture f)
{
    private static WizardTemplateRequest Template(Guid jobId, int count = 5) => new(jobId, "قالب آزمایشی", "general",
        Enumerable.Range(1, count).Select(x => $"سؤال مصنوعی {x}").ToList(), "شروع آزمایشی", "پایان آزمایشی");
    private async Task<(Account Hr, JobDto Job, TemplateDto Template)> Setup()
    {
        var hr = await f.RegisterOwner(); await f.FundInterviewsAsync(hr);
        var jobResponse = await hr.Client.Send(HttpMethod.Post, "/api/panel-wizard/positions", new WizardPositionRequest("موقعیت مصنوعی", "شرح شغل مصنوعی"));
        Assert.Equal(HttpStatusCode.OK, jobResponse.StatusCode);
        var job = (await jobResponse.Content.ReadFromJsonAsync<JobDto>(BillingJson.Options))!;
        var templateResponse = await hr.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates", Template(job.Id));
        Assert.Equal(HttpStatusCode.OK, templateResponse.StatusCode);
        return (hr, job, (await templateResponse.Content.ReadFromJsonAsync<TemplateDto>(BillingJson.Options))!);
    }
    private async Task<HttpResponseMessage> Invite(Account hr, Guid job, Guid template, string phone = "09123456789", string filename = "resume.pdf", byte[]? bytes = null, string interviewType = "general", bool sendEmail = false, bool sendSms = false)
    {
        using var client = f.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", hr.Client.AccessToken);
        using var form = new MultipartFormDataContent();
        foreach (var field in new Dictionary<string, string> { ["jobPositionId"] = job.ToString(), ["templateId"] = template.ToString(),
            ["firstName"] = "Synthetic", ["lastName"] = "Candidate", ["email"] = "candidate@example.test", ["phone"] = phone, ["deadlineDays"] = "3", ["interviewType"] = interviewType })
            form.Add(new StringContent(field.Value), field.Key);
        form.Add(new StringContent("Synthetic candidate work history and projects"), "resumeText");
        form.Add(new StringContent(sendEmail.ToString()), "sendEmail");
        form.Add(new StringContent(sendSms.ToString()), "sendSms");
        form.Add(new ByteArrayContent(bytes ?? "%PDF-1.7\nSynthetic test document"u8.ToArray()), "resume", filename);
        return await client.PostAsync("/api/panel-wizard/invitations", form);
    }
    [PostgresFact]
    public async Task Introductions_context_and_clarification_persist_and_company_is_only_exposed_after_questions()
    {
        var (hr, job, original) = await Setup(); f.InterviewAi.Reset();
        var changed = Template(job.Id) with { CandidateIntroductionEnabled = true, CompanyIntroductionEnabled = true, CompanyIntroductionMessage = "Synthetic company introduction" };
        var saved = await hr.Client.Send(HttpMethod.Put, $"/api/panel-wizard/templates/{original.Id}", new UpdateWizardTemplateRequest(changed, original.RowVersion));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var invite = (await (await Invite(hr, job.Id, original.Id)).Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        using var c = f.CreateClient(); c.DefaultRequestHeaders.Authorization = new("Bearer", invite.Token);
        var route = $"/api/candidate/interviews/{invite.InterviewId}";
        var start = (await (await c.PostAsync(route + "/start", null)).Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        Assert.Equal("Welcome", start.Stage); Assert.Null(start.Question); Assert.False(start.CanRecord); Assert.Null(start.GoodbyeMessage);
        var intro = (await (await c.PostAsJsonAsync(route + "/stage/advance", new AdvanceInterviewStageRequest("Welcome"))).Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        Assert.Equal("CandidateIntroduction", intro.Stage); Assert.Equal(changed.CandidateIntroductionMessage, intro.StageMessage);
        var repeated = (await (await c.PostAsJsonAsync(route + "/stage/advance", new AdvanceInterviewStageRequest("Welcome"))).Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        Assert.Equal("CandidateIntroduction", repeated.Stage);
        var current = (await (await c.PostAsJsonAsync(route + "/stage/advance", new AdvanceInterviewStageRequest("CandidateIntroduction", "Synthetic introduction history"))).Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        Assert.Equal("Questions", current.Stage);
        async Task<CandidateInterviewDto> Submit(CandidateInterviewDto state)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(state.Question!.Id.ToString()), "questionId");
            form.Add(new StringContent(Guid.NewGuid().ToString("N")), "clientTurnId");
            var audio = new ByteArrayContent("ID3-synthetic-answer"u8.ToArray()); audio.Headers.ContentType = new("audio/mpeg"); form.Add(audio, "audio", "answer.mp3");
            var response = await c.PostAsync(route + "/answers", form); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        }
        try
        {
            f.InterviewAi.InteractionAction = "clarify";
            var clarified = await Submit(current);
            Assert.Equal(current.Question!.Id, clarified.Question!.Id); Assert.NotNull(clarified.Question.InteractionMessage); Assert.Equal(0, clarified.Progress.AnsweredQuestions);
            f.InterviewAi.InteractionAction = null; current = await Submit(clarified);
            var context = f.InterviewAi.LastContext!;
            Assert.Equal(job.Description, context.PositionDescription); Assert.Contains("work history", context.ResumeText);
            Assert.Equal("Synthetic introduction history", context.CandidateIntroduction); Assert.Equal("general", context.InterviewType);
            Assert.Equal(InterviewDecisionPrompt.Master, context.MasterPrompt);
            Assert.Equal(context.AvailableQuestions.Select(q => q.QuestionId).Append(context.CurrentQuestionId).ToHashSet(),
                context.QuestionBank!.Select(q => q.QuestionId).ToHashSet());
            Assert.Equal(2, context.QuestionBank!.Count);
            Assert.Single(context.PreviousAnswers!); Assert.NotNull(context.PreviousAnswers![0].AssistantReply);
            Assert.Equal("سؤال مصنوعی 2", current.Question!.Text);
            for (var i = 1; i < 5; i++) current = await Submit(current);
            Assert.Equal("Completed", current.Status); Assert.Equal("CompanyIntroduction", current.Stage); Assert.Equal(changed.CompanyIntroductionMessage, current.StageMessage);
            var goodbye = (await (await c.PostAsJsonAsync(route + "/stage/advance", new AdvanceInterviewStageRequest("CompanyIntroduction"))).Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
            Assert.Equal("Goodbye", goodbye.Stage); Assert.Equal(changed.GoodbyeMessage, goodbye.StageMessage);
            await f.Db(async db => { var s = await db.InterviewSessions.Include(s => s.Answers).SingleAsync(s => s.Id == invite.InterviewId);
                Assert.Equal(5, s.Questions.Count); Assert.Equal(5, s.AnsweredQuestionCount); Assert.Equal(6, s.Answers.Count); Assert.Single(s.Answers, a => a.IsInteraction);
                Assert.Equal(InterviewDecisionPrompt.Master, s.MasterPromptSnapshot); Assert.Equal(InterviewDecisionPrompt.Version, s.MasterPromptVersion); Assert.False(db.Database.HasPendingModelChanges()); });
        }
        finally { f.InterviewAi.Reset(); }
    }
    [PostgresFact]
    public async Task Wizard_uses_template_questions_snapshots_resume_and_dashboard_without_skill_setup()
    {
        var (hr, job, template) = await Setup();
        var response = await Invite(hr, job.Id, template.Id); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var issued = (await response.Content.ReadFromJsonAsync<PanelInvitationResult>())!;
        Assert.Equal("NotConfigured", issued.EmailDelivery); Assert.Equal("NotConfigured", issued.SmsDelivery);
        var id = issued.Invitation.InterviewId;
        var report = await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{id}")).Content.ReadFromJsonAsync<HrInterviewDto>();
        Assert.Equal(5, report!.Questions.Count); Assert.Contains(report.Questions, x => x.Text == "سؤال مصنوعی 1");
        var resume = await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{id}/resume");
        Assert.Equal(HttpStatusCode.OK, resume.StatusCode); Assert.Equal("application/pdf", resume.Content.Headers.ContentType!.MediaType);
        var list = await (await hr.Client.Send(HttpMethod.Get, "/api/interviews?statuses=pending&search=Synthetic")).Content.ReadFromJsonAsync<Page<PanelInterviewDto>>();
        var row = Assert.Single(list!.Items); Assert.Equal(id, row.Id); Assert.Equal(3, row.DeadlineDays); Assert.Null(row.EvaluationPercent); Assert.False(row.VideoAvailable);
        var dashboard = await (await hr.Client.Send(HttpMethod.Get, "/api/dashboard")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, dashboard.GetProperty("pendingInterviews").GetInt32());
        var changed = Template(job.Id) with { WelcomeMessage = "پیام تغییر یافته", Questions = Enumerable.Repeat("سؤال ویرایش شده", 5).ToList() };
        Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Put, $"/api/panel-wizard/templates/{template.Id}", new UpdateWizardTemplateRequest(changed, template.RowVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await hr.Client.Send(HttpMethod.Put, $"/api/panel-wizard/templates/{template.Id}", new UpdateWizardTemplateRequest(changed, template.RowVersion))).StatusCode);
        using var candidate = f.CreateClient(); candidate.DefaultRequestHeaders.Authorization = new("Bearer", issued.Invitation.Token);
        var start = await candidate.PostAsync($"/api/candidate/interviews/{id}/start", null);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var current = (await start.Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        Assert.Equal("شروع آزمایشی", current.WelcomeMessage); Assert.Equal("سؤال مصنوعی 1", current.Question!.Text);
        var replaced = await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{id}/resend", new ResendInterviewRequest());
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        var fresh = (await replaced.Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        Assert.NotEqual(id, fresh.InterviewId);
        Assert.Equal(HttpStatusCode.Gone, (await candidate.GetAsync($"/api/candidate/interviews/{id}/current")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{fresh.InterviewId}/cancel")).StatusCode);
    }
    [PostgresFact]
    public async Task Wizard_validates_fields_files_scope_and_reuses_organization_template_by_copy()
    {
        var (hr, job, template) = await Setup(); var stranger = await f.RegisterOwner();
        Assert.Equal(HttpStatusCode.BadRequest, (await hr.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates", Template(job.Id, 4))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Invite(hr, job.Id, template.Id, phone: "123")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Invite(hr, job.Id, template.Id, interviewType: "unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Invite(hr, job.Id, template.Id, filename: "resume.pdf", bytes: "not-pdf"u8.ToArray())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Invite(stranger, job.Id, template.Id)).StatusCode);
        var empty = await (await hr.Client.Send(HttpMethod.Get, "/api/interviews")).Content.ReadFromJsonAsync<Page<PanelInterviewDto>>(); Assert.Empty(empty!.Items);
        var second = (await (await hr.Client.Send(HttpMethod.Post, "/api/panel-wizard/positions", new WizardPositionRequest("دوم", "شرح دوم"))).Content.ReadFromJsonAsync<JobDto>(BillingJson.Options))!;
        var result = await Invite(hr, second.Id, template.Id); Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var issued = (await result.Content.ReadFromJsonAsync<PanelInvitationResult>())!;
        await f.Db(async db => { var s = await db.InterviewSessions.SingleAsync(x => x.Id == issued.Invitation.InterviewId); Assert.NotEqual(template.Id, s.InterviewTemplateId); Assert.Equal(second.Id, s.JobPositionId); Assert.Equal("شرح دوم", s.PositionDescriptionSnapshot); });
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Get, $"/api/interviews/{issued.Invitation.InterviewId}/resume")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await hr.Client.Send(HttpMethod.Get, "/api/interviews?statuses=unknown")).StatusCode);
    }
    [PostgresFact]
    public async Task Invitation_type_is_independent_of_bank_and_existing_capabilities_are_enforced()
    {
        var hr = await f.RegisterOwner();
        var job = (await (await hr.Client.Send(HttpMethod.Post, "/api/panel-wizard/positions", new WizardPositionRequest("مصنوعی", "شرح مصنوعی"))).Content.ReadFromJsonAsync<JobDto>(BillingJson.Options))!;
        var template = (await (await hr.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates", Template(job.Id) with { InterviewType = "specialized" })).Content.ReadFromJsonAsync<TemplateDto>(BillingJson.Options))!;
        Assert.Equal(HttpStatusCode.Forbidden, (await Invite(hr, job.Id, template.Id)).StatusCode);
        var packages = (await (await hr.Client.Send(HttpMethod.Get, "/api/pricing/packages")).Content.ReadFromJsonAsync<PricingResponse>(BillingJson.Options))!;
        var period = packages.Packages.Single(x => x.Code == "basic").Periods.Single(x => x.Months == 1);
        var purchase = (await (await hr.Client.Send(HttpMethod.Post, "/api/billing/subscription/purchases", new HireYar.Application.Billing.SubscriptionPurchaseRequest(period.Id))).Content.ReadFromJsonAsync<HireYar.Api.Billing.RialPurchaseResult>(BillingJson.Options))!;
        Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Post, $"/api/billing/payments/{purchase.Payment.Id}/verify")).StatusCode);
        var general = await Invite(hr, job.Id, template.Id); Assert.Equal(HttpStatusCode.OK, general.StatusCode);
        var issued = (await general.Content.ReadFromJsonAsync<PanelInvitationResult>())!;
        await f.Db(async db => Assert.Equal("hr_interview", (await db.InterviewSessions.SingleAsync(x => x.Id == issued.Invitation.InterviewId)).RequiredCapabilityCode));
        Assert.Equal(HttpStatusCode.Forbidden, (await Invite(hr, job.Id, template.Id, interviewType: "specialized")).StatusCode);
        var rows = (await (await hr.Client.Send(HttpMethod.Get, "/api/interviews")).Content.ReadFromJsonAsync<Page<PanelInterviewDto>>())!;
        Assert.Equal("general", Assert.Single(rows.Items).InterviewType);
    }
    [PostgresFact]
    public async Task Short_access_code_and_link_are_recoverable_only_in_the_owning_organization_panel()
    {
        var (hr, job, template) = await Setup(); var response = await Invite(hr, job.Id, template.Id);
        var issued = (await response.Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        Assert.Matches("^[0-9]{6}$", issued.AccessCode!); Assert.Equal(issued.AccessCode, issued.Token);
        Assert.EndsWith($"/interview/{issued.InterviewId}", issued.InterviewUrl); Assert.DoesNotContain(issued.Token, issued.InterviewUrl!);
        var accessResponse = await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{issued.InterviewId}/access");
        Assert.Equal(HttpStatusCode.OK, accessResponse.StatusCode); Assert.True(accessResponse.Headers.CacheControl!.NoStore);
        var access = (await accessResponse.Content.ReadFromJsonAsync<InterviewAccessDetails>())!;
        Assert.Equal(issued.AccessCode, access.AccessCode); Assert.Equal(issued.InterviewUrl, access.InterviewUrl);
        var listResponse = await hr.Client.Send(HttpMethod.Get, "/api/interviews"); Assert.True(listResponse.Headers.CacheControl!.NoStore);
        var row = Assert.Single((await listResponse.Content.ReadFromJsonAsync<Page<PanelInterviewDto>>())!.Items);
        Assert.Equal(issued.AccessCode, row.AccessCode); Assert.Equal(issued.InterviewUrl, row.InterviewUrl);
        var stranger = await f.RegisterOwner();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Get, $"/api/interviews/{issued.InterviewId}/access")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Post, $"/api/interviews/{issued.InterviewId}/resend", new ResendInterviewRequest())).StatusCode);
        await f.Db(async db => {
            var s = await db.InterviewSessions.SingleAsync(s => s.Id == issued.InterviewId);
            Assert.NotEqual(issued.Token, s.ProtectedAccessCode); Assert.NotEqual(InterviewTokens.Hash(issued.Token), s.AccessTokenHash);
            Assert.Equal(InterviewTokens.Hash(s.ProtectedAccessCode!), s.AccessTokenHash); Assert.False(db.Database.HasPendingModelChanges());
        });
    }

    [PostgresFact]
    public async Task Completed_invitation_shows_terminal_message_and_reinvite_creates_clean_session_with_same_snapshots()
    {
        var (hr, job, template) = await Setup(); var issued = (await (await Invite(hr, job.Id, template.Id)).Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        using var c = f.CreateClient(); c.DefaultRequestHeaders.Authorization = new("Bearer", issued.Token);
        var route = $"/api/candidate/interviews/{issued.InterviewId}";
        var start = (await (await c.PostAsync(route + "/start", null)).Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        if (start.Stage == "Welcome") await c.PostAsJsonAsync(route + "/stage/advance", new AdvanceInterviewStageRequest("Welcome"));
        var completed = await c.PostAsync(route + "/complete", null); Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var end = (await completed.Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        var old = await c.GetAsync(route + "/current"); Assert.Equal(HttpStatusCode.Gone, old.StatusCode);
        Assert.Equal("interview_completed", (await old.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Gone, (await c.PostAsync(route + "/start", null)).StatusCode);
        // An already displayed goodbye can still finish; reopening the completed link cannot resume the interview.
        if (end.Stage == "Goodbye") Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync(route + "/stage/advance", new AdvanceInterviewStageRequest("Goodbye"))).StatusCode);
        using var anonymous = f.CreateClient();
        var publicState = (await (await anonymous.GetAsync(route + "/access-status")).Content.ReadFromJsonAsync<InterviewAccessStatus>())!;
        Assert.Equal("completed", publicState.Status); Assert.Contains("اتمام", publicState.Message);
        var resent = await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{issued.InterviewId}/resend", new ResendInterviewRequest());
        Assert.Equal(HttpStatusCode.OK, resent.StatusCode); var fresh = (await resent.Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        Assert.NotEqual(issued.InterviewId, fresh.InterviewId); Assert.NotEqual(issued.Token, fresh.Token);
        Assert.Equal("changed", (await (await anonymous.GetAsync(route + "/access-status")).Content.ReadFromJsonAsync<InterviewAccessStatus>())!.Status);
        await f.Db(async db => {
            var previous = await db.InterviewSessions.SingleAsync(s => s.Id == issued.InterviewId);
            var next = await db.InterviewSessions.Include(s => s.Answers).SingleAsync(s => s.Id == fresh.InterviewId);
            Assert.Equal(InterviewStatus.Completed, previous.Status); Assert.Equal(fresh.InterviewId, previous.SupersededByInterviewId);
            Assert.Equal(InterviewStatus.Created, next.Status); Assert.Null(next.StartedAtUtc); Assert.Null(next.CandidateIntroductionTranscript);
            Assert.Empty(next.Answers); Assert.Empty(next.Evidence); Assert.All(next.Questions, q => Assert.Null(q.AskedAtUtc));
            // EF navigation materialization order is not the persisted question order.
            Assert.Equal(previous.Questions.OrderBy(q => q.Order).Select(q => (q.Order, q.Text)),
                next.Questions.OrderBy(q => q.Order).Select(q => (q.Order, q.Text)));
            Assert.Equal(previous.ResumeTextSnapshot, next.ResumeTextSnapshot);
        });
    }

    [PostgresFact]
    public async Task Five_bad_code_attempts_lock_that_invitation_and_expiry_allows_valid_code_again()
    {
        var (hr, job, template) = await Setup(); var issued = (await (await Invite(hr, job.Id, template.Id)).Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        using var c = f.CreateClient(); c.DefaultRequestHeaders.Authorization = new("Bearer", issued.Token == "000000" ? "000001" : "000000");
        var route = $"/api/candidate/interviews/{issued.InterviewId}/current";
        for (var attempt = 1; attempt <= 5; attempt++)
            Assert.Equal(attempt < 5 ? HttpStatusCode.Unauthorized : HttpStatusCode.TooManyRequests, (await c.GetAsync(route)).StatusCode);
        c.DefaultRequestHeaders.Authorization = new("Bearer", issued.Token);
        var locked = await c.GetAsync(route); Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        Assert.Equal("interview_access_locked", (await locked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var offset = f.Clock.Offset;
        try { f.Clock.Offset += TimeSpan.FromMinutes(16); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(route)).StatusCode); }
        finally { f.Clock.Offset = offset; }
        await f.Db(async db => Assert.Equal(5, (await db.InterviewSessions.SingleAsync(s => s.Id == issued.InterviewId)).AccessFailedAttempts));
    }

    [PostgresFact]
    public async Task Concurrent_reinvitation_creates_exactly_one_replacement_and_never_reuses_old_url()
    {
        var (hr, job, template) = await Setup(); var issued = (await (await Invite(hr, job.Id, template.Id)).Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        var requests = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => hr.Client.Send(HttpMethod.Post,
            $"/api/interviews/{issued.InterviewId}/resend", new ResendInterviewRequest())));
        Assert.Single(requests, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(requests, r => r.StatusCode == HttpStatusCode.Conflict);
        var fresh = (await requests.Single(r => r.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        Assert.NotEqual(issued.InterviewUrl, fresh.InterviewUrl);
        await f.Db(async db => {
            var old = await db.InterviewSessions.SingleAsync(s => s.Id == issued.InterviewId);
            Assert.Equal(fresh.InterviewId, old.SupersededByInterviewId);
            Assert.Equal(2, await db.InterviewSessions.CountAsync(s => s.CandidateApplicationId == old.CandidateApplicationId));
        });
    }

    [PostgresFact]
    public async Task Legacy_hash_only_invitation_remains_usable_and_reinvite_upgrades_it_to_short_code()
    {
        var (hr, job, template) = await Setup(); var issued = (await (await Invite(hr, job.Id, template.Id)).Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        var legacyToken = InterviewTokens.Create();
        await f.Db(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"InterviewSessions\" SET \"ProtectedAccessCode\" = NULL, \"AccessTokenHash\" = {InterviewTokens.Hash(legacyToken)} WHERE \"Id\" = {issued.InterviewId}"));
        using var c = f.CreateClient(); c.DefaultRequestHeaders.Authorization = new("Bearer", legacyToken);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/candidate/interviews/{issued.InterviewId}/current")).StatusCode);
        var access = (await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{issued.InterviewId}/access")).Content.ReadFromJsonAsync<InterviewAccessDetails>())!;
        Assert.Null(access.AccessCode);
        var replacement = (await (await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{issued.InterviewId}/resend", new ResendInterviewRequest())).Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        Assert.Matches("^[0-9]{6}$", replacement.Token);
        Assert.Equal(HttpStatusCode.Gone, (await c.GetAsync($"/api/candidate/interviews/{issued.InterviewId}/current")).StatusCode);
    }

    [PostgresFact]
    public async Task Resend_rotates_token_and_expiry_and_filters_expired_and_cancelled()
    {
        var (hr, job, template) = await Setup();
        var invitation = (await (await Invite(hr, job.Id, template.Id)).Content.ReadFromJsonAsync<PanelInvitationResult>())!;
        var id = invitation.Invitation.InterviewId;
        f.Clock.Offset = TimeSpan.FromDays(4);
        try
        {
            var expired = await (await hr.Client.Send(HttpMethod.Get, "/api/interviews?statuses=expired")).Content.ReadFromJsonAsync<Page<PanelInterviewDto>>(); Assert.Equal(id, Assert.Single(expired!.Items).Id);
            var resent = await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{id}/resend", new ResendInterviewRequest(7)); Assert.Equal(HttpStatusCode.OK, resent.StatusCode);
            var fresh = (await resent.Content.ReadFromJsonAsync<PanelInvitationResult>())!;
            Assert.NotEqual(id, fresh.Invitation.InterviewId); Assert.NotEqual(invitation.Invitation.Token, fresh.Invitation.Token);
            using var c = f.CreateClient(); c.DefaultRequestHeaders.Authorization = new("Bearer", invitation.Invitation.Token);
            var changed = await c.GetAsync($"/api/candidate/interviews/{id}/current"); Assert.Equal(HttpStatusCode.Gone, changed.StatusCode);
            Assert.Equal("interview_address_changed", (await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            c.DefaultRequestHeaders.Authorization = new("Bearer", fresh.Invitation.Token);
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/candidate/interviews/{fresh.Invitation.InterviewId}/current")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{fresh.Invitation.InterviewId}/cancel")).StatusCode);
            var cancelled = await (await hr.Client.Send(HttpMethod.Get, "/api/interviews?statuses=cancelled")).Content.ReadFromJsonAsync<Page<PanelInterviewDto>>();
            Assert.Equal(2, cancelled!.Items.Count); Assert.Contains(cancelled.Items, row => row.Id == id && !row.CanResend);
        }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
}
