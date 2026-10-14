using HireYar.Application.Common;
using HireYar.Application.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HireYar.Infrastructure.Interviews;

public sealed class InterviewReportWorker(IServiceScopeFactory scopes, TimeProvider time,
    ILogger<InterviewReportWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(
        PollAsync(ProcessPendingAsync, "generation", stoppingToken),
        PollAsync(RecoverPendingAsync, "recovery", stoppingToken));

    private async Task PollAsync(Func<CancellationToken, Task> work, string operation, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try {
            while (await timer.WaitForNextTickAsync(ct)) {
                try { await work(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (Exception error) { logger.LogWarning("Report poll failed operation={Operation} errorType={ErrorType}", operation, error.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    // Independent of generation capacity: even four blocked model requests cannot suppress recovery.
    public async Task RecoverPendingAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var stale = await (from report in db.InterviewEvaluationReports.AsNoTracking().Where(InterviewReportLifecycle.StaleAt(time.GetUtcNow()))
            join session in db.InterviewSessions.IgnoreAutoIncludes().AsNoTracking() on report.InterviewSessionId equals session.Id
            where report.OrganizationId == session.OrganizationId && session.DeletedAtUtc == null &&
                session.Status == HireYar.Domain.Interviews.InterviewStatus.Completed
            orderby report.StartedAtUtc, report.InterviewSessionId
            select new { report.OrganizationId, report.InterviewSessionId }).Take(20).ToListAsync(ct);
        await Parallel.ForEachAsync(stale, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (item, token) => {
            try {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(5));
                await using var job = scopes.CreateAsyncScope();
                await job.ServiceProvider.GetRequiredService<InterviewReportService>()
                    .RecoverStaleAsync(item.OrganizationId, item.InterviewSessionId, deadline.Token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error) { logger.LogWarning("Report recovery failed interviewId={InterviewId} errorType={ErrorType}", item.InterviewSessionId, error.GetType().Name); }
        });
    }

    public async Task ProcessPendingAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var pending = await (from report in db.InterviewEvaluationReports.AsNoTracking()
            join session in db.InterviewSessions.IgnoreAutoIncludes().AsNoTracking() on report.InterviewSessionId equals session.Id
            where report.OrganizationId == session.OrganizationId && session.DeletedAtUtc == null &&
                session.Status == HireYar.Domain.Interviews.InterviewStatus.Completed && report.GenerationStage == "Queued"
            orderby report.RequestedAtUtc, report.InterviewSessionId
            select new { report.OrganizationId, report.InterviewSessionId }).Take(20).ToListAsync(ct);
        await Parallel.ForEachAsync(pending, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (item, token) => {
            try {
                await using var job = scopes.CreateAsyncScope();
                await job.ServiceProvider.GetRequiredService<InterviewReportService>()
                    .ProcessQueuedAsync(item.OrganizationId, item.InterviewSessionId, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error) { logger.LogWarning("Report worker failed interviewId={InterviewId} errorType={ErrorType}", item.InterviewSessionId, error.GetType().Name); }
        });
    }
}
