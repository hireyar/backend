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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await ProcessPendingAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogWarning("Report poll failed errorType={ErrorType}", error.GetType().Name); }
        }
    }

    public async Task ProcessPendingAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var now = time.GetUtcNow();
        var pending = await (from report in db.InterviewEvaluationReports.AsNoTracking()
            join session in db.InterviewSessions.IgnoreAutoIncludes().AsNoTracking() on report.InterviewSessionId equals session.Id
            where report.OrganizationId == session.OrganizationId && session.DeletedAtUtc == null &&
                session.Status == HireYar.Domain.Interviews.InterviewStatus.Completed &&
                (report.GenerationStage == "Queued" || report.LeaseExpiresAtUtc <= now)
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
