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
                    db.InterviewBillings.Any(b => b.OrganizationId == s.OrganizationId && b.InterviewSessionId == s.Id && b.Status == HireYar.Domain.Billing.InterviewBillingStatus.Reserved) &&
                    s.RecoveryAttempts < options.MaxRecoveryAttempts && (s.NextRecoveryAtUtc == null || s.NextRecoveryAtUtc <= now) &&
                    (s.ProcessingLeaseExpiresAtUtc == null || s.ProcessingLeaseExpiresAtUtc <= now))
                    .OrderBy(s => s.NextRecoveryAtUtc).ThenBy(s => s.Id).Select(s => new { s.OrganizationId, s.Id }).Take(20).ToListAsync(stoppingToken);
                foreach (var item in pending)
                {
                    try
                    {
                        await using var work = scopes.CreateAsyncScope();
                        using var trace = InterviewTrace.Begin(work.ServiceProvider.GetRequiredService<IInterviewDiagnostics>(), item.OrganizationId, item.Id, Guid.NewGuid().ToString("N"));
                        InterviewTrace.Stage("BackgroundRecovery", "بازیابی پردازش پاسخ پذیرفته‌شده توسط worker");
                        await work.ServiceProvider.GetRequiredService<CandidateInterviewService>().RecoverAsync(item.OrganizationId, item.Id, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception e) { logger.LogWarning("Interview recovery deferred interviewId={InterviewId} exceptionType={ExceptionType}", item.Id, e.GetType().Name); }
                }
                var introductions = await (from introduction in db.InterviewIntroductions.AsNoTracking()
                    join session in db.InterviewSessions.IgnoreAutoIncludes().AsNoTracking() on introduction.InterviewSessionId equals session.Id
                    where introduction.OrganizationId == session.OrganizationId && session.Stage == InterviewStage.CandidateIntroduction &&
                        session.Status == InterviewStatus.WaitingForAnswer && session.DeletedAtUtc == null && session.AccessRevokedAtUtc == null && session.ExpiresAtUtc > now &&
                        introduction.Status != IntroductionStatus.Processed && introduction.RecoveryAttempts < options.MaxRecoveryAttempts &&
                        (introduction.NextRecoveryAtUtc == null || introduction.NextRecoveryAtUtc <= now) &&
                        (introduction.ProcessingLeaseExpiresAtUtc == null || introduction.ProcessingLeaseExpiresAtUtc <= now)
                    orderby introduction.NextRecoveryAtUtc, introduction.Id
                    select new { session.OrganizationId, session.Id }).Take(20).ToListAsync(stoppingToken);
                foreach (var item in introductions)
                {
                    try
                    {
                        await using var work = scopes.CreateAsyncScope();
                        using var trace = InterviewTrace.Begin(work.ServiceProvider.GetRequiredService<IInterviewDiagnostics>(), item.OrganizationId, item.Id, Guid.NewGuid().ToString("N"));
                        InterviewTrace.Stage("BackgroundRecovery", "بازیابی معرفی صوتی پذیرفته‌شده توسط worker");
                        await work.ServiceProvider.GetRequiredService<CandidateInterviewService>().RecoverIntroductionAsync(item.OrganizationId, item.Id, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception e) { logger.LogWarning("Introduction recovery deferred interviewId={InterviewId} exceptionType={ExceptionType}", item.Id, e.GetType().Name); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception e) { logger.LogWarning("Interview recovery poll failed exceptionType={ExceptionType}", e.GetType().Name); }
        }
    }
}
