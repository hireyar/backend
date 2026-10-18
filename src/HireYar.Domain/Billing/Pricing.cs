namespace HireYar.Domain.Billing;

public sealed class PricingPackage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsPublic { get; set; }
    public int SortOrder { get; set; }
    // Existing price is the HR/general rate; retain its column and integration name.
    public decimal AiPricePerMinuteUsd { get; set; }
    public decimal? SpecializedAiPricePerMinuteUsd { get; set; }
    public int InterviewRetentionDays { get; set; }
    public int IncludedTeamUsers { get; set; } = 3;
    public int PaidTeamUserLimit { get; set; } = 30;
    public decimal TeamUserMonthlyPriceIrt { get; set; } = 200_000m;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<PackagePeriod> Periods { get; set; } = [];
    public List<PackageFeature> Features { get; set; } = [];
}
public sealed class PackagePeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PackageId { get; set; }
    public int Months { get; set; }
    public decimal PriceAmount { get; set; }
    public string Currency { get; set; } = "IRT";
    public decimal DiscountPercent { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
public enum FeatureDataType { Boolean, Integer, Text }
public sealed class FeatureDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public FeatureDataType DataType { get; set; }
}
public sealed class PackageFeature
{
    public Guid PackageId { get; set; }
    public Guid FeatureId { get; set; }
    public bool IsEnabled { get; set; }
    public string? Value { get; set; }
    public FeatureDefinition Feature { get; set; } = null!;
}
public sealed class AiCreditPackage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal CreditAmountRial { get; set; }
    public decimal SellPriceAmount { get; set; }
    public string SellPriceCurrency { get; set; } = "IRT";
    public bool IsActive { get; set; }
    public bool IsPublic { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public static class BillingMath
{
    public static decimal Cost(int seconds, decimal rate)
    {
        if (seconds < 0 || rate < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        return decimal.Round(seconds / 60m * rate, 6, MidpointRounding.ToEven);
    }
    public static bool HasPrecision(decimal value, int scale) => decimal.Round(value, scale) == value;
}
