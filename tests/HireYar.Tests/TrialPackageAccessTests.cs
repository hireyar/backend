using HireYar.Api.Billing;
using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Billing;
using HireYar.Application.Interviews;
using HireYar.Application.InterviewTemplates;
using HireYar.Application.JobPositions;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class TrialPackageAccessTests
{
    [PostgresFact]
    public Task General_interview_works_with_only_trial_credit() => VerifyAsync("general", false);
    [PostgresFact]
    public Task Specialized_interview_works_with_only_trial_credit() => VerifyAsync("specialized", false);
    [PostgresFact]
    public Task Advanced_general_interview_works_with_only_trial_credit() => VerifyAsync("general", true);
    [PostgresFact]
    public Task Advanced_specialized_interview_works_with_only_trial_credit() => VerifyAsync("specialized", true);

    private static async Task VerifyAsync(string type, bool advanced)
    {
        var f = new ApiFixture { TrialCreditAmountUsd = 0.5m, TrialDurationDays = 2 };
        await f.InitializeAsync();
        try
        {
            var owner = await f.RegisterOwner(signupTrial: true);
            var subscription = (await (await owner.Client.Send(HttpMethod.Get, "/api/billing/subscription")).Content.ReadFromJsonAsync<RialSubscriptionOverviewDto>(BillingJson.Options))!.Current!;
            Assert.True(subscription.IsTrial);
            var catalog = (await (await owner.Client.Send(HttpMethod.Get, "/api/pricing/packages")).Content.ReadFromJsonAsync<PricingResponse>(BillingJson.Options))!;
            var expected = catalog.Packages.SelectMany(p => p.Features).Where(x => x.IsEnabled).Select(x => x.Code).Distinct().Order();
            Assert.Equal(expected, subscription.Features.Select(x => x.Code).Order());
            var job = (await (await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/positions", new WizardPositionRequest("Synthetic trial role", "Synthetic SQL and API work"))).Content.ReadFromJsonAsync<JobDto>(BillingJson.Options))!;
            if (advanced)
                Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/question-suggestions", new QuestionSuggestionRequest(job.Id, 5))).StatusCode);
            var request = new WizardTemplateRequest(job.Id, "Synthetic trial template", type,
                advanced ? null : Enumerable.Range(1, 5).Select(i => $"Synthetic question {i}").ToList(), "Welcome", "Goodbye", 5,
                advanced ? FakeQuestionSuggestionProvider.Topics(5) : null);
            async Task<TemplateDto> Save(WizardTemplateRequest value)
            {
                var response = await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates", value);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                return (await response.Content.ReadFromJsonAsync<TemplateDto>(BillingJson.Options))!;
            }
            async Task<InterviewInvitation> Invite(TemplateDto template)
            {
                using var client = f.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", owner.Client.AccessToken);
                using var form = new MultipartFormDataContent();
                foreach (var field in new Dictionary<string, string> { ["jobPositionId"] = job.Id.ToString(), ["templateId"] = template.Id.ToString(),
                    ["firstName"] = "Synthetic", ["lastName"] = "Candidate", ["email"] = "candidate@example.test", ["phone"] = "09123456789", ["interviewType"] = type })
                    form.Add(new StringContent(field.Value), field.Key);
                form.Add(new StringContent("Synthetic SQL work history"), "resumeText");
                form.Add(new ByteArrayContent("%PDF-1.7 synthetic"u8.ToArray()), "resume", "resume.pdf");
                var response = await client.PostAsync("/api/panel-wizard/invitations", form);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                return (await response.Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
            }
            var invite = await Invite(await Save(request));
            using var candidate = f.CreateClient(); candidate.DefaultRequestHeaders.Authorization = new("Bearer", invite.Token);
            var route = $"/api/candidate/interviews/{invite.InterviewId}";
            Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync(route + "/start", null)).StatusCode);
            var state = (await (await candidate.PostAsJsonAsync(route + "/stage/advance", new AdvanceInterviewStageRequest("Welcome"))).Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
            Assert.NotNull(state.Question);
            using var answer = new MultipartFormDataContent();
            answer.Add(new StringContent(state.Question.Id.ToString()), "questionId");
            answer.Add(new StringContent(Guid.NewGuid().ToString("N")), "clientTurnId");
            var audio = new ByteArrayContent("ID3-synthetic-answer"u8.ToArray()); audio.Headers.ContentType = new("audio/mpeg"); answer.Add(audio, "audio", "answer.mp3");
            f.Clock.Offset = TimeSpan.FromSeconds(20);
            Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync(route + "/answers", answer)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync(route + "/complete", null)).StatusCode);
            f.Clock.Offset = TimeSpan.Zero;
            Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/report")).StatusCode);
            await f.Db(async db => {
                var grant = await db.RegistrationTrials.SingleAsync();
                var bill = await db.InterviewBillings.SingleAsync();
                Assert.Equal(grant.Id, bill.SubscriptionId);
                Assert.True(bill.BilledAmountUsd > 0);
                Assert.Equal(BillingMath.Cost(bill.BillableSeconds, bill.RatePerMinuteUsd), bill.BilledAmountUsd);
                Assert.Equal(0.5m - bill.BilledAmountUsd, (await db.OrganizationAiWallets.SingleAsync()).Balance);
                Assert.False(await db.Payments.AnyAsync());
                Assert.False(await db.SubscriptionPurchases.AnyAsync());
            });
            // A longer session still has its features; only the wallet prevents its start.
            var tooLong = await Invite(await Save(request with { Title = "Synthetic trial long template", DurationMinutes = 60 }));
            using var otherCandidate = f.CreateClient(); otherCandidate.DefaultRequestHeaders.Authorization = new("Bearer", tooLong.Token);
            var rejected = await otherCandidate.PostAsync($"/api/candidate/interviews/{tooLong.InterviewId}/start", null);
            Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
            Assert.Contains("insufficient_ai_credit", await rejected.Content.ReadAsStringAsync());
        }
        finally { await ((IAsyncLifetime)f).DisposeAsync(); }
    }
}
