using System.Globalization;
using System.Text.Json;
using HireYar.Application.Billing;
using HireYar.Application.Common;

namespace HireYar.Infrastructure.Billing;

// One shared quote and refresh gate per process; never contact the exchange under a DB lock.
public sealed class WallexAiDisplayRateProvider(IHttpClientFactory clients, TimeProvider time) : IAiDisplayRateProvider
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private AiDisplayRateDto? cached;
    private DateTimeOffset retryAfter;

    public async Task<AiDisplayRateDto> GetAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            var now = time.GetUtcNow();
            if (cached is not null && now < cached.ExpiresAtUtc) return cached;
            if (now < retryAfter) throw Unavailable();
            try
            {
                using var client = clients.CreateClient("WallexDisplayRate");
                using var response = await client.GetAsync("v1/markets", ct);
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var rate = ParseRate(json.RootElement);
                var fetched = time.GetUtcNow();
                return cached = new("Wallex", "USDTTMN", "IRT", rate, fetched, fetched.AddSeconds(5), 5);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
            {
                retryAfter = time.GetUtcNow().AddSeconds(5);
                throw Unavailable();
            }
        }
        finally { gate.Release(); }
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
        return value;
    }

    private static AppException Unavailable() => new(503, "ai_display_rate_unavailable", "The live AI display rate is temporarily unavailable.");
}
