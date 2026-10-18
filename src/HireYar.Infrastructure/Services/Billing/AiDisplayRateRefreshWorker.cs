using HireYar.Application.Billing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HireYar.Infrastructure.Billing;

// Uses the same singleton/gate as HTTP reads; no separate quote or retry storm.
public sealed class AiDisplayRateRefreshWorker(IAiDisplayRateProvider rates, TimeProvider time,
    ILogger<AiDisplayRateRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var quote = await rates.GetAsync(stoppingToken);
                var delay = quote.ExpiresAtUtc - time.GetUtcNow();
                await Task.Delay(delay > TimeSpan.Zero ? delay : WallexAiDisplayRateProvider.RefreshInterval, time, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception)
            {
                logger.LogWarning("AI display rate refresh interrupted. EventCode={EventCode}", "ai_rate_refresh_interrupted");
                try { await Task.Delay(WallexAiDisplayRateProvider.RefreshInterval, time, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            }
        }
    }
}
