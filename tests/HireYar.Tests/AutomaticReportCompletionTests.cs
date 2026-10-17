using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HireYar.Tests;

public sealed partial class InterviewFlowTests
{
    private static async Task CompleteReport(ApiFixture fixture,Guid org,Guid id,CancellationToken ct=default)
    {
        await using var scope=fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ReportCompletionService>().ProcessAsync(org,id,ct);
    }

    [PostgresFact]
    public async Task Completion_worker_automatically_repairs_dimensions_then_generates_prose_without_a_report_request()
    {
        var isolated=new ApiFixture();await isolated.InitializeAsync();
        try {
            var flow=new InterviewFlowTests(isolated);var x=await flow.CompletedForAssessment();
            var calls=0;isolated.AssessmentAi.Handler=(_,_)=>Task.FromResult(Interlocked.Increment(ref calls)==1
                ? FakeAnswerAssessmentProvider.Valid() : WithDimensions("specialized"));
            var worker=new AnswerAssessmentWorker(isolated.Services.GetRequiredService<IServiceScopeFactory>(),new(),isolated.Clock,NullLogger<AnswerAssessmentWorker>.Instance);
            await worker.RunOnceAsync(default);
            Assert.Equal(2,isolated.AssessmentAi.Calls);Assert.Equal(1,isolated.NarrativeAi.Calls);
            var result=await NewReport(x.Hr,x.Session);
            Assert.Equal("Available",result.Narrative!.State);
            Assert.All(result.SpecializedProfile.Metrics.Where(m=>m.Code!="performance"),m=>Assert.NotNull(m.Value));
            Assert.Equal(80,result.Questions[0].Assessment.Value);Assert.Equal(1,result.Questions[0].Assessment.Attempts);
            Assert.Equal("Idle",result.Processing.State);
            await worker.RunOnceAsync(default);
            Assert.Equal(2,isolated.AssessmentAi.Calls);Assert.Equal(1,isolated.NarrativeAi.Calls);
        } finally {await ((IAsyncLifetime)isolated).DisposeAsync();}
    }

    [PostgresFact]
    public async Task Automatic_completion_waits_for_all_answers_and_does_not_rewrite_successful_primary_scores()
    {
        var x=await CompletedForAssessment(2);f.NarrativeAi.Reset();
        await Assess(f,x.Org,x.Answers[0]);
        await CompleteReport(f,x.Org,x.Session);Assert.Equal(1,f.AssessmentAi.Calls);Assert.Equal(0,f.NarrativeAi.Calls);
        await Assess(f,x.Org,x.Answers[1]);
        f.AssessmentAi.Handler=(_,_)=>Task.FromResult(WithDimensions("specialized",5));
        await CompleteReport(f,x.Org,x.Session);
        var result=await NewReport(x.Hr,x.Session);
        Assert.Equal("Available",result.Narrative!.State);Assert.Equal(1,f.NarrativeAi.Calls);
        Assert.All(result.Questions.Where(q=>q.Answer is not null),q=> {Assert.Equal(80,q.Assessment.Value);Assert.Equal(1,q.Assessment.Attempts);Assert.Equal(1,q.Assessment.DimensionRecovery!.Attempts);});
        await CompleteReport(f,x.Org,x.Session);Assert.Equal(4,f.AssessmentAi.Calls);Assert.Equal(1,f.NarrativeAi.Calls);
        f.AssessmentAi.Reset();f.NarrativeAi.Reset();
    }

    [PostgresFact]
    public async Task Automatic_completion_excludes_historical_deleted_and_other_organization_sessions()
    {
        var x=await CompletedForAssessment();await Assess(f,x.Org,x.Answers[0]);f.NarrativeAi.Reset();
        await CompleteReport(f,Guid.NewGuid(),x.Session);Assert.Equal(1,f.AssessmentAi.Calls);Assert.Equal(0,f.NarrativeAi.Calls);
        await f.Db(async db=> {var s=await db.InterviewSessions.SingleAsync(s=>s.Id==x.Session);
            db.Entry(s).Property(s=>s.AutomaticReportCompletionEnabled).CurrentValue=false;await db.SaveChangesAsync();});
        await CompleteReport(f,x.Org,x.Session);Assert.Equal(1,f.AssessmentAi.Calls);Assert.Equal(0,f.NarrativeAi.Calls);
        var report=await NewReport(x.Hr,x.Session);Assert.Equal("NotGenerated",report.Narrative!.State);Assert.Equal("Idle",report.Processing.State);
        await f.Db(async db=> {var s=await db.InterviewSessions.SingleAsync(s=>s.Id==x.Session);
            db.Entry(s).Property(s=>s.AutomaticReportCompletionEnabled).CurrentValue=true;s.MarkDeleted(DateTimeOffset.UtcNow.AddMinutes(1));await db.SaveChangesAsync();});
        await CompleteReport(f,x.Org,x.Session);Assert.Equal(1,f.AssessmentAi.Calls);Assert.Equal(0,f.NarrativeAi.Calls);
    }

    [PostgresFact]
    public async Task Automatic_dimension_failure_exhausts_separate_budget_then_prose_failure_is_bounded_and_scores_survive()
    {
        var x=await CompletedForAssessment();await Assess(f,x.Org,x.Answers[0]);f.NarrativeAi.Reset();
        f.NarrativeAi.Handler=(_,_)=>throw new InterviewProviderException("report_narrative_http_402");
        for(var i=0;i<7;i++) await CompleteReport(f,x.Org,x.Session);
        var report=await NewReport(x.Hr,x.Session);
        Assert.Equal(4,f.AssessmentAi.Calls);Assert.Equal(3,f.NarrativeAi.Calls);
        Assert.Equal(80,report.Questions[0].Assessment.Value);Assert.Equal(1,report.Questions[0].Assessment.Attempts);
        Assert.Equal(3,report.Questions[0].Assessment.DimensionRecovery!.Attempts);
        Assert.All(report.SpecializedProfile.Metrics.Where(m=>m.Code!="performance"),m=>Assert.Null(m.Value));
        Assert.Equal("Failed",report.Narrative!.State);Assert.Equal("narrative_provider_payment_required",report.Narrative.FailureCode);
        Assert.False(report.Narrative.CanGenerate);Assert.Equal("Idle",report.Processing.State);
        f.AssessmentAi.Reset();f.NarrativeAi.Reset();
    }

    [PostgresFact]
    public async Task Explicit_insufficient_evidence_is_not_repaired_and_automatic_narrative_claims_are_fenced()
    {
        var x=await CompletedForAssessment();f.NarrativeAi.Reset();
        f.AssessmentAi.Handler=(_,_)=>Task.FromResult(new AnswerAssessmentResponse("""
            {"verdict":"scored","score":80,"confidence":0.8,"explanation":"Synthetic assessment","evidenceIds":[1],
             "dimensions":[{"code":"knowledge_depth","verdict":"insufficient_evidence","score":null,"confidence":null,"explanation":"No reasoning evidence","evidenceIds":[]},
             {"code":"communication","verdict":"insufficient_evidence","score":null,"confidence":null,"explanation":"No structure evidence","evidenceIds":[]},
             {"code":"job_alignment","verdict":"insufficient_evidence","score":null,"confidence":null,"explanation":"No role evidence","evidenceIds":[]}]}
            ""","Synthetic","synthetic"));
        await Assess(f,x.Org,x.Answers[0]);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource<AnswerAssessmentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.NarrativeAi.Handler=(_,_)=>{entered.TrySetResult();return release.Task;};
        var initial=await NewReport(x.Hr,x.Session);
        var first=CompleteReport(f,x.Org,x.Session);await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try {
            await CompleteReport(f,x.Org,x.Session);
            var manual=await x.Hr.Client.Send(HttpMethod.Post,NarrativeUrl(x.Session),new GenerateReportNarrativeRequest(initial.Fingerprint));
            Assert.Equal(HttpStatusCode.OK,manual.StatusCode);Assert.Equal(1,f.NarrativeAi.Calls);Assert.Equal(1,f.AssessmentAi.Calls);
        } finally {
            f.NarrativeAi.Handler=null;

            release.TrySetResult(new("invalid-json","Synthetic","synthetic"));
        }
        await first;Assert.Equal("Failed",(await NewReport(x.Hr,x.Session)).Narrative!.State);
        f.AssessmentAi.Reset();f.NarrativeAi.Reset();
    }
}