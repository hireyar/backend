using HireYar.Application.Common;
using HireYar.Application.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HireYar.Infrastructure.Notifications;

public sealed class NotificationReminderOptions
{
    public bool Enabled { get; set; } = true;
    public int PollSeconds { get; set; } = 60;
}

public sealed class NotificationReminderWorker(IServiceScopeFactory scopes, NotificationReminderOptions options,
    ILogger<NotificationReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.PollSeconds));
        try { while (await timer.WaitForNextTickAsync(ct)) await RunOnceAsync(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        // Small bounded reads; one organization transaction at a time, no external scheduler.
        for (var page = 0; ; page++)
        {
            Guid[] organizations;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                organizations = await db.Organizations.AsNoTracking().OrderBy(o => o.Id).Skip(page * 100).Take(100).Select(o => o.Id).ToArrayAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception e) { logger.LogWarning("Notification scan deferred. ExceptionType={ExceptionType}", e.GetType().Name); return; }
            foreach (var org in organizations)
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<NotificationReminderService>().GenerateAsync(org, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception e) { logger.LogWarning("Notification reminder deferred. ExceptionType={ExceptionType}", e.GetType().Name); }
            }
            if (organizations.Length < 100) return;
        }
    }
}
