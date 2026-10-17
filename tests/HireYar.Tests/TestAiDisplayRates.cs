using HireYar.Application.Billing;
using HireYar.Application.Common;

namespace HireYar.Tests;

// Synthetic 1 rial/USD keeps accounting-flow expectations separate from the forex projection tests.
public sealed class TestAiDisplayRates : IAiDisplayRateProvider
{
    public TimeProvider Clock { get; set; } = TimeProvider.System;
    public decimal RialPerUsdt { get; set; } = 1m;
    public bool Unavailable { get; set; }
    public Task<AiDisplayRateDto> GetAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (Unavailable) throw new AppException(503, "ai_display_rate_unavailable", "Synthetic rate failure.");
        var now = Clock.GetUtcNow();
        return Task.FromResult(new AiDisplayRateDto("IRR", RialPerUsdt, now, now.AddSeconds(5), 5));
    }
}
