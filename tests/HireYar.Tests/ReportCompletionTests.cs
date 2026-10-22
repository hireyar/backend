using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace HireYar.Tests;

public sealed class ReportNarrativeProtocolTests
{
    private static ReportNarrativeInput Input()=>new("Synthetic role","Synthetic job criteria",
        [new(1,"answer",Guid.NewGuid(),0,"Synthetic work example"),new(2,"resume",null,0,"من چهار سال سابقه توسعه نرم‌افزار دارم.")],[]);
    [Fact]
    public void Narrative_evidence_and_experience_are_reconstructed_from_exact_server_sources()
    {
        var input=Input();var validated=ReportNarrativeProtocol.Validate(FakeReportNarrativeProvider.Valid(input).Json,input);
        var row=new SessionReportNarrative {State="Succeeded",Attempts=1,ResultJson=validated};
        var result=ReportNarrativeProtocol.View(row,input,"synthetic",DateTimeOffset.UtcNow,true);
        Assert.Equal("Available",result.State);Assert.Single(result.Summary!.Evidence);
        Assert.Equal(input.Sources[1],Assert.Single(result.Experience.Statements));Assert.Equal("SelfReported",result.Experience.State);
        Assert.Single(result.FollowUpQuestions);Assert.False(result.CanGenerate);
    }
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"summary\":{\"text\":\"fake\",\"evidenceIds\":[99]},\"nextStep\":null,\"followUpQuestions\":[],\"experienceSourceIds\":[]}")]
    [InlineData("{\"summary\":{\"text\":\"fake\",\"evidenceIds\":[2]},\"nextStep\":null,\"followUpQuestions\":[],\"experienceSourceIds\":[]}")]
    [InlineData("{\"summary\":null,\"nextStep\":null,\"followUpQuestions\":[],\"experienceSourceIds\":[99]}")]
    [InlineData("{\"summary\":null,\"nextStep\":null,\"followUpQuestions\":[],\"experienceSourceIds\":[],\"years\":4}")]
    public void Forged_missing_or_extra_data_is_rejected(string json) => Assert.Equal("narrative_invalid_output",
        Assert.Throws<InterviewProviderException>(()=>ReportNarrativeProtocol.Validate(json,Input())).Code);
    [Fact]
    public void Missing_dimensions_and_explicit_insufficient_evidence_remain_distinct()
    {
        var input=new AnswerAssessmentInput("Role","specialized","Q","topic",3,AnswerAssessmentProtocol.Excerpts("test"),"criteria");
        var missing=AnswerDimensionProtocol.Parse(FakeAnswerAssessmentProvider.Valid().Json,Guid.NewGuid(),input);
        Assert.All(missing,d=>Assert.Equal("dimension_not_returned",d.FailureCode));
        var explicitUnknown=AnswerDimensionProtocol.Parse("{\"dimensions\":[{\"code\":\"communication\",\"verdict\":\"insufficient_evidence\",\"score\":null,\"confidence\":null,\"explanation\":\"No structural evidence\",\"evidenceIds\":[]}]}",Guid.NewGuid(),input);
        var communication=explicitUnknown.Single(d=>d.Code=="communication");
        Assert.Equal("dimension_insufficient_evidence",communication.FailureCode);Assert.Null(communication.Value);
    }
}

public sealed partial class InterviewFlowTests
{
    private static string DimensionUrl(Guid session,Guid answer)=>$"/api/interviews/{session}/report/answers/{answer}/dimensions/retry";
    private static string NarrativeUrl(Guid session)=>$"/api/interviews/{session}/report/narrative";

    [PostgresFact]
    public async Task Missing_dimensions_have_actionable_reasons_and_recovery_preserves_primary_score_and_attempt_budget()
    {
        var x=await CompletedForAssessment();await Assess(f,x.Org,x.Answers[0]);
        var before=await NewReport(x.Hr,x.Session);
        Assert.All(before.SpecializedProfile.Metrics.Where(m=>m.Code!="performance"),m=> {
            Assert.Null(m.Value);Assert.Equal("dimension_not_returned",Assert.Single(m.Issues!,i=>i.QuestionId==before.Questions[0].QuestionId).ReasonCode);
        });
        Assert.True(before.Questions[0].Assessment.DimensionRecovery!.CanRetry);
        f.AssessmentAi.Handler=(_,_)=>Task.FromResult(WithDimensions("specialized",10));
        var response=await x.Hr.Client.Send(HttpMethod.Post,DimensionUrl(x.Session,x.Answers[0]));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var after=await NewReport(x.Hr,x.Session);
        Assert.Equal(before.OverallScore.Value,after.OverallScore.Value);Assert.Equal(80,after.Questions[0].Assessment.Value);
        Assert.Equal(1,after.Questions[0].Assessment.Attempts);Assert.Equal(1,after.Questions[0].Assessment.DimensionRecovery!.Attempts);
        Assert.All(after.SpecializedProfile.Metrics.Where(m=>m.Code!="performance"),m=>Assert.Equal(75,m.Value));
        Assert.False(after.Questions[0].Assessment.DimensionRecovery!.CanRetry);
        Assert.Equal(HttpStatusCode.Conflict,(await x.Hr.Client.Send(HttpMethod.Post,DimensionUrl(x.Session,x.Answers[0]))).StatusCode);
        Assert.Equal(2,f.AssessmentAi.Calls);f.AssessmentAi.Reset();
    }
    [PostgresFact]
    public async Task Dimension_recovery_fences_concurrent_calls_and_invalid_outputs_have_a_separate_three_attempt_budget()
    {
        var x=await CompletedForAssessment();await Assess(f,x.Org,x.Answers[0]);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource<AnswerAssessmentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.AssessmentAi.Handler=(_,_)=>{entered.TrySetResult();return release.Task;};
        var first=x.Hr.Client.Send(HttpMethod.Post,DimensionUrl(x.Session,x.Answers[0]));await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try {Assert.Equal(HttpStatusCode.OK,(await x.Hr.Client.Send(HttpMethod.Post,DimensionUrl(x.Session,x.Answers[0]))).StatusCode);}
        finally {release.TrySetResult(new("not-json","Synthetic","synthetic"));}
        Assert.Equal(HttpStatusCode.OK,(await first).StatusCode);
        f.AssessmentAi.Handler=(_,_)=>Task.FromResult(new AnswerAssessmentResponse("not-json","Synthetic","synthetic"));
        for(var i=0;i<2;i++)Assert.Equal(HttpStatusCode.OK,(await x.Hr.Client.Send(HttpMethod.Post,DimensionUrl(x.Session,x.Answers[0]))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await x.Hr.Client.Send(HttpMethod.Post,DimensionUrl(x.Session,x.Answers[0]))).StatusCode);
        var after=await NewReport(x.Hr,x.Session);Assert.Equal(80,after.OverallScore.Value);Assert.Equal(1,after.Questions[0].Assessment.Attempts);
        Assert.Equal(3,after.Questions[0].Assessment.DimensionRecovery!.Attempts);Assert.False(after.Questions[0].Assessment.DimensionRecovery!.CanRetry);f.AssessmentAi.Reset();
    }
    [PostgresFact]
    public async Task Narrative_is_explicit_cached_evidence_bound_and_does_not_change_scores_or_human_decisions()
    {
        f.NarrativeAi.Reset();var x=await CompletedForAssessment();await Assess(f,x.Org,x.Answers[0]);
        var before=await NewReport(x.Hr,x.Session);Assert.Equal("NotGenerated",before.Narrative!.State);Assert.Equal(0,f.NarrativeAi.Calls);
        Assert.NotNull(before.PositionFit);Assert.NotNull(before.ExpectedLevel);Assert.NotEmpty(before.ExpectedLevel.Answers);
        var body=new GenerateReportNarrativeRequest(before.Fingerprint);
        var response=await x.Hr.Client.Send(HttpMethod.Post,NarrativeUrl(x.Session),body);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var generated=(await response.Content.ReadFromJsonAsync<ReportNarrative>())!;Assert.Equal("Available",generated.State);
        Assert.Single(generated.Summary!.Evidence);Assert.Equal(x.Answers[0],generated.Summary.Evidence[0].AnswerId);Assert.NotNull(generated.NextStep);Assert.Single(generated.FollowUpQuestions);
        Assert.Equal(HttpStatusCode.OK,(await x.Hr.Client.Send(HttpMethod.Post,NarrativeUrl(x.Session),body)).StatusCode);Assert.Equal(1,f.NarrativeAi.Calls);
        var after=await NewReport(x.Hr,x.Session);Assert.Equal(before.OverallScore.Value,after.OverallScore.Value);Assert.NotEqual(before.Fingerprint,after.Fingerprint);Assert.Null(after.HumanReview.Latest);
        var html=await(await x.Hr.Client.Send(HttpMethod.Get,$"/api/interviews/{x.Session}/report/print")).Content.ReadAsStringAsync();
        Assert.Contains("&lt;script&gt;untrusted&lt;/script&gt;",html);Assert.DoesNotContain("<script>untrusted",html);Assert.Contains("سؤال‌های پیشنهادی",html);
        await f.Db(async db=>Assert.Single(await db.SessionReportNarratives.Where(n=>n.InterviewSessionId==x.Session).ToArrayAsync()));f.NarrativeAi.Reset();
    }
    [PostgresFact]
    public async Task Narrative_scope_fingerprint_failure_budget_and_read_only_GET_are_enforced()
    {
        f.NarrativeAi.Reset();var x=await CompletedForAssessment();var report=await NewReport(x.Hr,x.Session);var other=await f.RegisterOwner();
        var body=new GenerateReportNarrativeRequest(report.Fingerprint);
        Assert.Equal(HttpStatusCode.NotFound,(await other.Client.Send(HttpMethod.Post,NarrativeUrl(x.Session),body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Browser().Send(HttpMethod.Post,NarrativeUrl(x.Session),body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await x.Hr.Client.Send(HttpMethod.Post,NarrativeUrl(x.Session),body with {ReportFingerprint=new string('A',64)})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await other.Client.Send(HttpMethod.Post,DimensionUrl(x.Session,x.Answers[0]))).StatusCode);
        f.NarrativeAi.Handler=(_,_)=>Task.FromResult(new AnswerAssessmentResponse("not-json","Synthetic","synthetic"));
        for(var i=0;i<3;i++) {Assert.Equal(HttpStatusCode.OK,(await x.Hr.Client.Send(HttpMethod.Post,NarrativeUrl(x.Session),body)).StatusCode);Assert.Equal("Failed",(await NewReport(x.Hr,x.Session)).Narrative!.State);}
        Assert.Equal(HttpStatusCode.Conflict,(await x.Hr.Client.Send(HttpMethod.Post,NarrativeUrl(x.Session),body)).StatusCode);
        Assert.Equal(3,f.NarrativeAi.Calls);Assert.Null((await NewReport(x.Hr,x.Session)).OverallScore.Value);f.NarrativeAi.Reset();
    }
    [PostgresFact]
    public async Task Late_narrative_cannot_overwrite_new_attempt_or_attach_to_changed_assessment()
    {
        f.NarrativeAi.Reset();var x=await CompletedForAssessment();var report=await NewReport(x.Hr,x.Session);var body=new GenerateReportNarrativeRequest(report.Fingerprint);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource<AnswerAssessmentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        ReportNarrativeInput? claimedInput=null;
        f.NarrativeAi.Handler=(input,_)=>{claimedInput=input;entered.TrySetResult();return release.Task;};
        var pending=x.Hr.Client.Send(HttpMethod.Post,NarrativeUrl(x.Session),body);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var original=f.Clock.Offset;
        try {
            f.Clock.Offset+=TimeSpan.FromMinutes(3);f.NarrativeAi.Handler=null;
            Assert.Equal(HttpStatusCode.OK,(await x.Hr.Client.Send(HttpMethod.Post,NarrativeUrl(x.Session),body)).StatusCode);
            release.TrySetResult(FakeReportNarrativeProvider.Valid(claimedInput!));await pending;
            Assert.Equal(2,(await NewReport(x.Hr,x.Session)).Narrative!.Attempts);
            await Assess(f,x.Org,x.Answers[0]);
            var changed=await NewReport(x.Hr,x.Session);Assert.NotEqual(report.Fingerprint,changed.Fingerprint);Assert.Equal("NotGenerated",changed.Narrative!.State);
            Assert.Equal(HttpStatusCode.Conflict,(await x.Hr.Client.Send(HttpMethod.Post,NarrativeUrl(x.Session),body)).StatusCode);
        } finally {f.Clock.Offset=original;release.TrySetResult(FakeReportNarrativeProvider.Valid(claimedInput!));await pending;f.NarrativeAi.Reset();}
    }
    [PostgresFact]
    public async Task Report_completion_migration_is_idempotent_and_protects_narrative_and_dimension_audit()
    {
        var isolated=new ApiFixture();await isolated.InitializeAsync();
        try {
            var flow=new InterviewFlowTests(isolated);var x=await flow.CompletedForAssessment();var report=await NewReport(x.Hr,x.Session);
            await isolated.Db(async db=> {
                var migrations=(await db.Database.GetAppliedMigrationsAsync()).ToArray();var latest=migrations.Single(m=>m.EndsWith("_CompleteReportEvidenceAndNarrative"));var previous=migrations[Array.IndexOf(migrations,latest)-1];
                var script=db.GetService<IMigrator>().GenerateScript(previous,latest,MigrationsSqlGenerationOptions.Idempotent);
                await db.Database.ExecuteSqlRawAsync(script);await db.Database.ExecuteSqlRawAsync(script);await db.GetService<IMigrator>().MigrateAsync(migrations[^1]);Assert.False(db.Database.HasPendingModelChanges());
            });
            Assert.Equal(HttpStatusCode.OK,(await x.Hr.Client.Send(HttpMethod.Post,NarrativeUrl(x.Session),new GenerateReportNarrativeRequest(report.Fingerprint))).StatusCode);
            await isolated.Db(async db=> {
                var migrations=(await db.Database.GetAppliedMigrationsAsync()).ToArray();
                var error=await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(migrations[Array.FindIndex(migrations,m=>m.EndsWith("_CompleteReportEvidenceAndNarrative"))-1]));Assert.Equal("P0001",error.SqlState);
            });
        } finally {await ((IAsyncLifetime)isolated).DisposeAsync();}
    }
}
public sealed partial class InterviewFlowTests
{
    [PostgresFact]
    public async Task Narrative_changes_require_new_human_review_and_snapshot_keeps_the_prose()
    {
        f.NarrativeAi.Reset();var x=await CompletedForAssessment();var report=await NewReport(x.Hr,x.Session);
        var decision=new ReportDecisionRequest(Guid.NewGuid(),"review",report.Fingerprint);
        Assert.Equal(HttpStatusCode.OK,(await x.Hr.Client.Send(HttpMethod.Post,$"/api/interviews/{x.Session}/report/review",decision)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await x.Hr.Client.Send(HttpMethod.Post,NarrativeUrl(x.Session),new GenerateReportNarrativeRequest(report.Fingerprint))).StatusCode);
        var changed=await NewReport(x.Hr,x.Session);Assert.Equal("ReviewOutdated",changed.HumanReview.State);
        var filtered=await x.Hr.Client.Send(HttpMethod.Get,"/api/interviews?humanDecisions=review");
        Assert.Equal(HttpStatusCode.OK,filtered.StatusCode);
        var listed=(await filtered.Content.ReadFromJsonAsync<HireYar.Application.Common.Page<PanelInterviewDto>>())!;
        Assert.Equal(x.Session,Assert.Single(listed.Items).Id);Assert.Equal("review",listed.Items[0].HumanDecision);
        Assert.Equal(HttpStatusCode.Conflict,(await x.Hr.Client.Send(HttpMethod.Post,$"/api/interviews/{x.Session}/report/review",decision with {RequestId=Guid.NewGuid()})).StatusCode);
        var response=await x.Hr.Client.Send(HttpMethod.Post,$"/api/interviews/{x.Session}/report/review",decision with {RequestId=Guid.NewGuid(),ReportFingerprint=changed.Fingerprint});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var current=await NewReport(x.Hr,x.Session);Assert.Equal("Reviewed",current.HumanReview.State);Assert.NotNull(current.HumanReview.Latest!.Snapshot!.Narrative!.Summary);f.NarrativeAi.Reset();
    }
    [PostgresFact]
    public async Task Recruiter_reads_completion_data_but_cannot_generate_narrative_or_repair_dimensions()
    {
        var x=await CompletedForAssessment();await Assess(f,x.Org,x.Answers[0]);
        var mobile="09"+Random.Shared.NextInt64(100_000_000,1_000_000_000).ToString("D9");
        var invitation=await x.Hr.Client.Send(HttpMethod.Post,"/api/team/invitations",new HireYar.Application.Organizations.InviteTeamMemberRequest(mobile,"نام","آزمایشی", Email: "invite@example.test"));
        Assert.Equal(HttpStatusCode.Created,invitation.StatusCode);
        var invited=(await invitation.Content.ReadFromJsonAsync<HireYar.Application.Organizations.TeamInvitationDto>())!;
        var member=f.Browser();Assert.Equal(HttpStatusCode.OK,(await member.Send(HttpMethod.Post,"/api/auth/otp/request",new HireYar.Application.Auth.RequestOtpRequest(mobile))).StatusCode);
        var accepted=await member.Send(HttpMethod.Post,$"/api/auth/team-invitations/{invited.Id}/accept",new HireYar.Application.Auth.VerifyOtpRequest(mobile,f.PhoneOtp.Codes[mobile]));
        Assert.Equal(HttpStatusCode.OK,accepted.StatusCode);member.AccessToken=(await accepted.Content.ReadFromJsonAsync<HireYar.Application.Auth.AccessResponse>())!.AccessToken;
        var reportResponse=await member.Send(HttpMethod.Get,$"/api/interviews/{x.Session}/report");Assert.Equal(HttpStatusCode.OK,reportResponse.StatusCode);
        var report=(await reportResponse.Content.ReadFromJsonAsync<SessionReportDocument>())!;
        Assert.Equal((await NewReport(x.Hr,x.Session)).Fingerprint,report.Fingerprint);Assert.NotNull(report.PositionFit);Assert.False(report.Narrative!.CanGenerate);Assert.False(report.Questions[0].Assessment.DimensionRecovery!.CanRetry);
        Assert.Equal(HttpStatusCode.Forbidden,(await member.Send(HttpMethod.Post,NarrativeUrl(x.Session),new GenerateReportNarrativeRequest(report.Fingerprint))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await member.Send(HttpMethod.Post,DimensionUrl(x.Session,x.Answers[0]))).StatusCode);
        f.NarrativeAi.Reset();
        Assert.Equal(HttpStatusCode.OK,(await x.Hr.Client.Send(HttpMethod.Post,NarrativeUrl(x.Session),new GenerateReportNarrativeRequest(report.Fingerprint))).StatusCode);
        var owner=await NewReport(x.Hr,x.Session);
        var memberReport=(await(await member.Send(HttpMethod.Get,$"/api/interviews/{x.Session}/report")).Content.ReadFromJsonAsync<SessionReportDocument>())!;
        Assert.Equal(owner.Fingerprint,memberReport.Fingerprint);Assert.Equal("Available",memberReport.Narrative!.State);Assert.False(memberReport.Narrative.CanGenerate);f.NarrativeAi.Reset();
    }
    [PostgresFact]
    public async Task Provider_payment_failure_explains_missing_metrics_without_inventing_scores()
    {
        var x=await CompletedForAssessment();f.AssessmentAi.Handler=(_,_)=>throw new InterviewProviderException("answer_assessment_http_402");
        await Assess(f,x.Org,x.Answers[0]);var report=await NewReport(x.Hr,x.Session);
        Assert.Equal("assessment_provider_payment_required",report.Questions[0].Assessment.FailureCode);Assert.Null(report.OverallScore.Value);
        Assert.All(report.SpecializedProfile.Metrics.Where(m=>m.Code!="performance"),m=>Assert.Contains(m.Issues!,i=>i.ReasonCode=="assessment_provider_payment_required"));f.AssessmentAi.Reset();
    }
    [PostgresFact]
    public async Task Narrative_input_changed_during_generation_is_not_committed_to_the_new_report()
    {
        f.NarrativeAi.Reset();var x=await CompletedForAssessment();var report=await NewReport(x.Hr,x.Session);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource<AnswerAssessmentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        ReportNarrativeInput? input=null;f.NarrativeAi.Handler=(value,_)=>{input=value;entered.TrySetResult();return release.Task;};
        var running=x.Hr.Client.Send(HttpMethod.Post,NarrativeUrl(x.Session),new GenerateReportNarrativeRequest(report.Fingerprint));await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try {await Assess(f,x.Org,x.Answers[0]);} finally {release.TrySetResult(FakeReportNarrativeProvider.Valid(input!));}
        Assert.Equal(HttpStatusCode.OK,(await running).StatusCode);
        Assert.Equal("NotGenerated",(await NewReport(x.Hr,x.Session)).Narrative!.State);
        await f.Db(async db=> {var old=await db.SessionReportNarratives.SingleAsync(n=>n.InterviewSessionId==x.Session);Assert.Equal("Failed",old.State);Assert.Equal("narrative_input_changed",old.FailureCode);});f.NarrativeAi.Reset();
    }
}

public sealed partial class InterviewFlowTests
{
    [PostgresFact]
    public async Task Dimension_repair_keeps_successful_dimensions_and_records_explicit_insufficient_evidence()
    {
        var x=await CompletedForAssessment();
        f.AssessmentAi.Handler=(_,_)=>Task.FromResult(new AnswerAssessmentResponse("{\"verdict\":\"scored\",\"score\":80,\"confidence\":0.8,\"explanation\":\"Synthetic primary\",\"evidenceIds\":[1],\"dimensions\":[{\"code\":\"communication\",\"verdict\":\"scored\",\"score\":15,\"confidence\":0.8,\"explanation\":\"Synthetic dimension\",\"evidenceIds\":[1]}]}","Synthetic","synthetic"));
        await Assess(f,x.Org,x.Answers[0]);
        f.AssessmentAi.Handler=(input,_)=> {
            Assert.DoesNotContain(input.Dimensions!,d=>d.Code=="communication");
            return Task.FromResult(new AnswerAssessmentResponse(JsonSerializer.Serialize(new {dimensions=input.Dimensions!.Select(d=>new{code=d.Code,verdict="insufficient_evidence",score=(decimal?)null,confidence=(decimal?)null,explanation="No sufficient evidence for this dimension",evidenceIds=Array.Empty<int>()})}),"Synthetic","synthetic"));
        };
        Assert.Equal(HttpStatusCode.OK,(await x.Hr.Client.Send(HttpMethod.Post,DimensionUrl(x.Session,x.Answers[0]))).StatusCode);
        var report=await NewReport(x.Hr,x.Session);Assert.Equal(80,report.OverallScore.Value);
        var assessment=report.Questions[0].Assessment;Assert.Equal(15,assessment.Dimensions!.Single(d=>d.Code=="communication").Value);
        Assert.All(assessment.Dimensions!.Where(d=>d.Code!="communication"),d=>{Assert.Null(d.Value);Assert.Equal("dimension_insufficient_evidence",d.FailureCode);});
        Assert.False(assessment.DimensionRecovery!.CanRetry);Assert.Equal("Completed",assessment.DimensionRecovery.State);f.AssessmentAi.Reset();
    }
}

public sealed partial class InterviewFlowTests
{
    [PostgresFact]
    public async Task Both_report_types_use_real_metrics_narrative_and_exact_self_reported_experience()
    {
        foreach(var type in new[]{"general","specialized"}) {
            f.NarrativeAi.Reset();var (hr,invite)=await Invite();
            const string experience="من چهار سال سابقه توسعه نرم‌افزار دارم.";
            await f.Db(async db=> {
                var session=await db.InterviewSessions.SingleAsync(s=>s.Id==invite.InterviewId);
                if(type=="general") session.SetInterviewType("general");
                session.SetAiSnapshot(session.MasterPromptSnapshot??"Synthetic prompt",session.MasterPromptVersion??"synthetic",experience,"fa-IR",false,"",false,"");await db.SaveChangesAsync();
            });
            using var candidate=Candidate(invite);var state=await Start(candidate,invite.InterviewId);await Submit(candidate,invite.InterviewId,state.Question!.Id);await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete",null);
            var before=await NewReport(hr,invite.InterviewId);Guid org=default;
            await f.Db(async db=>org=await db.InterviewSessions.Where(s=>s.Id==invite.InterviewId).Select(s=>s.OrganizationId).SingleAsync());
            f.AssessmentAi.Handler=(_,_)=>Task.FromResult(WithDimensions(type));await Assess(f,org,before.Questions[0].Answer!.AnswerId);
            var report=await NewReport(hr,invite.InterviewId);
            Assert.Equal(HttpStatusCode.OK,(await hr.Client.Send(HttpMethod.Post,NarrativeUrl(invite.InterviewId),new GenerateReportNarrativeRequest(report.Fingerprint))).StatusCode);
            report=await NewReport(hr,invite.InterviewId);
            var profile=type=="general"?report.GeneralProfile:report.SpecializedProfile;
            Assert.Equal(type=="general"?7:4,profile.Metrics.Count);Assert.All(profile.Metrics,m=>Assert.NotNull(m.Value));
            Assert.Equal("Available",report.Narrative!.State);Assert.Equal(experience,Assert.Single(report.Narrative.Experience.Statements).Text);
            var html=HireYar.Api.Controllers.SessionReportPage.Render(report);Assert.Contains(experience,html);Assert.Contains("سابقه اعلام‌شده",html);if(type=="specialized") Assert.Contains("تناسب کلی با موقعیت",html);
            var directory=Environment.GetEnvironmentVariable("HIREYAR_REPORT_PREVIEW_DIRECTORY");
            if(!string.IsNullOrWhiteSpace(directory)) {
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(Path.Combine(directory,$"{type}.html"),html);
                await File.WriteAllTextAsync(Path.Combine(directory,$"{type}.json"),await (await hr.Client.Send(HttpMethod.Get,$"/api/interviews/{invite.InterviewId}/report")).Content.ReadAsStringAsync());
            }
            f.AssessmentAi.Reset();f.NarrativeAi.Reset();
        }
    }
}
