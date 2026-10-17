namespace HireYar.Domain.Billing;

// One immutable signup grant per organization; no gateway payment or cash purchase.
public sealed class RegistrationTrial
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public decimal CreditAmountRial { get; set; }
    public decimal AiPricePerMinuteUsd { get; set; }
    public int RetentionDays { get; set; }
    public int IncludedTeamUsers { get; set; } = 5;
    public int PaidTeamUserLimit { get; set; } = 30;
    public decimal TeamUserMonthlyPriceIrt { get; set; } = 200_000m;
    public List<RegistrationTrialEntitlement> Entitlements { get; set; } = [];
}

public sealed class RegistrationTrialEntitlement
{
    public Guid TrialId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public FeatureDataType DataType { get; set; }
    public string? Value { get; set; }
}
