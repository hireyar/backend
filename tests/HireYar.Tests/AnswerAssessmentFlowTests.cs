using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HireYar.Tests;

public sealed partial class InterviewFlowTests
{
    private async Task<(Account Hr, Guid Session, Guid Org, Guid[] Answers)> CompletedForAssessment(int count = 1)
    {
        f.AssessmentAi.Reset(); var (hr, invite) = await Invite(); using var candidate = Candidate(invite);
        var state = await Start(candidate, invite.InterviewId);
        for(var i=0;i<count;i++) {
            var response=await Submit(candidate,invite.InterviewId,state.Question!.Id);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            state=(await response.Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        }
        Assert.Equal(HttpStatusCode.OK,(await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete",null)).StatusCode);
        Guid org=default;Guid[] answers=[];await f.Db(async db=> {
            org=await db.InterviewSessions.Where(s=>s.Id==invite.InterviewId).Select(s=>s.OrganizationId).SingleAsync();
            answers=await db.InterviewAnswers.Where(a=>a.InterviewSessionId==invite.InterviewId && !a.IsInteraction).OrderBy(a=>a.SubmittedAtUtc).ThenBy(a=>a.Id).Select(a=>a.Id).ToArrayAsync();
        });
        return(hr,invite.InterviewId,org,answers);
    }
    private static async Task Assess(ApiFixture fixture,Guid org,Guid answer,CancellationToken ct=default)
    {
        await using var scope=fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AnswerAssessmentService>().ProcessAsync(org,answer,ct);
    }
    private static string RetryUrl(Guid session,Guid answer)=>$"/api/interviews/{session}/report/answers/{answer}/assessment/retry";

    [PostgresFact]
    public async Task Independent_assessment_preserves_other_answers_and_report_when_one_output_is_invalid()
    {
        var x=await CompletedForAssessment(2);
        f.AssessmentAi.Handler=(_,_)=>Task.FromResult(new AnswerAssessmentResponse("not-json","Synthetic","synthetic"));
        await Assess(f,x.Org,x.Answers[0]);f.AssessmentAi.Handler=null;await Assess(f,x.Org,x.Answers[1]);
        var report=await NewReport(x.Hr,x.Session);
        Assert.Equal("BaseReady",report.State);Assert.Equal("Failed",report.Questions[0].Assessment.State);
        Assert.Equal("assessment_invalid_output",report.Questions[0].Assessment.FailureCode);Assert.True(report.Questions[0].Assessment.CanRetry);
        var success=report.Questions[1];Assert.Equal("Succeeded",success.Assessment.State);Assert.Equal(80,success.Assessment.Value);
        var evidence=Assert.Single(success.Assessment.Evidence);Assert.Equal(success.Answer!.AnswerId,evidence.AnswerId);
        Assert.Equal(evidence.Quote,success.Answer.Transcript!.Substring(evidence.Start,evidence.Length));
        Assert.Equal("answer-quality/2",success.Assessment.EvaluatorVersion);Assert.Equal(80,report.OverallScore.Value);Assert.True(report.Coverage.IsProvisional);
        await Assess(f,x.Org,x.Answers[1]);Assert.Equal(2,f.AssessmentAi.Calls);
        Assert.Equal(HttpStatusCode.Conflict,(await x.Hr.Client.Send(HttpMethod.Post,RetryUrl(x.Session,x.Answers[1]))).StatusCode);
        var html=await(await x.Hr.Client.Send(HttpMethod.Get,$"/api/interviews/{x.Session}/report/print")).Content.ReadAsStringAsync();
        Assert.Contains("ارزیابی AI",html);Assert.Contains("۴ / ۵",html);Assert.Contains("Synthetic job-related answer",html);
    }

    [PostgresFact]
    public async Task Concurrent_claims_and_duplicate_retry_requests_make_only_one_model_call_per_attempt()
    {
        var x=await CompletedForAssessment();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource<AnswerAssessmentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.AssessmentAi.Handler=(_,_)=>{entered.TrySetResult();return release.Task;};
        var first=Assess(f,x.Org,x.Answers[0]);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try {
            await Assess(f,x.Org,x.Answers[0]);Assert.Equal(1,f.AssessmentAi.Calls);
            var live=await NewReport(x.Hr,x.Session);Assert.Equal("Processing",live.Questions[0].Assessment.State);Assert.Equal("BaseReady",live.State);
        } finally {release.TrySetResult(new("not-json","Synthetic","synthetic"));}
        await first;
        var retries=await Task.WhenAll(x.Hr.Client.Send(HttpMethod.Post,RetryUrl(x.Session,x.Answers[0])),x.Hr.Client.Send(HttpMethod.Post,RetryUrl(x.Session,x.Answers[0])));
        Assert.All(retries,r=>Assert.Equal(HttpStatusCode.Accepted,r.StatusCode));
        f.AssessmentAi.Handler=null;await Assess(f,x.Org,x.Answers[0]);
        var report=await NewReport(x.Hr,x.Session);Assert.Equal("Succeeded",report.Questions[0].Assessment.State);
        Assert.Equal(2,report.Questions[0].Assessment.Attempts);Assert.Equal(2,f.AssessmentAi.Calls);
        await f.Db(async db=>Assert.Single(await db.AnswerAssessments.Where(a=>a.InterviewSessionId==x.Session).ToArrayAsync()));
    }

    [PostgresFact]
    public async Task Noncooperative_timeout_is_bounded_and_late_result_cannot_overwrite_retry_success()
    {
        var x=await CompletedForAssessment();var late=new TaskCompletionSource<AnswerAssessmentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.AssessmentAi.Handler=(_,_)=>late.Task;
        await Assess(f,x.Org,x.Answers[0]).WaitAsync(TimeSpan.FromSeconds(6));
        var failed=await NewReport(x.Hr,x.Session);Assert.Equal("Queued",failed.Questions[0].Assessment.State);
        Assert.Equal("assessment_timeout",failed.Questions[0].Assessment.FailureCode);
        var original=f.Clock.Offset;
        try {
            f.Clock.Offset+=TimeSpan.FromSeconds(6);f.AssessmentAi.Handler=(_,_)=>Task.FromResult(FakeAnswerAssessmentProvider.Valid(20));
            await Assess(f,x.Org,x.Answers[0]);late.TrySetResult(FakeAnswerAssessmentProvider.Valid(95));await late.Task;
            var report=await NewReport(x.Hr,x.Session);Assert.Equal(20,report.Questions[0].Assessment.Value);Assert.Equal(2,report.Questions[0].Assessment.Attempts);
        } finally {f.Clock.Offset=original;late.TrySetResult(FakeAnswerAssessmentProvider.Valid());f.AssessmentAi.Reset();}
    }

    [PostgresFact]
    public async Task Worker_restart_recovers_expired_processing_and_exhausts_the_third_lease_without_new_model_call()
    {
        var x=await CompletedForAssessment();
        await f.Db(async db=> {db.AnswerAssessments.Add(new(){OrganizationId=x.Org,InterviewSessionId=x.Session,AnswerId=x.Answers[0],State="Processing",Attempts=1,AttemptId=Guid.NewGuid(),RequestedAtUtc=f.Clock.GetUtcNow().AddMinutes(-5),StartedAtUtc=f.Clock.GetUtcNow().AddMinutes(-4),LeaseExpiresAtUtc=f.Clock.GetUtcNow().AddMinutes(-1)});await db.SaveChangesAsync();});
        Assert.Equal("RecoveryPending",(await NewReport(x.Hr,x.Session)).Questions[0].Assessment.State);
        await Assess(f,x.Org,x.Answers[0]);Assert.Equal("Succeeded",(await NewReport(x.Hr,x.Session)).Questions[0].Assessment.State);
        var y=await CompletedForAssessment();
        await f.Db(async db=> {db.AnswerAssessments.Add(new(){OrganizationId=y.Org,InterviewSessionId=y.Session,AnswerId=y.Answers[0],State="Processing",Attempts=3,AttemptId=Guid.NewGuid(),RequestedAtUtc=f.Clock.GetUtcNow().AddMinutes(-5),StartedAtUtc=f.Clock.GetUtcNow().AddMinutes(-4),LeaseExpiresAtUtc=f.Clock.GetUtcNow().AddMinutes(-1)});await db.SaveChangesAsync();});
        await Assess(f,y.Org,y.Answers[0]);var report=await NewReport(y.Hr,y.Session);
        Assert.Equal("Failed",report.Questions[0].Assessment.State);Assert.False(report.Questions[0].Assessment.CanRetry);Assert.Equal(0,f.AssessmentAi.Calls);
    }

    [PostgresFact]
    public async Task Automatic_transient_retries_have_backoff_and_a_fixed_three_attempt_budget()
    {
        var x=await CompletedForAssessment();var original=f.Clock.Offset;
        f.AssessmentAi.Handler=(_,_)=>throw new InterviewProviderException("answer_assessment_http_503");
        try {
            for(var attempt=1;attempt<=3;attempt++) {
                await Assess(f,x.Org,x.Answers[0]);var report=await NewReport(x.Hr,x.Session);
                Assert.Equal(attempt,report.Questions[0].Assessment.Attempts);Assert.Equal(attempt<3?"Queued":"Failed",report.Questions[0].Assessment.State);
                await Assess(f,x.Org,x.Answers[0]);Assert.Equal(attempt,f.AssessmentAi.Calls);f.Clock.Offset+=TimeSpan.FromSeconds(31);
            }
            var terminal=(await NewReport(x.Hr,x.Session)).Questions[0].Assessment;Assert.False(terminal.CanRetry);Assert.Null(terminal.Value);
            Assert.Equal(HttpStatusCode.Conflict,(await x.Hr.Client.Send(HttpMethod.Post,RetryUrl(x.Session,x.Answers[0]))).StatusCode);
        } finally {f.Clock.Offset=original;f.AssessmentAi.Reset();}
    }

    [PostgresFact]
    public async Task Changed_input_during_processing_is_not_committed_as_a_valid_assessment()
    {
        var x=await CompletedForAssessment();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource<AnswerAssessmentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.AssessmentAi.Handler=(_,_)=>{entered.TrySetResult();return release.Task;};
        var processing=Assess(f,x.Org,x.Answers[0]);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try {await f.Db(async db=>await db.InterviewAnswers.Where(a=>a.Id==x.Answers[0]).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Transcript,"Synthetic changed transcript")));}
        finally {release.TrySetResult(FakeAnswerAssessmentProvider.Valid());}
        await processing;var report=await NewReport(x.Hr,x.Session);
        Assert.Equal("Failed",report.Questions[0].Assessment.State);Assert.Equal("assessment_input_changed",report.Questions[0].Assessment.FailureCode);Assert.Null(report.Questions[0].Assessment.Value);
    }

    [PostgresFact]
    public async Task Assessment_retry_is_tenant_scoped_and_database_rejects_cross_tenant_assessment()
    {
        var x=await CompletedForAssessment();var other=await f.RegisterOwner();var url=RetryUrl(x.Session,x.Answers[0]);
        Assert.Equal(HttpStatusCode.NotFound,(await other.Client.Send(HttpMethod.Post,url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await x.Hr.Client.Send(HttpMethod.Post,RetryUrl(x.Session,Guid.NewGuid()))).StatusCode);
        Assert.Equal(0,f.AssessmentAi.Calls);
        await f.Db(async db=> {
            var org=await db.Users.Where(u=>u.Id==other.Id).Select(u=>u.OrganizationId).SingleAsync();
            db.AnswerAssessments.Add(new(){OrganizationId=org,InterviewSessionId=x.Session,AnswerId=x.Answers[0],RequestedAtUtc=f.Clock.GetUtcNow()});
            var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());Assert.Equal("23503",Assert.IsType<PostgresException>(error.InnerException).SqlState);
        });
    }

    [PostgresFact]
    public async Task Insufficient_evidence_remains_distinct_from_failed_or_zero_and_corrupt_stored_evidence_is_isolated()
    {
        var x=await CompletedForAssessment(2);
        f.AssessmentAi.Handler=(_,_)=>Task.FromResult(new AnswerAssessmentResponse("{\"verdict\":\"insufficient_evidence\",\"score\":null,\"confidence\":null,\"explanation\":\"Not enough evidence\",\"evidenceIds\":[]}","Synthetic","synthetic"));
        await Assess(f,x.Org,x.Answers[0]);f.AssessmentAi.Handler=null;await Assess(f,x.Org,x.Answers[1]);
        await f.Db(async db=> {var row=await db.AnswerAssessments.SingleAsync(a=>a.AnswerId==x.Answers[1]);row.EvidenceJson="[{}]";await db.SaveChangesAsync();});
        var report=await NewReport(x.Hr,x.Session);Assert.Equal("BaseReady",report.State);
        Assert.Equal("InsufficientEvidence",report.Questions[0].Assessment.State);Assert.Null(report.Questions[0].Assessment.Value);Assert.False(report.Questions[0].Assessment.CanRetry);
        Assert.Equal("Unavailable",report.Questions[1].Assessment.State);Assert.Equal("assessment_storage_invalid",report.Questions[1].Assessment.FailureCode);Assert.Null(report.Questions[1].Assessment.Value);
    }

    [PostgresFact]
    public async Task Worker_discovers_completed_answers_without_a_report_GET_and_migration_is_idempotent()
    {
        var isolated=new ApiFixture();await isolated.InitializeAsync();
        try {
            var flow=new InterviewFlowTests(isolated);var x=await flow.CompletedForAssessment();
            await isolated.Db(async db=> {
                // Preserve the primary-only historical completion scenario without rolling back the schema.
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"InterviewSessions\" SET \"AutomaticReportCompletionEnabled\" = false WHERE \"Id\" = {x.Session}");
                Assert.Empty(await db.AnswerAssessments.ToArrayAsync());
                var migrations=(await db.Database.GetAppliedMigrationsAsync()).ToArray();var index=Array.FindIndex(migrations,m=>m.EndsWith("_AddAnswerReportAssessments",StringComparison.Ordinal));var latest=migrations[index];Assert.EndsWith("_AddAnswerReportAssessments",latest);
                var migrator=db.GetService<IMigrator>();var script=migrator.GenerateScript(migrations[index-1],latest,MigrationsSqlGenerationOptions.Idempotent);
                await db.Database.ExecuteSqlRawAsync(script);await db.Database.ExecuteSqlRawAsync(script);await migrator.MigrateAsync(migrations[^1]);Assert.False(db.Database.HasPendingModelChanges());
                Assert.False(await db.InterviewSessions.Where(s=>s.Id==x.Session).Select(s=>s.AutomaticReportCompletionEnabled).SingleAsync());
                var futureScript=migrator.GenerateScript(migrations[^2],migrations[^1],MigrationsSqlGenerationOptions.Idempotent);
                await db.Database.ExecuteSqlRawAsync(futureScript);await db.Database.ExecuteSqlRawAsync(futureScript);
            });
            var worker=new AnswerAssessmentWorker(isolated.Services.GetRequiredService<IServiceScopeFactory>(),new(),isolated.Clock,Microsoft.Extensions.Logging.Abstractions.NullLogger<AnswerAssessmentWorker>.Instance);
            await worker.RunOnceAsync(default);Assert.Equal(1,isolated.AssessmentAi.Calls);
            Assert.Equal("Succeeded",(await NewReport(x.Hr,x.Session)).Questions[0].Assessment.State);
            await worker.RunOnceAsync(default);Assert.Equal(1,isolated.AssessmentAi.Calls);
        } finally {await ((IAsyncLifetime)isolated).DisposeAsync();}
    }
    [PostgresFact]
    public async Task Reclaimed_lease_fences_a_still_running_older_attempt_and_shutdown_cancellation_is_recoverable()
    {
        var x=await CompletedForAssessment();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource<AnswerAssessmentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.AssessmentAi.Handler=(_,_)=>{entered.TrySetResult();return release.Task;};
        var old=Assess(f,x.Org,x.Answers[0]);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));var original=f.Clock.Offset;
        try {
            f.Clock.Offset+=TimeSpan.FromMinutes(3);f.AssessmentAi.Handler=(_,_)=>Task.FromResult(FakeAnswerAssessmentProvider.Valid(30));
            await Assess(f,x.Org,x.Answers[0]);release.TrySetResult(FakeAnswerAssessmentProvider.Valid(99));await old;
            var report=await NewReport(x.Hr,x.Session);Assert.Equal(30,report.Questions[0].Assessment.Value);Assert.Equal(2,report.Questions[0].Assessment.Attempts);
        } finally {f.Clock.Offset=original;release.TrySetResult(FakeAnswerAssessmentProvider.Valid());await old;f.AssessmentAi.Reset();}
        var y=await CompletedForAssessment();entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        release=new(TaskCreationOptions.RunContinuationsAsynchronously);using var stop=new CancellationTokenSource();
        f.AssessmentAi.Handler=(_,_)=>{entered.TrySetResult();return release.Task;};
        var cancelled=Assess(f,y.Org,y.Answers[0],stop.Token);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>cancelled);
        try {
            f.Clock.Offset+=TimeSpan.FromMinutes(3);f.AssessmentAi.Handler=null;
            Assert.Equal("RecoveryPending",(await NewReport(y.Hr,y.Session)).Questions[0].Assessment.State);
            await Assess(f,y.Org,y.Answers[0]);Assert.Equal("Succeeded",(await NewReport(y.Hr,y.Session)).Questions[0].Assessment.State);
        } finally {f.Clock.Offset=original;release.TrySetResult(FakeAnswerAssessmentProvider.Valid());f.AssessmentAi.Reset();}
    }

}
