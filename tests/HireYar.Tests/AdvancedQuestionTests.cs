using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Application.InterviewTemplates;
using HireYar.Application.JobPositions;
using HireYar.Application.Billing;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Tests;

public sealed class FakeQuestionSuggestionProvider : IQuestionSuggestionProvider
{
    public int Calls; public bool Fail; public string? Override; public QuestionSuggestionContext? Last;
    public static List<QuestionTopicRequest> Topics(int count) => Enumerable.Range(1, count).Select(i =>
        new QuestionTopicRequest($"موضوع مصنوعی {i}", new[] { "easy", "medium", "hard" }.Select(l => new TopicQuestionRequest(l, $"سؤال {i} {l}")).ToList())).ToList();
    public Task<string> SuggestAsync(QuestionSuggestionContext context, CancellationToken ct)
    {
        Calls++; Last = context;
        if (Fail) throw new InterviewProviderException("llm_timeout");
        var topics = Topics(context.TopicCount ?? 7);
        if (context.TopicName is not null) topics[0] = topics[0] with { Name = context.TopicName };
        return Task.FromResult(Override ?? JsonSerializer.Serialize(new { topics }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
    public void Reset() { Fail = false; Override = null; Last = null; }
}
[Collection("Postgres")]
public sealed class AdvancedQuestionTests(ApiFixture f)
{
    private async Task<(Account Owner, JobDto Job)> Setup(bool advanced = true)
    {
        f.QuestionAi.Reset(); f.InterviewAi.Reset();
        var owner = await f.RegisterOwner();
        if (advanced) await f.FundInterviewsAsync(owner);
        else
        {
            var pricing = (await (await owner.Client.Send(HttpMethod.Get, "/api/pricing/packages")).Content.ReadFromJsonAsync<PricingResponse>(BillingJson.Options))!;
            var period = pricing.Packages.Single(p => p.Code == "basic").Periods.Single(p => p.Months == 1);
            var purchase = (await (await owner.Client.Send(HttpMethod.Post, "/api/billing/subscription/purchases", new SubscriptionPurchaseRequest(period.Id))).Content.ReadFromJsonAsync<PurchaseResult>(BillingJson.Options))!;
            Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Post, $"/api/billing/payments/{purchase.Payment.Id}/verify")).StatusCode);
        }
        var job = (await (await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/positions", new WizardPositionRequest("توسعه‌دهنده مصنوعی", "شرح شغل مصنوعی SQL و API"))).Content.ReadFromJsonAsync<JobDto>(BillingJson.Options))!;
        return (owner, job);
    }
    private static WizardTemplateRequest Template(Guid job, int count = 7) => new(job, "قالب موضوعی", "specialized", null, "شروع", "پایان", 60, FakeQuestionSuggestionProvider.Topics(count));
    private async Task<TemplateDto> Save(Account owner, WizardTemplateRequest r)
    {
        var response = await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates", r);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TemplateDto>(BillingJson.Options))!;
    }
    private async Task<PanelInvitationResult> Invite(Account owner, Guid job, Guid template, string type = "specialized")
    {
        using var client = f.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", owner.Client.AccessToken);
        using var form = new MultipartFormDataContent();
        foreach (var item in new Dictionary<string,string> { ["jobPositionId"] = job.ToString(), ["templateId"] = template.ToString(), ["firstName"] = "Synthetic", ["lastName"] = "Candidate", ["email"] = "candidate@example.test", ["phone"] = "09123456789", ["interviewType"] = type })
            form.Add(new StringContent(item.Value), item.Key);
        form.Add(new ByteArrayContent("%PDF-1.4 synthetic"u8.ToArray()), "resume", "resume.pdf");
        var response = await client.PostAsync("/api/panel-wizard/invitations", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PanelInvitationResult>())!;
    }
    [PostgresFact]
    public async Task Suggestions_use_scoped_JD_support_auto_and_single_topic_and_do_not_save()
    {
        var (owner, job) = await Setup();
        foreach (int? count in new int?[] { 5, 7, 10, null })
        {
            var response = await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/question-suggestions", new QuestionSuggestionRequest(job.Id, count));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var dto = (await response.Content.ReadFromJsonAsync<QuestionSuggestionsDto>())!;
            Assert.Equal(count ?? 7, dto.TopicCount); Assert.Equal(dto.TopicCount * 3, dto.BankQuestionCount); Assert.Equal(dto.TopicCount, dto.InterviewQuestionCount);
            Assert.Equal(job.Description, f.QuestionAi.Last!.PositionDescription);
        }
        var one = await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/question-suggestions/topic", new TopicSuggestionRequest(job.Id, "SQL"));
        Assert.Equal("SQL", Assert.Single((await one.Content.ReadFromJsonAsync<QuestionSuggestionsDto>())!.Topics).Name);
        await f.Db(async db => Assert.False(await db.InterviewTemplates.AnyAsync(t => t.JobPositionId == job.Id)));
        var stranger = await f.RegisterOwner(); var calls = f.QuestionAi.Calls;
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Post, "/api/panel-wizard/question-suggestions", new QuestionSuggestionRequest(job.Id))).StatusCode);
        Assert.Equal(calls, f.QuestionAi.Calls);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/question-suggestions", new QuestionSuggestionRequest(job.Id, 11))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Delete, $"/api/job-positions/{job.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/question-suggestions", new QuestionSuggestionRequest(job.Id))).StatusCode);
    }
    [PostgresFact]
    public async Task Basic_keeps_flat_questions_and_cannot_generate_or_save_topic_bank()
    {
        var (owner, job) = await Setup(false); var calls = f.QuestionAi.Calls;
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/question-suggestions", new QuestionSuggestionRequest(job.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates", Template(job.Id))).StatusCode);
        Assert.Equal(calls, f.QuestionAi.Calls);
        var basic = Template(job.Id) with { Topics = null, Questions = Enumerable.Range(1, 5).Select(i => $"سؤال {i}").ToList(), InterviewType = "general" };
        var saved = await Save(owner, basic); Assert.False(saved.UseTopicQuestions); Assert.Null(saved.Topics); Assert.Equal(5, saved.InterviewQuestionCount);
    }
    [PostgresFact]
    public async Task Malformed_and_failed_AI_responses_return_explicit_errors_without_persisting()
    {
        var (owner, job) = await Setup();
        try
        {
            foreach (var json in new[] { "not-json", "{\"topics\":null}", "{\"topics\":[]}", JsonSerializer.Serialize(new { topics = FakeQuestionSuggestionProvider.Topics(5) }, new JsonSerializerOptions(JsonSerializerDefaults.Web)) })
            {
                f.QuestionAi.Override = json;
                Assert.Equal(HttpStatusCode.BadGateway, (await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/question-suggestions", new QuestionSuggestionRequest(job.Id))).StatusCode);
            }
            f.QuestionAi.Fail = true;
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/question-suggestions", new QuestionSuggestionRequest(job.Id))).StatusCode);
            await f.Db(async db => Assert.False(await db.InterviewTemplates.AnyAsync(t => t.JobPositionId == job.Id)));
        }
        finally { f.QuestionAi.Reset(); }
    }
    [PostgresFact]
    public async Task Approval_edits_orders_levels_and_counts_are_validated_and_versioned()
    {
        var (owner, job) = await Setup();
        foreach (var invalid in new[] { Template(job.Id, 4), Template(job.Id, 11), Template(job.Id) with { Questions = Enumerable.Repeat("سؤال", 5).ToList() }, Template(job.Id) with { Topics = null } })
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates", invalid)).StatusCode);
        var duplicate = Template(job.Id); duplicate.Topics![1] = duplicate.Topics[0];
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates", duplicate)).StatusCode);
        var levels = Template(job.Id); levels.Topics![0].Questions[2] = levels.Topics[0].Questions[0];
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates", levels)).StatusCode);
        var saved = await Save(owner, Template(job.Id));
        Assert.True(saved.UseTopicQuestions); Assert.Equal(21, saved.Questions.Count); Assert.Equal(7, saved.InterviewQuestionCount);
        var update = Template(job.Id, 5); update.Topics![0].Questions[0] = new("easy", "ویرایش کاربر");
        var response = await owner.Client.Send(HttpMethod.Put, $"/api/panel-wizard/templates/{saved.Id}", new UpdateWizardTemplateRequest(update, saved.RowVersion));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = (await response.Content.ReadFromJsonAsync<TemplateDto>(BillingJson.Options))!;
        Assert.Equal("ویرایش کاربر", edited.Topics![0].Questions[0].Text); Assert.Equal(15, edited.Questions.Count);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.Send(HttpMethod.Put, $"/api/panel-wizard/templates/{saved.Id}", new UpdateWizardTemplateRequest(update, saved.RowVersion))).StatusCode);
        var list = (await (await owner.Client.Send(HttpMethod.Get, "/api/interview-templates")).Content.ReadFromJsonAsync<HireYar.Application.Common.Page<TemplateListDto>>(BillingJson.Options))!;
        Assert.Equal(5, Assert.Single(list.Items).InterviewQuestionCount);
    }
    [PostgresFact]
    public async Task Topic_session_asks_one_per_topic_and_preserves_invitation_snapshot_after_edit_and_copy()
    {
        var (owner, job) = await Setup(); var template = await Save(owner, Template(job.Id, 5));
        var other = (await (await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/positions", new WizardPositionRequest("موقعیت دوم", "شرح دوم"))).Content.ReadFromJsonAsync<JobDto>(BillingJson.Options))!;
        var issued = await Invite(owner, other.Id, template.Id); var id = issued.Invitation.InterviewId;
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Put, $"/api/panel-wizard/templates/{template.Id}", new UpdateWizardTemplateRequest(Template(job.Id, 7), template.RowVersion))).StatusCode);
        await f.Db(async db => { var s = await db.InterviewSessions.SingleAsync(s => s.Id == id); Assert.True(s.AskOneQuestionPerTopic); Assert.Equal(5, s.MaxQuestionCount); Assert.Equal(15, s.Questions.Count); Assert.NotEqual(template.Id, s.InterviewTemplateId); Assert.Equal(5, s.Skills.Count); });
        using var candidate = f.CreateClient(); candidate.DefaultRequestHeaders.Authorization = new("Bearer", issued.Invitation.Token);
        var start = await candidate.PostAsync($"/api/candidate/interviews/{id}/start", null);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var current = (await start.Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        var asked = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            Assert.NotNull(current.Question); asked.Add(current.Question!.Id);
            using var form = new MultipartFormDataContent(); form.Add(new StringContent(current.Question.Id.ToString()), "questionId");
            var data = new ByteArrayContent(new byte[] { 0x1a, 0x45, 0xdf, 0xa3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }); data.Headers.ContentType = new("audio/webm"); form.Add(data, "audio", "answer.webm");
            var answer = await candidate.PostAsync($"/api/candidate/interviews/{id}/answers", form);
            Assert.Equal(HttpStatusCode.OK, answer.StatusCode); current = (await answer.Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        }
        Assert.Equal("Completed", current.Status); Assert.Null(current.Question);
        await f.Db(async db => { var s = await db.InterviewSessions.Include(s => s.Answers).SingleAsync(s => s.Id == id); Assert.Equal(5, s.AnsweredQuestionCount); Assert.Equal(5, s.Questions.Where(q => asked.Contains(q.QuestionId)).Select(q => q.Topic).Distinct().Count()); Assert.Equal(3, s.Questions.Single(q => q.QuestionId == asked[0]).Difficulty); });
    }
    [PostgresFact]
    public async Task Advanced_session_start_rechecks_topic_capabilities_even_for_general_invitation()
    {
        var (owner, job) = await Setup(); var template = await Save(owner, Template(job.Id, 5));
        var pricing = (await (await owner.Client.Send(HttpMethod.Get, "/api/pricing/packages")).Content.ReadFromJsonAsync<PricingResponse>(BillingJson.Options))!;
        var basic = pricing.Packages.Single(p => p.Code == "basic").Periods.Single(p => p.Months == 1);
        var purchase = (await (await owner.Client.Send(HttpMethod.Post, "/api/billing/subscription/purchases", new SubscriptionPurchaseRequest(basic.Id))).Content.ReadFromJsonAsync<PurchaseResult>(BillingJson.Options))!;
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Post, $"/api/billing/payments/{purchase.Payment.Id}/verify")).StatusCode);
        DateTimeOffset expiry = default;
        await f.Db(async db => { var org = await db.Users.Where(u => u.Id == owner.Id).Select(u => u.OrganizationId).SingleAsync();
            expiry = await db.OrganizationSubscriptions.Where(s => s.OrganizationId == org && s.Status == HireYar.Domain.Billing.SubscriptionStatus.Active).Select(s => s.ExpiresAtUtc).SingleAsync(); });
        try
        {
            f.Clock.Offset = expiry - DateTimeOffset.UtcNow - TimeSpan.FromHours(1);
            var issued = await Invite(owner, job.Id, template.Id, "general");
            f.Clock.Offset += TimeSpan.FromHours(2);
            using var candidate = f.CreateClient(); candidate.DefaultRequestHeaders.Authorization = new("Bearer", issued.Invitation.Token);
            Assert.Equal(HttpStatusCode.Forbidden, (await candidate.PostAsync($"/api/candidate/interviews/{issued.Invitation.InterviewId}/start", null)).StatusCode);
        }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
}
