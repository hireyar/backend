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
                    await RecoverAsync(item.OrganizationId, item.Id, false, stoppingToken);
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
                    await RecoverAsync(item.OrganizationId, item.Id, true, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception e) { logger.LogWarning("Interview recovery poll failed exceptionType={ExceptionType}", e.GetType().Name); }
        }
    }

    private async Task RecoverAsync(Guid organizationId, Guid interviewId, bool introduction, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            using var trace = InterviewTrace.Begin(scope.ServiceProvider.GetRequiredService<IInterviewDiagnostics>(),
                organizationId, interviewId, Guid.NewGuid().ToString("N"));
            InterviewTrace.Stage("BackgroundRecovery", introduction
                ? "بازیابی معرفی صوتی پذیرفته‌شده توسط worker"
                : "بازیابی پردازش پاسخ پذیرفته‌شده توسط worker");
            var service = scope.ServiceProvider.GetRequiredService<CandidateInterviewService>();
            if (introduction) await service.RecoverIntroductionAsync(organizationId, interviewId, ct);
            else await service.RecoverAsync(organizationId, interviewId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            logger.LogWarning("{RecoveryKind} recovery deferred interviewId={InterviewId} exceptionType={ExceptionType}",
                introduction ? "Introduction" : "Interview", interviewId, e.GetType().Name);
        }
    }
}
