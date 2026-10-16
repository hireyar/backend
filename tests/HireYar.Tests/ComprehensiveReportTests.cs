using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HireYar.Tests;

public sealed class ComprehensiveReportCalculationTests
{
    private static (InterviewSession Session,ReportQuestion[] Questions,ReportPolicy Policy) Data(bool general=false)
    {
        var now=DateTimeOffset.UtcNow;var a=Guid.NewGuid();var b=Guid.NewGuid();
        var skills=new[]{new InterviewSkill(a,"A",true,20,3),new InterviewSkill(b,"B",true,80,3)};
        var snapshots=new[]{new InterviewQuestionSnapshot(Guid.NewGuid(),a,"easy A","A",1,true),new InterviewQuestionSnapshot(Guid.NewGuid(),a,"medium A","A",3,true),new InterviewQuestionSnapshot(Guid.NewGuid(),b,"hard B","B",5,true)};
        var session=new InterviewSession(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),snapshots,skills,3,1800,now,now.AddDays(1));
        if(general) session.SetRequiredCapability("hr_interview");session.Start(snapshots[0].QuestionId,now);session.Complete(InterviewCompletionReason.Manual,now.AddSeconds(1));
        var scores=new[]{100m,60m,20m};
        var questions=snapshots.Select((q,i)=> {
            var answerId=Guid.NewGuid();var evidence=new[]{new AnswerAssessmentEvidence(answerId,0,4,"test")};
            return new ReportQuestion(q.QuestionId,i+1,q.Text,q.Topic,q.Difficulty,now,"Processed",new(answerId,"Processed","test",null,now,10,null,null),[],new("Succeeded",scores[i],0.8m,"test","test",evidence,1,false,null,now,now,now,null,"answer-quality/2","Synthetic","synthetic"));
        }).ToArray();
        return(session,questions,SessionReportPolicyService.Read(session.Skills,null,true));
    }
    [Fact]
    public void Specialized_mean_does_not_overweight_adaptive_questions_and_fit_uses_target_level()
    {
        var d=Data();var report=ComprehensiveReportBuilder.Build(d.Session,d.Questions,d.Policy,[],true);
        Assert.Equal(32,report.Score.Value);Assert.Equal(28,report.Fit.Value);Assert.Equal(1,report.Coverage.Ratio);
        Assert.Single(report.Strengths.Findings!);Assert.Single(report.Improvements.Findings!);
        Assert.Equal("BelowConfiguredThreshold",report.Fit.Classification);Assert.Equal("NotApplicable",report.General.State);
        Assert.Contains(report.Specialized.Axes,a=>a.Topic=="A" && a.Value==80);Assert.Equal(4,report.Specialized.Metrics.Count);
    }
    [Fact]
    public void General_mean_is_equal_per_question_and_has_seven_evidence_limited_metrics()
    {
        var d=Data(true);var report=ComprehensiveReportBuilder.Build(d.Session,d.Questions,d.Policy,[],true);
        Assert.Equal(60,report.Score.Value);Assert.Equal("NotApplicable",report.Specialized.State);Assert.Equal(7,report.General.Metrics.Count);
        Assert.All(report.General.Metrics,m=> {Assert.Null(m.Value);Assert.Equal("InsufficientEvidence",m.State);});
    }
    [Fact]
    public void Missing_evidence_is_excluded_not_zero_and_missing_required_skills_block_a_fit_classification()
    {
        var d=Data();d.Questions[2]=d.Questions[2] with {Answer=null,AnswerState="Unanswered",Assessment=d.Questions[2].Assessment with {State="NotApplicable",Value=null,Confidence=null,Evidence=[]}};
        var report=ComprehensiveReportBuilder.Build(d.Session,d.Questions,d.Policy,[],true);
        Assert.Equal(80,report.Score.Value);Assert.True(report.Coverage.IsProvisional);Assert.Equal(0.67m,report.Coverage.Ratio);
        Assert.Equal("InsufficientCoverage",report.Fit.Classification);Assert.Equal(60,report.Fit.Value);Assert.Equal(0.2m,report.Fit.Coverage);
        Assert.Empty(report.Improvements.Findings!);Assert.Contains(report.Gaps,g=>g.Code=="required_skill_unobserved");Assert.Contains(report.Gaps,g=>g.Code=="answer_missing");
    }
    [Fact]
    public void Fingerprint_is_independent_of_viewer_and_history_but_changes_with_policy()
    {
        var d=Data();var first=ComprehensiveReportBuilder.Build(d.Session,d.Questions,d.Policy,[],true);
        var rows=new[]{new InterviewReviewEntry{OrganizationId=d.Session.OrganizationId,InterviewSessionId=d.Session.Id,RequestId=Guid.NewGuid(),ActorUserId=Guid.NewGuid(),Kind="review",RecordedAtUtc=DateTimeOffset.UtcNow,ReportFingerprint=first.Fingerprint,ReportSnapshotJson=JsonSerializer.Serialize(new ReportReviewSnapshot("comprehensive-report/1",first.Score,first.Coverage,first.Fit,d.Policy))}};
        var second=ComprehensiveReportBuilder.Build(d.Session,d.Questions,d.Policy with {CanEdit=false},rows,false);
        Assert.Equal(first.Fingerprint,second.Fingerprint);Assert.Equal("Reviewed",second.Review.State);Assert.False(second.Review.CanSubmit);
        var changed=ComprehensiveReportBuilder.Build(d.Session,d.Questions,d.Policy with {PassingScore=70},rows,true);
        Assert.NotEqual(first.Fingerprint,changed.Fingerprint);Assert.Equal("ReviewOutdated",changed.Review.State);
    }
    [Fact]
    public void One_invalid_dimension_does_not_invalidate_primary_or_other_dimensions()
    {
        var input=new AnswerAssessmentInput("role","specialized","question","topic",3,AnswerAssessmentProtocol.Excerpts("test"),"job criteria");
        var json="{\"verdict\":\"scored\",\"score\":80,\"confidence\":0.8,\"explanation\":\"test\",\"evidenceIds\":[1],\"dimensions\":[{\"code\":\"communication\",\"verdict\":\"scored\",\"score\":70,\"confidence\":0.7,\"explanation\":\"test\",\"evidenceIds\":[1]},{\"code\":\"knowledge_depth\",\"verdict\":\"scored\",\"score\":1000,\"confidence\":0.7,\"explanation\":\"test\",\"evidenceIds\":[1]}]}";
        Assert.Equal(80,AnswerAssessmentProtocol.Parse(json,Guid.NewGuid(),input.Excerpts).Score);
        var dimensions=AnswerDimensionProtocol.Parse(json,Guid.NewGuid(),input);
        Assert.Equal("Succeeded",dimensions.Single(d=>d.Code=="communication").State);Assert.Equal("Unavailable",dimensions.Single(d=>d.Code=="knowledge_depth").State);
        Assert.Equal("InsufficientEvidence",dimensions.Single(d=>d.Code=="job_alignment").State);
    }
}

public sealed partial class InterviewFlowTests
{
    private static AnswerAssessmentResponse WithDimensions(string type,int score=80)=>new(JsonSerializer.Serialize(new {
        verdict="scored",score,confidence=0.8,explanation="Synthetic observable answer evidence",evidenceIds=new[]{1},
        dimensions=AnswerDimensionProtocol.Catalog(type).Select(d=>new{code=d.Code,verdict="scored",score=75,confidence=0.7,explanation="Synthetic explicit statement",evidenceIds=new[]{1}})
    }),"Synthetic","synthetic-assessor");

    [PostgresFact]
    public async Task Comprehensive_report_has_every_section_actual_profiles_findings_and_versioned_human_review()
    {
        var x=await CompletedForAssessment(2);var index=0;f.AssessmentAi.Handler=(_,_)=>Task.FromResult(WithDimensions("specialized",Interlocked.Increment(ref index)==1?95:45));
        foreach(var answer in x.Answers)await Assess(f,x.Org,answer);
        var report=await NewReport(x.Hr,x.Session);Assert.Equal(11,report.Sections.Count);Assert.DoesNotContain(report.Sections,s=>s.State is "NotImplemented" or "NotStarted");
        Assert.False(report.Processing.HasUnimplementedSections);Assert.False(report.Processing.ShouldPoll);Assert.Equal(70,report.OverallScore.Value);
        Assert.Equal(3,report.SpecializedProfile.Axes.Count);Assert.Equal(4,report.SpecializedProfile.Metrics.Count);Assert.Contains(report.SpecializedProfile.Metrics,m=>m.Code=="communication" && m.Value==75);
        Assert.Single(report.Strengths.Findings!);Assert.Single(report.Improvements.Findings!);Assert.NotEmpty(report.Summary.Text!);
        Assert.Equal("AwaitingReview",report.HumanReview.State);Assert.True(report.HumanReview.CanSubmit);
        var request=new ReportDecisionRequest(Guid.NewGuid(),"review",report.Fingerprint,"Synthetic review note <script>alert(1)</script>");
        var response=await x.Hr.Client.Send(HttpMethod.Post,$"/api/interviews/{x.Session}/report/review",request);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var review=(await response.Content.ReadFromJsonAsync<ReportHumanReview>())!;Assert.Equal("Reviewed",review.State);Assert.Single(review.History);Assert.NotNull(review.Latest!.Snapshot);Assert.Equal(70,review.Latest.Snapshot!.Score.Value);
        Assert.Equal(HttpStatusCode.OK,(await x.Hr.Client.Send(HttpMethod.Post,$"/api/interviews/{x.Session}/report/review",request)).StatusCode);
        var update=new ReportPolicyRequest(report.Policy.Revision,90,95,0.8m,report.Policy.Weights);
        var policyResponse=await x.Hr.Client.Send(HttpMethod.Put,$"/api/interviews/{x.Session}/report/policy",update);Assert.Equal(HttpStatusCode.OK,policyResponse.StatusCode);
        var changed=await NewReport(x.Hr,x.Session);Assert.NotEqual(report.Fingerprint,changed.Fingerprint);Assert.Equal("ReviewOutdated",changed.HumanReview.State);
        Assert.Equal(HttpStatusCode.Conflict,(await x.Hr.Client.Send(HttpMethod.Put,$"/api/interviews/{x.Session}/report/policy",update)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await x.Hr.Client.Send(HttpMethod.Post,$"/api/interviews/{x.Session}/report/review",request with {RequestId=Guid.NewGuid()})).StatusCode);
        var html=await(await x.Hr.Client.Send(HttpMethod.Get,$"/api/interviews/{x.Session}/report/print")).Content.ReadAsStringAsync();
        foreach(var section in changed.Sections.Where(s=>s.State!="NotApplicable"))Assert.Contains($"data-section='{section.Code}'",html);
        Assert.Contains("&lt;script&gt;",html);Assert.DoesNotContain("<script>",html);Assert.DoesNotContain("NaN",html);Assert.Contains("<svg",html);Assert.DoesNotContain("fill-opacity='.2'",html);
        f.AssessmentAi.Reset();
    }
    [PostgresFact]
    public async Task Comprehensive_general_report_persists_seven_metrics_and_preserves_base_on_corrupt_policy_or_dimension()
    {
        f.AssessmentAi.Reset();var (hr,invite)=await Invite();await f.Db(async db=>{var session=await db.InterviewSessions.SingleAsync(s=>s.Id==invite.InterviewId);session.SetRequiredCapability("hr_interview");await db.SaveChangesAsync();});
        using var candidate=Candidate(invite);var state=await Start(candidate,invite.InterviewId);await Submit(candidate,invite.InterviewId,state.Question!.Id);await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete",null);
        var report=await NewReport(hr,invite.InterviewId);Guid org=default;await f.Db(async db=>org=await db.InterviewSessions.Where(s=>s.Id==invite.InterviewId).Select(s=>s.OrganizationId).SingleAsync());
        f.AssessmentAi.Handler=(_,_)=>Task.FromResult(WithDimensions("general"));await Assess(f,org,report.Questions[0].Answer!.AnswerId);report=await NewReport(hr,invite.InterviewId);
        Assert.Equal("NotApplicable",report.SpecializedProfile.State);Assert.Equal(7,report.GeneralProfile.Metrics.Count);Assert.All(report.GeneralProfile.Metrics,m=>Assert.Equal(75,m.Value));
        var policy=report.Policy;Assert.Equal(HttpStatusCode.OK,(await hr.Client.Send(HttpMethod.Put,$"/api/interviews/{invite.InterviewId}/report/policy",new ReportPolicyRequest(policy.Revision,60,80,0.6m,policy.Weights))).StatusCode);
        await f.Db(async db=> {var row=await db.SessionReportPolicies.SingleAsync(p=>p.InterviewSessionId==invite.InterviewId);row.WeightsJson="[{}]";var answer=await db.AnswerAssessments.SingleAsync(a=>a.InterviewSessionId==invite.InterviewId);answer.DimensionsJson="{}";await db.SaveChangesAsync();});
        report=await NewReport(hr,invite.InterviewId);Assert.Equal("BaseReady",report.State);Assert.Equal("Defaulted",report.Policy.State);Assert.Equal(80,report.OverallScore.Value);
        Assert.All(report.GeneralProfile.Metrics,m=>Assert.Null(m.Value));Assert.False(report.Processing.ShouldPoll);Assert.DoesNotContain(report.Sections,s=>s.State=="NotImplemented");f.AssessmentAi.Reset();
    }
    [PostgresFact]
    public async Task Report_policy_and_review_are_scoped_and_validate_revisions_weights_and_decisions()
    {
        var x=await CompletedForAssessment();var report=await NewReport(x.Hr,x.Session);var other=await f.RegisterOwner();var url=$"/api/interviews/{x.Session}/report/policy";
        var valid=new ReportPolicyRequest(Guid.Empty,60,80,0.6m,report.Policy.Weights);
        Assert.Equal(HttpStatusCode.NotFound,(await other.Client.Send(HttpMethod.Put,url,valid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await x.Hr.Client.Send(HttpMethod.Put,url,valid with {StrongScore=50})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await x.Hr.Client.Send(HttpMethod.Put,url,valid with {Weights=[]})).StatusCode);
        var decision=new ReportDecisionRequest(Guid.NewGuid(),"review",report.Fingerprint);
        Assert.Equal(HttpStatusCode.NotFound,(await other.Client.Send(HttpMethod.Post,$"/api/interviews/{x.Session}/report/review",decision)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await x.Hr.Client.Send(HttpMethod.Post,$"/api/interviews/{x.Session}/report/review",decision with {Decision="hire_automatically"})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await x.Hr.Client.Send(HttpMethod.Post,$"/api/interviews/{x.Session}/report/review",decision with {ReportFingerprint=new string('A',64)})).StatusCode);
    }
    [PostgresFact]
    public async Task Complete_report_migration_is_idempotent_and_refuses_to_erase_review_audit()
    {
        var isolated = new ApiFixture(); await isolated.InitializeAsync();
        try
        {
            var flow = new InterviewFlowTests(isolated);
            var x = await flow.CompletedForAssessment();
            var report = await NewReport(x.Hr, x.Session);
            string reportMigration = "", previousMigration = "";
            await isolated.Db(async db =>
            {
                var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
                var reportIndex = Array.FindIndex(migrations, m => m.EndsWith("_CompleteSessionReports", StringComparison.Ordinal));
                Assert.True(reportIndex > 0);
                reportMigration = migrations[reportIndex]; previousMigration = migrations[reportIndex - 1];
                var migrator = db.GetService<IMigrator>();
                await migrator.MigrateAsync(previousMigration);
                var script = migrator.GenerateScript(previousMigration, reportMigration, MigrationsSqlGenerationOptions.Idempotent);
                await db.Database.ExecuteSqlRawAsync(script);
                await db.Database.ExecuteSqlRawAsync(script);
                // Restore later migrations before exercising the current API/model.
                await db.Database.MigrateAsync();
                Assert.False(db.Database.HasPendingModelChanges());
                Assert.True(await db.InterviewSessions.AnyAsync(s => s.Id == x.Session));
            });
            Assert.Equal(HttpStatusCode.OK, (await x.Hr.Client.Send(HttpMethod.Post, $"/api/interviews/{x.Session}/report/review",
                new ReportDecisionRequest(Guid.NewGuid(), "review", report.Fingerprint))).StatusCode);
            await isolated.Db(async db =>
            {
                var error = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(previousMigration));
                Assert.Equal("P0001", error.SqlState);
                Assert.Contains(reportMigration, await db.Database.GetAppliedMigrationsAsync());
                await db.Database.MigrateAsync();
            });
            Assert.Equal("Reviewed", (await NewReport(x.Hr, x.Session)).HumanReview.State);
        }
        finally { await ((IAsyncLifetime)isolated).DisposeAsync(); }
    }

}
