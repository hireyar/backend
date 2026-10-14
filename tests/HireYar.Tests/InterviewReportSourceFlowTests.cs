using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HireYar.Tests;

public sealed partial class InterviewFlowTests
{
    private sealed class ReferenceReportHandler(bool invalid = false, bool recoverOnRepair = false) : HttpMessageHandler
    {
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("report-reference-model",body.RootElement.GetProperty("model").GetString());
            using var prompt=JsonDocument.Parse(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
            var context=prompt.RootElement.GetProperty("interview").Deserialize<InterviewReportContext>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var sources=prompt.RootElement.GetProperty("evidenceSources");
            Assert.Equal(context.Answers.Count(a=>!a.IsInteraction),sources.EnumerateArray().Select(s=>s.GetProperty("answerRef").GetString()).Distinct().Count());
            var answerCount=context.Answers.Count(a=>!a.IsInteraction);
            var response=JsonSerializer.Serialize(new {
                summary="گزارش آزمایشی", findings=Array.Empty<object>(),limitations=new[]{"نیازمند بررسی انسانی"},nextRoundFocus=Array.Empty<string>(),
                questionEvaluations=Enumerable.Range(1,answerCount).Select(i=>new {
                    answerRef="A"+i.ToString(CultureInfo.InvariantCulture),sourceRef="A"+i.ToString(CultureInfo.InvariantCulture)+".1",
                    score=3,confidence=0.8m,feedback="ارزیابی آزمایشی",highlights=Array.Empty<string>(),gaps=Array.Empty<string>()
                }),
                metrics=InterviewReportMetricCatalog.Codes(context.InterviewType).Select(code=> new {
                    code,score=answerCount==0?(int?)null:3,confidence=answerCount==0?(decimal?)null:0.8m,explanation="شاهد آزمایشی",
                    evidence=answerCount==0?Array.Empty<object>():new object[]{new {sourceRef=invalid && (!recoverOnRepair || Calls == 1)?"A999.1":"A1.1"}}
                })
            });
            return new(HttpStatusCode.OK){Content=JsonContent.Create(new {choices=new[]{new {message=new {content=response}}}})};
        }
    }

    [PostgresFact]
    public async Task Report_reference_provider_repairs_once_and_persists_valid_report_in_existing_schema_and_cache()
    {
        f.ReportAi.Reset(); var (hr,invite)=await Invite(); using var candidate=Candidate(invite);
        var state=await Start(candidate,invite.InterviewId);
        Assert.Equal(HttpStatusCode.OK,(await Submit(candidate,invite.InterviewId,state.Question!.Id)).StatusCode);
        await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete",null);
        var handler=new ReferenceReportHandler(invalid:true, recoverOnRepair:true); using var http=new HttpClient(handler);
        var provider=new LiaraClient(http,Options.Create(new LiaraOptions{BaseUrl="https://synthetic.invalid/v1",ApiKey="synthetic",EvaluationModel="report-reference-model"}),NullLogger<LiaraClient>.Instance);
        await using(var scope=f.Services.CreateAsyncScope()) {
            var db=scope.ServiceProvider.GetRequiredService<HireYar.Infrastructure.Persistence.AppDbContext>();
            var org=await db.InterviewSessions.Where(s=>s.Id==invite.InterviewId).Select(s=>s.OrganizationId).SingleAsync();
            await ActivatorUtilities.CreateInstance<InterviewReportService>(scope.ServiceProvider,provider).ProcessQueuedAsync(org,invite.InterviewId,default);
        }
        var status=await ReportStatus(hr,invite.InterviewId);
        Assert.Equal("Succeeded",status.Status); Assert.True(status.ReportAvailable); Assert.Equal(2,handler.Calls); Assert.Equal(1,status.Attempts);
        var response=await hr.Client.Send(HttpMethod.Get,$"/api/interviews/{invite.InterviewId}/evaluation-report");
        var json=await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK,response.StatusCode); Assert.DoesNotContain("sourceRef",json); Assert.DoesNotContain("answerRef",json);
        var report=(await response.Content.ReadFromJsonAsync<InterviewReportDto>())!;
        Assert.Equal(InterviewReportPrompt.Version,report.PromptVersion);
        await f.Db(async db=> {
            var answers=await db.InterviewAnswers.Where(a=>a.InterviewSessionId==invite.InterviewId&&a.Status==AnswerStatus.Processed&&!a.IsInteraction).ToDictionaryAsync(a=>a.Id);
            Assert.All(report.Content.Metrics!.SelectMany(m=>m.Evidence),c=>Assert.Contains(c.Quote,answers[c.AnswerId].Transcript!));
        });
        Assert.Equal(json,await(await hr.Client.Send(HttpMethod.Get,$"/api/interviews/{invite.InterviewId}/evaluation-report")).Content.ReadAsStringAsync());
    }

    [PostgresFact]
    public async Task Invalid_model_source_reference_fails_without_persisting_fabricated_evidence()
    {
        var (hr,invite)=await Invite(); using var candidate=Candidate(invite);
        var state=await Start(candidate,invite.InterviewId); await Submit(candidate,invite.InterviewId,state.Question!.Id);
        await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete",null);
        var handler=new ReferenceReportHandler(invalid:true); using var http=new HttpClient(handler);
        var provider=new LiaraClient(http,Options.Create(new LiaraOptions{BaseUrl="https://synthetic.invalid/v1",ApiKey="synthetic",EvaluationModel="report-reference-model"}),NullLogger<LiaraClient>.Instance);
        await using(var scope=f.Services.CreateAsyncScope()) {
            var db=scope.ServiceProvider.GetRequiredService<HireYar.Infrastructure.Persistence.AppDbContext>();
            var org=await db.InterviewSessions.Where(s=>s.Id==invite.InterviewId).Select(s=>s.OrganizationId).SingleAsync();
            await ActivatorUtilities.CreateInstance<InterviewReportService>(scope.ServiceProvider,provider).ProcessQueuedAsync(org,invite.InterviewId,default);
        }
        var status=await ReportStatus(hr,invite.InterviewId);
        Assert.Equal("Failed",status.Status); Assert.True(status.CanRetry); Assert.False(status.ReportAvailable);
        await f.Db(async db=>Assert.Null((await db.InterviewEvaluationReports.SingleAsync(r=>r.InterviewSessionId==invite.InterviewId)).ResultJson));
    }
}
