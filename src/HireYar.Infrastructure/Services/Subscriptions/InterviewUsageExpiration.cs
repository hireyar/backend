using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HireYar.Infrastructure.Subscriptions;

public sealed class InterviewUsageExpiration(AppDbContext db, TimeProvider time)
{
    public Task<int> RunAsync(CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT hireyar_record_usage_expirations({time.GetUtcNow()})", ct);
}

public sealed class InterviewUsageExpirationWorker(IServiceScopeFactory scopes, ILogger<InterviewUsageExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<InterviewUsageExpiration>().RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception e) { logger.LogWarning("Interview usage expiration deferred exceptionType={ExceptionType}", e.GetType().Name); }
        }
    }
}
