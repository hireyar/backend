namespace HireYar.Application.Billing;

public sealed record AiDisplayRateDto(string Currency,
    decimal RialPerUsdt, DateTimeOffset FetchedAtUtc, DateTimeOffset ExpiresAtUtc, int RefreshAfterSeconds);

public interface IAiDisplayRateProvider
{
    Task<AiDisplayRateDto> GetAsync(CancellationToken ct);
}
