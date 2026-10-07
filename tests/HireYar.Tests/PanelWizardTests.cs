using System.Net;
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
    private async Task<HttpResponseMessage> Invite(Account hr, Guid job, Guid template, string phone = "09123456789", string filename = "resume.pdf", byte[]? bytes = null, string interviewType = "general")
    {
        using var client = f.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", hr.Client.AccessToken);
        using var form = new MultipartFormDataContent();
        foreach (var field in new Dictionary<string, string> { ["jobPositionId"] = job.ToString(), ["templateId"] = template.ToString(),
            ["firstName"] = "Synthetic", ["lastName"] = "Candidate", ["email"] = "candidate@example.test", ["phone"] = phone, ["deadlineDays"] = "3", ["interviewType"] = interviewType })
            form.Add(new StringContent(field.Value), field.Key);
        form.Add(new StringContent("Synthetic candidate work history and projects"), "resumeText");
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
            Assert.Equal(InterviewDecisionPrompt.Master, context.MasterPrompt); Assert.Equal(5, context.QuestionBank!.Count);
            Assert.Single(context.PreviousAnswers!); Assert.NotNull(context.PreviousAnswers![0].AssistantReply);
            Assert.Equal("سؤال مصنوعی 2", current.Question!.Text);
            for (var i = 1; i < 5; i++) current = await Submit(current);
            Assert.Equal("Completed", current.Status); Assert.Equal("CompanyIntroduction", current.Stage); Assert.Equal(changed.CompanyIntroductionMessage, current.StageMessage);
            var goodbye = (await (await c.PostAsJsonAsync(route + "/stage/advance", new AdvanceInterviewStageRequest("CompanyIntroduction"))).Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
            Assert.Equal("Goodbye", goodbye.Stage); Assert.Equal(changed.GoodbyeMessage, goodbye.StageMessage);
            await f.Db(async db => { var s = await db.InterviewSessions.Include(s => s.Answers).SingleAsync(s => s.Id == invite.InterviewId);
                Assert.Equal(5, s.AnsweredQuestionCount); Assert.Equal(6, s.Answers.Count); Assert.Single(s.Answers, a => a.IsInteraction);
                Assert.Equal(InterviewDecisionPrompt.Master, s.MasterPromptSnapshot); Assert.Equal("v2", s.MasterPromptVersion); Assert.False(db.Database.HasPendingModelChanges()); });
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
        Assert.Equal(HttpStatusCode.Conflict, (await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{id}/resend", new ResendInterviewRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{id}/cancel")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await candidate.GetAsync($"/api/candidate/interviews/{id}/current")).StatusCode);
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
        var purchase = (await (await hr.Client.Send(HttpMethod.Post, "/api/billing/subscription/purchases", new HireYar.Application.Billing.SubscriptionPurchaseRequest(period.Id))).Content.ReadFromJsonAsync<HireYar.Application.Billing.PurchaseResult>(BillingJson.Options))!;
        Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Post, $"/api/billing/payments/{purchase.Payment.Id}/verify")).StatusCode);
        var general = await Invite(hr, job.Id, template.Id); Assert.Equal(HttpStatusCode.OK, general.StatusCode);
        var issued = (await general.Content.ReadFromJsonAsync<PanelInvitationResult>())!;
        await f.Db(async db => Assert.Equal("hr_interview", (await db.InterviewSessions.SingleAsync(x => x.Id == issued.Invitation.InterviewId)).RequiredCapabilityCode));
        Assert.Equal(HttpStatusCode.Forbidden, (await Invite(hr, job.Id, template.Id, interviewType: "specialized")).StatusCode);
        var rows = (await (await hr.Client.Send(HttpMethod.Get, "/api/interviews")).Content.ReadFromJsonAsync<Page<PanelInterviewDto>>())!;
        Assert.Equal("general", Assert.Single(rows.Items).InterviewType);
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
            var fresh = (await resent.Content.ReadFromJsonAsync<PanelInvitationResult>())!; Assert.NotEqual(invitation.Invitation.Token, fresh.Invitation.Token);
            using var c = f.CreateClient(); c.DefaultRequestHeaders.Authorization = new("Bearer", invitation.Invitation.Token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync($"/api/candidate/interviews/{id}/current")).StatusCode);
            c.DefaultRequestHeaders.Authorization = new("Bearer", fresh.Invitation.Token);
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/candidate/interviews/{id}/current")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{id}/cancel")).StatusCode);
            var cancelled = await (await hr.Client.Send(HttpMethod.Get, "/api/interviews?statuses=cancelled")).Content.ReadFromJsonAsync<Page<PanelInterviewDto>>(); Assert.Equal(id, Assert.Single(cancelled!.Items).Id);
        }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
}
