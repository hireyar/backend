using HireYar.Domain.Billing;
namespace HireYar.Application.Organizations;

public sealed record OrganizationFeatureDto(string Code, string Name, FeatureDataType DataType, string? Value);
public sealed record OrganizationFeaturesDto(bool HasActiveSubscription, bool IsTrial,
    DateTimeOffset? StartedAtUtc, DateTimeOffset? ExpiresAtUtc, IReadOnlyList<OrganizationFeatureDto> Features);
