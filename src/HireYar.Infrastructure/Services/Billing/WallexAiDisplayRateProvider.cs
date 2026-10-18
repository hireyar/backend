using System.Globalization;
using System.Text.Json;
using HireYar.Application.Billing;

namespace HireYar.Infrastructure.Billing;

// One shared quote and refresh gate per process; never contact the exchange under a DB lock.
public sealed class WallexAiDisplayRateProvider(IHttpClientFactory clients, TimeProvider time) : IAiDisplayRateProvider
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private AiDisplayRateDto? cached;
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InitialRetryInterval = TimeSpan.FromSeconds(5);
    private DateTimeOffset nextAttemptAtUtc;

    public async Task<AiDisplayRateDto> GetAsync(CancellationToken ct)
    {
        // Before the first successful quote, retry on the server without inventing a rate.
        // After that, a provider failure always returns the last successful amount.
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            TimeSpan delay;
            await gate.WaitAsync(ct);
            try
            {
                var now = time.GetUtcNow();
                if (cached is not null && now < nextAttemptAtUtc) return cached;
                if (now >= nextAttemptAtUtc)
                {
                    try
                    {
                        using var client = clients.CreateClient("WallexDisplayRate");
                        using var response = await client.GetAsync("v1/markets", ct);
                        response.EnsureSuccessStatusCode();
                        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                        var rate = ParseRate(json.RootElement);
                        var fetched = time.GetUtcNow();
                        nextAttemptAtUtc = fetched.Add(RefreshInterval);
                        return cached = new("IRR", rate, fetched, nextAttemptAtUtc, (int)RefreshInterval.TotalSeconds);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException or
                        InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or IOException or ArgumentException)
                    {
                        nextAttemptAtUtc = time.GetUtcNow().Add(cached is null ? InitialRetryInterval : RefreshInterval);
                        if (cached is not null)
                            return cached = cached with { ExpiresAtUtc = nextAttemptAtUtc, IsStale = true };
                    }
                }
                delay = nextAttemptAtUtc - time.GetUtcNow();
            }
            finally { gate.Release(); }
            if (delay > TimeSpan.Zero) await Task.Delay(delay, time, ct);
        }
    }

    public static decimal ParseRate(JsonElement root)
    {
        if (!root.GetProperty("success").GetBoolean()) throw new FormatException("Unsuccessful market response.");
        var market = root.GetProperty("result").GetProperty("symbols").GetProperty("USDTTMN");
        if (market.GetProperty("quoteAsset").GetString() != "TMN" || market.GetProperty("baseAsset").GetString() != "USDT")
            throw new FormatException("Unexpected market units.");
        var price = market.GetProperty("stats").GetProperty("lastPrice");
        var value = price.ValueKind == JsonValueKind.String
            ? decimal.Parse(price.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture) : price.GetDecimal();
        if (value <= 0 || value > 1_000_000_000m) throw new FormatException("Invalid market price.");
        // Wallex TMN prices are toman; the public conversion contract uses rial.
        return value * 10m;
    }

}
