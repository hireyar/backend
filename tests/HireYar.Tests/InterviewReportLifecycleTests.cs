using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using HireYar.Infrastructure.Persistence;
using HireYar.Domain.Interviews;
using Npgsql;

namespace HireYar.Tests;

public sealed partial class InterviewFlowTests
{
    private static async Task<InterviewReportGenerationDto> ReportStatus(Account hr, Guid id) =>
        (await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{id}/evaluation-report/status"))
            .Content.ReadFromJsonAsync<InterviewReportGenerationDto>())!;

    [PostgresFact]
    public async Task Automatic_completion_queues_once_and_success_cannot_be_regenerated()
    {
        f.ReportAi.Reset(); var (hr, invite) = await Invite(); using var candidate = Candidate(invite);
        var state = await Start(candidate, invite.InterviewId);
        Assert.Equal("WaitingForCompletion", (await ReportStatus(hr, invite.InterviewId)).Stage);
        for (var i = 0; i < 3; i++)
            state = (await (await Submit(candidate, invite.InterviewId, state.Question!.Id)).Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        Assert.Equal("Completed", state.Status);
        var queued = await ReportStatus(hr, invite.InterviewId);
        Assert.Equal("Queued", queued.Stage); Assert.False(queued.CanRetry); Assert.NotNull(queued.RequestedAtUtc);
        Assert.Equal(0, f.ReportAi.Calls);
        Assert.Equal(HttpStatusCode.Gone, (await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null)).StatusCode);
        await f.Db(async db => Assert.Equal(1, await db.InterviewEvaluationReports.CountAsync(r => r.InterviewSessionId == invite.InterviewId)));
        await f.ProcessReportAsync(invite.InterviewId);
        var succeeded = await ReportStatus(hr, invite.InterviewId);
        Assert.Equal("Succeeded", succeeded.Status); Assert.Equal(1, succeeded.Attempts);
        var conflict = await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{invite.InterviewId}/evaluation-report/regenerate", new GenerateInterviewReportRequest());
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var problem = await conflict.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        Assert.Equal("report_retry_not_allowed", problem.GetProperty("code").GetString());
        await f.ProcessReportAsync(invite.InterviewId); Assert.Equal(1, f.ReportAi.Calls);
    }

    [PostgresFact]
    public async Task Lifecycle_migration_preserves_cached_results_and_legacy_leases_and_guards_downgrade()
    {
        var isolated = new ApiFixture(); await isolated.InitializeAsync();
        try {
            var flow = new InterviewFlowTests(isolated);
            var (_, empty) = await flow.Invite(); var (_, cached) = await flow.Invite(); var (_, active) = await flow.Invite();
            var now = isolated.Clock.GetUtcNow(); var lease = Guid.NewGuid();
            await isolated.Db(async db => {
                foreach (var id in new[] { empty.InterviewId, cached.InterviewId, active.InterviewId }) {
                    var org = await db.InterviewSessions.Where(s => s.Id == id).Select(s => s.OrganizationId).SingleAsync();
                    db.InterviewEvaluationReports.Add(new() { InterviewSessionId = id, OrganizationId = org,
                        ResultJson = id == cached.InterviewId ? "{\"legacy\":true}" : null,
                        Model = id == cached.InterviewId ? "synthetic-model" : null, Provider = id == cached.InterviewId ? "Test" : null,
                        PromptVersion = id == cached.InterviewId ? "report-v1" : null, CreatedAtUtc = id == cached.InterviewId ? now : null,
                        LeaseId = id == active.InterviewId ? lease : null, LeaseExpiresAtUtc = id == active.InterviewId ? now.AddSeconds(150) : null });
                }
                await db.SaveChangesAsync();
            });
            await using var scope = isolated.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261012193519_AddInterviewRecordingSegments");
            var script = migrator.GenerateScript("20261012193519_AddInterviewRecordingSegments", "20261013054737_AddReportGenerationLifecycle", MigrationsSqlGenerationOptions.Idempotent);
            await db.Database.ExecuteSqlRawAsync(script); await db.Database.ExecuteSqlRawAsync(script);
            var rows = await db.InterviewEvaluationReports.AsNoTracking().ToDictionaryAsync(r => r.InterviewSessionId);
            Assert.Equal("Failed", rows[empty.InterviewId].GenerationStage); Assert.Equal("report_not_generated", rows[empty.InterviewId].FailureCode);
            Assert.Equal("Succeeded", rows[cached.InterviewId].GenerationStage); Assert.Contains("true", rows[cached.InterviewId].ResultJson!);
            Assert.Equal("report-v1", rows[cached.InterviewId].PromptVersion); Assert.Equal("synthetic-model", rows[cached.InterviewId].Model);
            Assert.Equal("Generating", rows[active.InterviewId].GenerationStage); Assert.Equal(lease, rows[active.InterviewId].LeaseId);
            Assert.Single(await db.Database.GetAppliedMigrationsAsync(), m => m == "20261013054737_AddReportGenerationLifecycle");
            Assert.False(db.Database.HasPendingModelChanges());
            var guarded = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync("20261012193519_AddInterviewRecordingSegments"));
            Assert.Equal(PostgresErrorCodes.RaiseException, guarded.SqlState);
        }
        finally { await ((IAsyncLifetime)isolated).DisposeAsync(); }
    }

    [PostgresFact]
    public async Task Concurrent_retries_after_failure_enqueue_one_attempt_and_failures_are_not_automatically_retried()
    {
        f.ReportAi.Reset(); var (hr, invite) = await Invite(); using var candidate = Candidate(invite);
        await Start(candidate, invite.InterviewId);
        await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null);
        f.ReportAi.Fail = true; await f.ProcessReportAsync(invite.InterviewId);
        await f.ProcessReportAsync(invite.InterviewId); Assert.Equal(1, f.ReportAi.Calls);
        var url = $"/api/interviews/{invite.InterviewId}/evaluation-report";
        var retries = await Task.WhenAll(hr.Client.Send(HttpMethod.Post, url), hr.Client.Send(HttpMethod.Post, url));
        Assert.Single(retries, r => r.StatusCode == HttpStatusCode.Accepted);
        Assert.Single(retries, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, f.ReportAi.Calls); Assert.Equal("Queued", (await ReportStatus(hr, invite.InterviewId)).Stage);
        f.ReportAi.Fail = false; await f.ProcessReportAsync(invite.InterviewId);
        var ready = await ReportStatus(hr, invite.InterviewId);
        Assert.Equal("Succeeded", ready.Status); Assert.Equal(2, ready.Attempts);
        f.ReportAi.Reset();
    }

    [PostgresFact]
    public async Task Worker_cancellation_persists_failure_and_requires_explicit_retry()
    {
        f.ReportAi.Reset(); var (hr, invite) = await Invite(); using var candidate = Candidate(invite);
        await Start(candidate, invite.InterviewId);
        await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null);
        f.ReportAi.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        f.ReportAi.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stopped = new CancellationTokenSource();
        var running = f.ProcessReportAsync(invite.InterviewId, stopped.Token);
        await f.ReportAi.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        stopped.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        var failed = await ReportStatus(hr, invite.InterviewId);
        Assert.Equal("Failed", failed.Status); Assert.Equal("report_interrupted", failed.FailureCode); Assert.True(failed.CanRetry);
        f.ReportAi.Entered = f.ReportAi.Release = null;
        await f.ProcessReportAsync(invite.InterviewId); Assert.Equal(1, f.ReportAi.Calls);
        Assert.Equal(HttpStatusCode.Accepted, (await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{invite.InterviewId}/evaluation-report")).StatusCode);
        await f.ProcessReportAsync(invite.InterviewId);
        Assert.Equal("Succeeded", (await ReportStatus(hr, invite.InterviewId)).Status);
        f.ReportAi.Reset();
    }

    [PostgresFact]
    public async Task Hosted_report_worker_generates_after_completion_without_panel_post()
    {
        var live = new ApiFixture { ReportWorkerEnabled = true };
        await live.InitializeAsync();
        try {
            var flow = new InterviewFlowTests(live);
            var (hr, invite) = await flow.Invite(); using var candidate = flow.Candidate(invite);
            await Start(candidate, invite.InterviewId);
            live.ReportAi.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            live.ReportAi.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null)).StatusCode);
            await live.ReportAi.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal("Generating", (await ReportStatus(hr, invite.InterviewId)).Stage);
            live.ReportAi.Release.TrySetResult();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while ((await ReportStatus(hr, invite.InterviewId)).Status != "Succeeded") await Task.Delay(100, deadline.Token);
            Assert.Equal(1, live.ReportAi.Calls);
            Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/evaluation-report")).StatusCode);
        }
        finally { live.ReportAi.Release?.TrySetResult(); await ((IAsyncLifetime)live).DisposeAsync(); }
    }
}
