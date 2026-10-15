using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HireYar.Infrastructure.Interviews;

public sealed class AnswerAssessmentWorker(IServiceScopeFactory scopes, AnswerAssessmentOptions options,
    TimeProvider time, ILogger<AnswerAssessmentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.WorkerEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.PollSeconds));
        try { while (await timer.WaitForNextTickAsync(ct)) await RunOnceAsync(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        try {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(); var now = time.GetUtcNow();
            var pending = await (from s in db.InterviewSessions.IgnoreAutoIncludes().AsNoTracking()
                join a in db.InterviewAnswers.AsNoTracking() on s.Id equals a.InterviewSessionId
                where s.Status == InterviewStatus.Completed && s.DeletedAtUtc == null && !a.IsInteraction && a.Transcript != null && a.Transcript.Trim() != "" &&
                    (!db.AnswerAssessments.Any(r => r.AnswerId == a.Id) || db.AnswerAssessments.Any(r => r.AnswerId == a.Id && r.OrganizationId == s.OrganizationId &&
                        ((r.State == "Queued" && (r.NextAttemptAtUtc == null || r.NextAttemptAtUtc <= now)) || (r.State == "Processing" && r.LeaseExpiresAtUtc <= now))))
                orderby a.SubmittedAtUtc, a.Id
                select new { s.OrganizationId, a.Id }).Take(3).ToArrayAsync(ct);
            await Parallel.ForEachAsync(pending, new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = ct }, async (item, token) => {
                try {
                    await using var job = scopes.CreateAsyncScope();
                    await job.ServiceProvider.GetRequiredService<AnswerAssessmentService>().ProcessAsync(item.OrganizationId, item.Id, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception e) { logger.LogWarning("Answer assessment deferred answerId={AnswerId} exceptionType={ExceptionType}", item.Id, e.GetType().Name); }
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) { logger.LogWarning("Answer assessment poll deferred exceptionType={ExceptionType}", e.GetType().Name); }
    }
}
