using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HireYar.Infrastructure.Interviews;

public sealed class InterviewRecoveryWorker(IServiceScopeFactory scopes, InterviewOptions options, TimeProvider time,
    ILogger<InterviewRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.BackgroundRecoveryEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.RecoveryPollSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var now = time.GetUtcNow();
                var pending = await db.InterviewSessions.IgnoreAutoIncludes().AsNoTracking().Where(s =>
                    s.Status == InterviewStatus.ProcessingAnswer && s.DeletedAtUtc == null && s.AccessRevokedAtUtc == null && s.ExpiresAtUtc > now &&
                    s.RecoveryAttempts < options.MaxRecoveryAttempts && (s.NextRecoveryAtUtc == null || s.NextRecoveryAtUtc <= now) &&
                    (s.ProcessingLeaseExpiresAtUtc == null || s.ProcessingLeaseExpiresAtUtc <= now))
                    .OrderBy(s => s.NextRecoveryAtUtc).ThenBy(s => s.Id).Select(s => new { s.OrganizationId, s.Id }).Take(20).ToListAsync(stoppingToken);
                foreach (var item in pending)
                {
                    try
                    {
                        await using var work = scopes.CreateAsyncScope();
                        await work.ServiceProvider.GetRequiredService<CandidateInterviewService>().RecoverAsync(item.OrganizationId, item.Id, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception e) { logger.LogWarning("Interview recovery deferred interviewId={InterviewId} exceptionType={ExceptionType}", item.Id, e.GetType().Name); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception e) { logger.LogWarning("Interview recovery poll failed exceptionType={ExceptionType}", e.GetType().Name); }
        }
    }
}
