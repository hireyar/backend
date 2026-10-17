namespace HireYar.Application.Billing;

public sealed record AiDisplayRateDto(string Source, string Symbol, string Currency,
    decimal TomanPerUsdt, DateTimeOffset FetchedAtUtc, DateTimeOffset ExpiresAtUtc, int RefreshAfterSeconds);

public interface IAiDisplayRateProvider
{
    Task<AiDisplayRateDto> GetAsync(CancellationToken ct);
}
