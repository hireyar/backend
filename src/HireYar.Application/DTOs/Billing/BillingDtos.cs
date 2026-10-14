using System.ComponentModel.DataAnnotations;
using HireYar.Domain.Billing;
namespace HireYar.Application.Billing;

public sealed record PackageRequest([Required, RegularExpression("^[a-z][a-z0-9_]{1,63}$")] string Code,
    [Required, StringLength(200, MinimumLength = 1)] string Name, [StringLength(4000)] string Description,
    bool IsActive, bool IsPublic, [Range(0, 10000)] int SortOrder,
    [Range(typeof(decimal), "0.000001", "1000")] decimal AiPricePerMinuteUsd, [Range(1, 3650)] int InterviewRetentionDays,
    [Range(1, 10000)] int IncludedTeamUsers = 3, [Range(1, 10000)] int PaidTeamUserLimit = 30,
    [Range(typeof(decimal), "0", "1000000000000")] decimal TeamUserMonthlyPriceIrt = 200_000m);
public sealed record PackageUpdateRequest(PackageRequest Package, [Required, MinLength(16), MaxLength(16)] byte[] RowVersion);
public sealed record PeriodRequest([Range(1, 120)] int Months, [Range(typeof(decimal), "1", "1000000000000")] decimal PriceAmount,
    [Required, RegularExpression("^IRT$")] string Currency, [Range(typeof(decimal), "0", "100")] decimal DiscountPercent,
    bool IsActive, [Range(0, 10000)] int SortOrder);
public sealed record PeriodWriteRequest(PeriodRequest Period, [Required, MinLength(16), MaxLength(16)] byte[] RowVersion);
public sealed record FeatureRequest([Required, RegularExpression("^[a-z][a-z0-9_]{1,63}$")] string Code,
    [Required, StringLength(200, MinimumLength = 1)] string Name, [StringLength(4000)] string Description,
    [EnumDataType(typeof(FeatureDataType))] FeatureDataType DataType);
public sealed record FeatureWriteRequest(Guid FeatureId, bool IsEnabled, [StringLength(1000)] string? Value,
    [Required, MinLength(16), MaxLength(16)] byte[] RowVersion);
public sealed record CreditPackageRequest([Required, StringLength(200, MinimumLength = 1)] string Name,
    [Range(typeof(decimal), "0.000001", "1000000")] decimal CreditAmountUsd,
    [Range(typeof(decimal), "1", "1000000000000")] decimal SellPriceAmount,
    [Required, RegularExpression("^IRT$")] string SellPriceCurrency, bool IsActive, bool IsPublic, [Range(0, 10000)] int SortOrder);
public sealed record CreditPackageUpdateRequest(CreditPackageRequest Package, [Required, MinLength(16), MaxLength(16)] byte[] RowVersion);
public sealed record SubscriptionPurchaseRequest(Guid PackagePeriodId, [Range(1, 10000)] int? TeamCapacity = null);
public sealed record CreditPurchaseRequest(Guid CreditPackageId);
public sealed record AdjustmentRequest(Guid IdempotencyKey, decimal Amount, [Required, StringLength(1000, MinimumLength = 1)] string Reason);
public sealed record FeatureDto(Guid Id, string Code, string Name, string Description, FeatureDataType DataType, bool IsEnabled, string? Value);
public sealed record PeriodDto(Guid Id, int Months, decimal Price, string Currency, decimal DiscountPercent, bool IsActive, int SortOrder);
public sealed record PackageDto(Guid Id, string Code, string Name, string Description, bool IsActive, bool IsPublic, int SortOrder,
    decimal AiPricePerMinuteUsd, int RetentionDays, byte[] RowVersion, IReadOnlyList<FeatureDto> Features, IReadOnlyList<PeriodDto> Periods,
    int IncludedTeamUsers = 3, int PaidTeamUserLimit = 30, decimal TeamUserMonthlyPriceIrt = 200_000m);
public sealed record CreditPackageDto(Guid Id, string Name, decimal CreditAmountUsd, decimal SellPriceAmount, string SellPriceCurrency,
    bool IsActive, bool IsPublic, int SortOrder, byte[] RowVersion);
public sealed record EntitlementDto(string Code, string Name, FeatureDataType DataType, string? Value);
public sealed record SubscriptionDto(Guid Id, string PackageCode, string PackageName, string Status, DateTimeOffset StartedAtUtc,
    DateTimeOffset ExpiresAtUtc, decimal AiRatePerMinuteUsd, int RetentionDays, IReadOnlyList<EntitlementDto> Features, bool IsTrial = false,
    int IncludedTeamUsers = 3, int PaidTeamUserLimit = 30, decimal TeamUserMonthlyPriceIrt = 200_000m, int TeamCapacity = 3);
public sealed record WalletDto(string Currency, decimal Balance, decimal ReservedCredit, decimal AvailableCredit);
public sealed record TransactionDto(Guid Id, CreditTransactionType Type, decimal Amount, decimal BalanceBefore, decimal BalanceAfter,
    string ReferenceType, Guid ReferenceId, string? Description, DateTimeOffset CreatedAtUtc);
public sealed record PaymentDto(Guid Id, PaymentType Type, decimal Amount, string Currency, PaymentStatus Status,
    string Provider, DateTimeOffset CreatedAtUtc, DateTimeOffset? PaidAtUtc, DateTimeOffset ExpiresAtUtc);
public sealed record PurchaseResult(PaymentDto Payment, string? RedirectUrl, bool GatewayEnabled);
public sealed record SubscriptionOverviewDto(SubscriptionDto? Current, IReadOnlyList<SubscriptionDto> Scheduled);
public sealed record SubscriptionPurchaseDto(Guid Id, Guid PaymentId, string PackageCode, string PackageName, int PeriodMonths,
    decimal PriceAmount, string Currency, decimal DiscountPercent, decimal AiRatePerMinuteUsd, int RetentionDays, DateTimeOffset QuotedAtUtc);
public sealed record UsageDto(Guid InterviewSessionId, decimal RatePerMinuteUsd, int BillableSeconds, decimal BilledAmountUsd,
    InterviewBillingStatus Status, DateTimeOffset? SettledAtUtc);
