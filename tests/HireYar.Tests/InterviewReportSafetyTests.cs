using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireYar.Tests;

public sealed partial class InterviewFlowTests
{
    [PostgresFact]
    public async Task Report_status_recovers_missing_expired_and_overlong_leases_and_allows_retry()
    {
        f.ReportAi.Reset();
        var (hr, invite) = await Invite(); using var candidate = Candidate(invite);
        await Start(candidate, invite.InterviewId);
        await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null);
        foreach (var stage in new[] { "Generating", "Validating", "Saving" })
        foreach (var leaseKind in new[] { "missing", "expired", "overlong" }) {
            await f.Db(async db => {
                var row = await db.InterviewEvaluationReports.SingleAsync(r => r.InterviewSessionId == invite.InterviewId);
                row.GenerationStage = stage; row.GenerationAttempts = 4;
                row.StartedAtUtc = f.Clock.GetUtcNow().AddSeconds(leaseKind == "overlong" ? -151 : -10);
                row.LeaseId = leaseKind == "missing" ? null : Guid.NewGuid();
                row.LeaseExpiresAtUtc = leaseKind == "missing" ? null : f.Clock.GetUtcNow().AddSeconds(leaseKind == "expired" ? -1 : 3600);
                row.FinishedAtUtc = null; row.FailureCode = row.FailureStage = null;
                await db.SaveChangesAsync();
            });
            var status = await ReportStatus(hr, invite.InterviewId);
            Assert.Equal("Failed", status.Status); Assert.True(status.CanRetry);
            Assert.Equal("report_interrupted", status.FailureCode); Assert.Equal(stage, status.FailureStage);
            Assert.Equal(4, status.Attempts); Assert.NotNull(status.FinishedAtUtc);
            await f.Db(async db => {
                var row = await db.InterviewEvaluationReports.SingleAsync(r => r.InterviewSessionId == invite.InterviewId);
                Assert.Equal("Failed", row.GenerationStage); Assert.Null(row.LeaseId); Assert.Null(row.LeaseExpiresAtUtc);
            });
        }
        Assert.Equal(0, f.ReportAi.Calls);
        Assert.Equal(HttpStatusCode.Accepted, (await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{invite.InterviewId}/evaluation-report")).StatusCode);
        await f.ProcessReportAsync(invite.InterviewId);
        Assert.Equal("Succeeded", (await ReportStatus(hr, invite.InterviewId)).Status);
        f.ReportAi.Reset();
    }

    [PostgresFact]
    public async Task Healthy_report_lease_is_not_expired_by_status_or_recovery()
    {
        f.ReportAi.Reset();
        var (hr, invite) = await Invite(); using var candidate = Candidate(invite);
        await Start(candidate, invite.InterviewId);
        await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null);
        f.ReportAi.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        f.ReportAi.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = f.ProcessReportAsync(invite.InterviewId);
        try {
            await f.ReportAi.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var status = await ReportStatus(hr, invite.InterviewId);
            Assert.Equal("Processing", status.Status); Assert.False(status.CanRetry); Assert.Null(status.FinishedAtUtc);
            await ActivatorUtilities.CreateInstance<InterviewReportWorker>(f.Services).RecoverPendingAsync(default);
            Assert.Equal("Processing", (await ReportStatus(hr, invite.InterviewId)).Status);
        }
        finally { f.ReportAi.Release.TrySetResult(); await running; f.ReportAi.Reset(); }
        Assert.Equal("Succeeded", (await ReportStatus(hr, invite.InterviewId)).Status);
    }

    [PostgresFact]
    public async Task Report_deadline_releases_uncooperative_provider_and_ignores_late_result_after_retry()
    {
        f.ReportAi.Reset();
        var (hr, invite) = await Invite(); using var candidate = Candidate(invite);
        await Start(candidate, invite.InterviewId);
        await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null);
        f.ReportAi.IgnoreCancellation = true;
        f.ReportAi.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldRelease = f.ReportAi.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Action? fireDeadline = null;
        f.Clock.TimerCreated = (callback, state, due) => {
            if (due == InterviewReportLifecycle.AttemptTimeout) fireDeadline = () => callback(state);
        };
        var running = f.ProcessReportAsync(invite.InterviewId);
        try {
            await f.ReportAi.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(fireDeadline); fireDeadline();
            await running.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(oldRelease.Task.IsCompleted);
            var failed = await ReportStatus(hr, invite.InterviewId);
            Assert.Equal("report_timeout", failed.FailureCode); Assert.Equal("Generating", failed.FailureStage); Assert.True(failed.CanRetry);
            f.Clock.TimerCreated = null;
            f.ReportAi.Release = f.ReportAi.Entered = null;
            Assert.Equal(HttpStatusCode.Accepted, (await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{invite.InterviewId}/evaluation-report")).StatusCode);
            await f.ProcessReportAsync(invite.InterviewId);
            var cached = await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/evaluation-report")).Content.ReadAsStringAsync();
            oldRelease.TrySetResult();
            await f.ProcessReportAsync(invite.InterviewId);
            Assert.Equal("Succeeded", (await ReportStatus(hr, invite.InterviewId)).Status);
            Assert.Equal(cached, await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/evaluation-report")).Content.ReadAsStringAsync());
            Assert.Equal(2, f.ReportAi.Calls);
        }
        finally { f.Clock.TimerCreated = null; oldRelease.TrySetResult(); await running; f.ReportAi.Reset(); }
    }

    [PostgresFact]
    public async Task Background_recovery_persists_failure_while_generation_is_still_blocked_without_status_reads()
    {
        var live = new ApiFixture { ReportWorkerEnabled = true };
        await live.InitializeAsync();
        try {
            var flow = new InterviewFlowTests(live);
            var (hr, invite) = await flow.Invite(); using var candidate = flow.Candidate(invite);
            await Start(candidate, invite.InterviewId);
            live.ReportAi.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            live.ReportAi.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null);
            await live.ReportAi.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await live.Db(async db => {
                var row = await db.InterviewEvaluationReports.SingleAsync(r => r.InterviewSessionId == invite.InterviewId);
                row.StartedAtUtc = live.Clock.GetUtcNow().AddSeconds(-151);
                await db.SaveChangesAsync();
            });
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var failed = false;
            while (!failed) {
                await live.Db(async db => failed = await db.InterviewEvaluationReports.AnyAsync(r => r.InterviewSessionId == invite.InterviewId && r.GenerationStage == "Failed", deadline.Token));
                if (!failed) await Task.Delay(100, deadline.Token);
            }
            Assert.False(live.ReportAi.Release.Task.IsCompleted);
            Assert.True((await ReportStatus(hr, invite.InterviewId)).CanRetry);
            live.ReportAi.Release.TrySetResult();
            Assert.Equal(1, live.ReportAi.Calls);
        }
        finally { live.ReportAi.Release?.TrySetResult(); await ((IAsyncLifetime)live).DisposeAsync(); }
    }

    [PostgresFact]
    public async Task Recovering_stale_report_preserves_cached_result_and_failure_does_not_replace_it()
    {
        f.ReportAi.Reset();
        var (hr, invite) = await Invite(); using var candidate = Candidate(invite);
        await Start(candidate, invite.InterviewId);
        await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null);
        await f.ProcessReportAsync(invite.InterviewId);
        var cached = await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/evaluation-report")).Content.ReadAsStringAsync();
        await f.Db(async db => {
            var row = await db.InterviewEvaluationReports.SingleAsync(r => r.InterviewSessionId == invite.InterviewId);
            row.GenerationStage = "Saving"; row.LeaseId = null; row.LeaseExpiresAtUtc = null; row.FinishedAtUtc = null;
            await db.SaveChangesAsync();
        });
        var failed = await ReportStatus(hr, invite.InterviewId);
        Assert.Equal("Failed", failed.Status); Assert.True(failed.ReportAvailable); Assert.True(failed.CanRetry);
        Assert.Equal(cached, await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/evaluation-report")).Content.ReadAsStringAsync());
        f.ReportAi.Invalid = true;
        Assert.Equal(HttpStatusCode.Accepted, (await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{invite.InterviewId}/evaluation-report")).StatusCode);
        await f.ProcessReportAsync(invite.InterviewId);
        Assert.Equal("report_validation_failed", (await ReportStatus(hr, invite.InterviewId)).FailureCode);
        Assert.Equal(cached, await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/evaluation-report")).Content.ReadAsStringAsync());
        f.ReportAi.Reset();
    }
}
