using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace HireYar.Tests;

public sealed partial class InterviewFlowTests
{
    private static async Task<SessionReportDocument> NewReport(Account account, Guid id) =>
        (await (await account.Client.Send(HttpMethod.Get, $"/api/interviews/{id}/report")).Content.ReadFromJsonAsync<SessionReportDocument>())!;

    [PostgresFact]
    public async Task New_report_is_immediately_readable_from_facts_without_AI_generation_or_fake_scores()
    {
        f.InterviewAi.Reset();var (hr,invite)=await Invite();using var candidate=Candidate(invite);
        var initial=await NewReport(hr,invite.InterviewId);
        Assert.Equal("InterviewIncomplete",initial.State);Assert.Empty(initial.Questions);Assert.Equal("Unknown",initial.Signals.ConsentState);
        var state=await Start(candidate,invite.InterviewId);await Submit(candidate,invite.InterviewId,state.Question!.Id);
        await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete",null);
        var calls=f.InterviewAi.LlmCalls;
        f.InterviewAi.FailLlm=f.InterviewAi.FailStt=f.InterviewAi.FailTts=true;
        try {
            var report=await NewReport(hr,invite.InterviewId);
            Assert.Equal("session-report/1",report.SchemaVersion);Assert.Equal("BaseReady",report.State);
            Assert.Equal(12,report.Sections.Count);Assert.Equal(3,report.Statistics.BankQuestions);
            Assert.Equal(2,report.Questions.Count);Assert.Equal(1,report.Statistics.SubmittedAnswers);
            Assert.Equal(1,report.Statistics.ProcessedAnswers);Assert.Equal(1,report.Statistics.UnansweredQuestions);
            Assert.Equal("InsufficientEvidence",report.OverallScore.State);Assert.Null(report.OverallScore.Value);Assert.NotNull(report.Summary.Text);
            Assert.False(report.Processing.HasUnimplementedSections);
            Assert.All(report.Questions,q=>Assert.Null(q.Assessment.Value));Assert.Contains(report.Questions,q=>q.AnswerState=="Unanswered");
            Assert.Equal("NotApplicable",report.Sections.Single(s=>s.Code=="generalProfile").State);
            var first=await(await hr.Client.Send(HttpMethod.Get,$"/api/interviews/{invite.InterviewId}/report")).Content.ReadAsStringAsync();
            var second=await(await hr.Client.Send(HttpMethod.Get,$"/api/interviews/{invite.InterviewId}/report")).Content.ReadAsStringAsync();
            Assert.Equal(first,second);Assert.Equal(calls,f.InterviewAi.LlmCalls);
            Assert.DoesNotContain("storageKey",first,StringComparison.OrdinalIgnoreCase);
            await f.Db(async db=>Assert.False(db.Database.HasPendingModelChanges()));
        }
        finally { f.InterviewAi.Reset(); }
    }

    [PostgresFact]
    public async Task New_report_scope_no_store_and_retired_API_are_enforced()
    {
        var (hr,invite)=await Invite();var other=await f.RegisterOwner();using var candidate=Candidate(invite);
        var url=$"/api/interviews/{invite.InterviewId}/report";
        var ok=await hr.Client.Send(HttpMethod.Get,url);Assert.Equal(HttpStatusCode.OK,ok.StatusCode);
        Assert.True(ok.Headers.CacheControl!.NoStore);Assert.Equal(HttpStatusCode.NotFound,(await other.Client.Send(HttpMethod.Get,url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await candidate.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Browser().Send(HttpMethod.Get,url)).StatusCode);
        var old=$"/api/interviews/{invite.InterviewId}/evaluation-report";
        Assert.Equal(HttpStatusCode.Gone,(await hr.Client.Send(HttpMethod.Get,old)).StatusCode);
        Assert.Equal(HttpStatusCode.Gone,(await hr.Client.Send(HttpMethod.Post,old)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await other.Client.Send(HttpMethod.Get,old)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await hr.Client.Send(HttpMethod.Delete,$"/api/interviews/{invite.InterviewId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await hr.Client.Send(HttpMethod.Get,url)).StatusCode);
    }

    [PostgresFact]
    public async Task Failed_transcription_remains_visible_in_base_report_and_does_not_turn_into_a_score()
    {
        f.InterviewAi.Reset();var (hr,invite)=await Invite();using var candidate=Candidate(invite);var state=await Start(candidate,invite.InterviewId);
        try {
            f.InterviewAi.FailStt=true;await Submit(candidate,invite.InterviewId,state.Question!.Id);
            var report=await NewReport(hr,invite.InterviewId);
            Assert.Single(report.Questions);Assert.NotNull(report.Questions[0].Answer);
            Assert.Equal("Failed",report.Questions[0].AnswerState);Assert.Null(report.Questions[0].Answer!.Transcript);
            Assert.Equal(1,report.Statistics.FailedAnswers);Assert.Equal(1,report.Statistics.SubmittedAnswers);
            Assert.Equal(0,report.Statistics.ProcessedAnswers);Assert.Null(report.OverallScore.Value);
        } finally { f.InterviewAi.Reset(); }
    }

    [PostgresFact]
    public async Task Malformed_recording_timeline_is_isolated_and_media_has_no_package_expiry()
    {
        var (hr,invite)=await Invite();using var candidate=Candidate(invite);var state=await Start(candidate,invite.InterviewId);
        await Submit(candidate,invite.InterviewId,state.Question!.Id);await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete",null);
        await f.Db(async db=> {
            var org=await db.InterviewSessions.Where(s=>s.Id==invite.InterviewId).Select(s=>s.OrganizationId).SingleAsync();
            db.InterviewSessionRecordings.Add(new(){InterviewSessionId=invite.InterviewId,OrganizationId=org,UploadId=Guid.NewGuid(),StorageKey="synthetic-private-unavailable",ContentType="video/webm",SizeBytes=16,DurationSeconds=10,TimelineJson="{}",UploadedAtUtc=DateTimeOffset.UtcNow});
            await db.SaveChangesAsync();
        });
        var report=await NewReport(hr,invite.InterviewId);Assert.Equal("BaseReady",report.State);
        Assert.Equal("Unavailable",report.Media.Recording!.TimelineState);Assert.Empty(report.Media.Recording.Timeline);
        Assert.Equal($"/api/interviews/{invite.InterviewId}/recording/video",report.Media.Recording.VideoUrl);
        var original=f.Clock.Offset;
        try {
            f.Clock.Offset+=TimeSpan.FromDays(400);report=await NewReport(hr,invite.InterviewId);
            Assert.Equal("BaseReady",report.State);Assert.Equal("UploadedUnverified",report.Media.State);Assert.NotNull(report.Media.Recording!.VideoUrl);
            Assert.All(report.Questions.Where(q=>q.Answer is not null),q=> Assert.NotNull(q.Answer!.AudioUrl));
        } finally { f.Clock.Offset=original; }
    }

    [PostgresFact]
    public async Task New_print_is_independent_of_analysis_and_escapes_untrusted_interview_text()
    {
        var (hr,invite)=await Invite();using var candidate=Candidate(invite);var state=await Start(candidate,invite.InterviewId);
        f.InterviewAi.SttText="<script>alert('x')</script><img src=x onerror=alert(1)>";
        try {await Submit(candidate,invite.InterviewId,state.Question!.Id);} finally {f.InterviewAi.Reset();}
        await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete",null);
        var response=await hr.Client.Send(HttpMethod.Get,$"/api/interviews/{invite.InterviewId}/report/print");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);var html=await response.Content.ReadAsStringAsync();
        Assert.Contains("&lt;script&gt;",html);Assert.DoesNotContain("<script>",html);Assert.DoesNotContain("<img",html);
        Assert.Contains("data-section='questions'",html);Assert.Contains("data-section='summary'",html);
        Assert.Contains("جمع‌بندی",html);Assert.Contains("default-src 'none'",response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [PostgresFact]
    public async Task Legacy_report_table_stays_absent_and_reapplying_migrations_preserves_current_reports()
    {
        var isolated=new ApiFixture();await isolated.InitializeAsync();
        try {
            var flow=new InterviewFlowTests(isolated);var (_,invite)=await flow.Invite();
            using var candidate=flow.Candidate(invite);
            await candidate.PostAsJsonAsync($"/api/candidate/interviews/{invite.InterviewId}/events",new InterviewEventRequest(Guid.NewGuid(),"consent_accepted",DateTimeOffset.UtcNow,"synthetic-consent"));
            await using var scope=isolated.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<HireYar.Infrastructure.Persistence.AppDbContext>();
            var migrations=(await db.Database.GetAppliedMigrationsAsync()).ToArray();var index=Array.FindIndex(migrations,m=>m.EndsWith("_RemoveLegacyEvaluationReports",StringComparison.Ordinal));var latest=migrations[index];var previous=migrations[index-1];
            Assert.Contains("RemoveLegacyEvaluationReports",latest);var migrator=db.GetService<IMigrator>();
            var org=await db.InterviewSessions.Where(s=>s.Id==invite.InterviewId).Select(s=>s.OrganizationId).SingleAsync();
            var script=migrator.GenerateScript(previous,latest,MigrationsSqlGenerationOptions.Idempotent);
            await db.Database.ExecuteSqlRawAsync(script);await db.Database.ExecuteSqlRawAsync(script);
            Assert.False(await db.Database.SqlQueryRaw<bool>("SELECT to_regclass('public.\"InterviewEvaluationReports\"') IS NOT NULL AS \"Value\"").SingleAsync());
            Assert.True(await db.InterviewSessions.AnyAsync(s=>s.Id==invite.InterviewId));Assert.True(await db.InterviewReviewEntries.AnyAsync(e=>e.InterviewSessionId==invite.InterviewId));
            Assert.False(db.Database.HasPendingModelChanges());Assert.Single(await db.Database.GetAppliedMigrationsAsync(),m=>m==latest);
        } finally {await ((IAsyncLifetime)isolated).DisposeAsync();}
    }
}
