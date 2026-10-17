using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HireYar.Infrastructure.Interviews;

namespace HireYar.Tests;

public sealed class ReportReadinessTests
{
    private static ReportAnswerAssessment Assessment(string state) => new(state,null,null,null,null,[],0,false,null,null,null,null,null,null,null,null);
    private static readonly ReportSection[] Unimplemented = [new("score","امتیاز کل","NotImplemented","در صف نیست")];

    [Theory]
    [InlineData("Queued",true)]
    [InlineData("Processing",true)]
    [InlineData("RecoveryPending",true)]
    [InlineData("Succeeded",false)]
    [InlineData("InsufficientEvidence",false)]
    [InlineData("Failed",false)]
    [InlineData("Unavailable",false)]
    [InlineData("NotReady",false)]
    [InlineData("NotApplicable",false)]
    public void Only_pending_answer_assessments_cause_polling(string state,bool pending)
    {
        var result=ReportProcessingStatus.From([Assessment(state)],Unimplemented,new());
        Assert.Equal(pending,result.ShouldPoll);Assert.Equal(pending?"Pending":"Idle",result.State);
        Assert.Equal(pending?1:0,result.PendingAssessments);Assert.Equal(pending?(int?)3:null,result.PollAfterSeconds);
        Assert.True(result.HasUnimplementedSections);Assert.Null(result.ReasonCode);
        Assert.All(Unimplemented,s=>Assert.False(s.ShouldPoll));
    }
    [Fact]
    public void Disabled_worker_is_paused_and_does_not_instruct_endless_polling()
    {
        var result=ReportProcessingStatus.From([Assessment("Queued"),Assessment("Failed"),Assessment("Succeeded"),Assessment("Unavailable"),Assessment("InsufficientEvidence")],Unimplemented,new(){WorkerEnabled=false});
        Assert.Equal("Paused",result.State);Assert.False(result.ShouldPoll);Assert.Null(result.PollAfterSeconds);
        Assert.Equal("assessment_worker_disabled",result.ReasonCode);Assert.Equal(1,result.PendingAssessments);
        Assert.Equal(2,result.CompletedAssessments);Assert.Equal(1,result.FailedAssessments);Assert.Equal(1,result.UnavailableAssessments);
    }
    [Fact]
    public void Unimplemented_sections_alone_never_create_work_or_polling()
    {
        var result=ReportProcessingStatus.From([],Unimplemented,new());
        Assert.Equal("Idle",result.State);Assert.False(result.ShouldPoll);Assert.Equal(0,result.PendingAssessments);
        Assert.True(result.HasUnimplementedSections);Assert.False(Unimplemented[0].ShouldPoll);
    }
}

public sealed partial class InterviewFlowTests
{
    [PostgresFact]
    public async Task Retry_has_typed_202_body_and_unimplemented_sections_are_not_work_in_progress()
    {
        var x=await CompletedForAssessment();
        var response=await x.Hr.Client.Send(HttpMethod.Post,RetryUrl(x.Session,x.Answers[0]));
        Assert.Equal(HttpStatusCode.Accepted,response.StatusCode);Assert.Equal($"/api/interviews/{x.Session}/report",response.Headers.Location!.ToString());
        var body=(await response.Content.ReadFromJsonAsync<RetryAnswerAssessmentResponse>())!;
        Assert.Equal(x.Session,body.InterviewId);Assert.Equal(x.Answers[0],body.AnswerId);Assert.Equal("Queued",body.Assessment.State);Assert.True(body.Assessment.IsPending);
        var report=await NewReport(x.Hr,x.Session);
        Assert.Equal("Paused",report.Processing.State);Assert.False(report.Processing.ShouldPoll);Assert.Equal("assessment_worker_disabled",report.Processing.ReasonCode);
        Assert.All(report.Sections.Where(s=>s.State=="NotImplemented"),s=> {Assert.False(s.ShouldPoll);Assert.Contains("در صف پردازش نیست",s.Message);});
        Assert.Equal("InsufficientEvidence",report.OverallScore.State);Assert.Equal("Available",report.Summary.State);
        Assert.DoesNotContain(report.Sections,s=>s.State=="NotImplemented");
        Assert.DoesNotContain(report.Sections,s=>s.State=="NotStarted");
        await Assess(f,x.Org,x.Answers[0]);report=await NewReport(x.Hr,x.Session);
        Assert.Equal("Paused",report.Processing.State);Assert.False(report.Processing.ShouldPoll);Assert.Equal(1,report.Processing.CompletedAssessments);
        Assert.Equal(0,report.Processing.PendingAssessments);Assert.False(report.Processing.HasUnimplementedSections);
        Assert.Equal(80,report.OverallScore.Value);Assert.Equal("Partial",report.OverallScore.State);
    }

    [PostgresFact]
    public async Task Swagger_retry_declares_202_with_the_same_typed_body_as_the_endpoint()
    {
        var json=await(await f.Browser().Send(HttpMethod.Get,"/openapi/v1.json")).Content.ReadFromJsonAsync<JsonElement>();
        var operation=json.GetProperty("paths").GetProperty("/api/interviews/{id}/report/answers/{answerId}/assessment/retry").GetProperty("post");
        var responses=operation.GetProperty("responses");Assert.False(responses.TryGetProperty("200",out _));
        JsonElement Resolve(JsonElement schema)=>schema.TryGetProperty("$ref",out var reference)?json.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/').Last()):schema;
        var body=Resolve(responses.GetProperty("202").GetProperty("content").GetProperty("application/json").GetProperty("schema"));
        var properties=body.GetProperty("properties");Assert.True(properties.TryGetProperty("interviewId",out _));Assert.True(properties.TryGetProperty("answerId",out _));
        var assessment=Resolve(properties.GetProperty("assessment")).GetProperty("properties");
        foreach(var field in new[]{"state","value","confidence","evidence","attempts","canRetry","failureCode","isPending"}) Assert.True(assessment.TryGetProperty(field,out _),field);
    }

    [PostgresFact]
    public async Task Real_hosted_worker_finishes_answer_assessments_while_unimplemented_sections_remain_non_pending()
    {
        var isolated=new ApiFixture{AssessmentWorkerEnabled=true};await isolated.InitializeAsync();
        try {
            var flow=new InterviewFlowTests(isolated);var x=await flow.CompletedForAssessment();
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));
            SessionReportDocument report;
            do {report=await NewReport(x.Hr,x.Session);if(!report.Processing.ShouldPoll) break;await Task.Delay(100,deadline.Token);}while(true);
            Assert.Equal("Idle",report.Processing.State);Assert.Equal(0,report.Processing.PendingAssessments);
            Assert.Equal("Succeeded",report.Questions[0].Assessment.State);Assert.Equal(4,isolated.AssessmentAi.Calls);Assert.Equal(1,isolated.NarrativeAi.Calls);Assert.Equal("Available",report.Narrative!.State);
            Assert.False(report.Processing.HasUnimplementedSections);Assert.Equal("Partial",report.OverallScore.State);
            Assert.All(report.Sections.Where(s=>s.State=="NotImplemented"),s=>Assert.False(s.ShouldPoll));
        } finally {
            await isolated.Services.GetServices<IHostedService>().OfType<AnswerAssessmentWorker>().Single().StopAsync(CancellationToken.None);
            await((IAsyncLifetime)isolated).DisposeAsync();
        }
    }
}
