namespace HireYar.Application.Billing;

public sealed record AiDisplayRateDto(string Currency,
    decimal RialPerUsdt, DateTimeOffset FetchedAtUtc, DateTimeOffset ExpiresAtUtc, int RefreshAfterSeconds, bool IsStale = false);

public interface IAiDisplayRateProvider
{
    Task<AiDisplayRateDto> GetAsync(CancellationToken ct);
}
