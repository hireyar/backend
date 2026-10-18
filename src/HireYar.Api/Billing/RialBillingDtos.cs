using HireYar.Application.Billing;
using HireYar.Application.Common;
using HireYar.Domain.Billing;

namespace HireYar.Api.Billing;

public sealed record RialPackageCatalogDto(IReadOnlyList<RialPackageDto> Packages);
public sealed record RialCreditPackageCatalogDto(IReadOnlyList<RialCreditPackageDto> Packages);

// HTTP contracts: AI accounting is IRR; legacy cash payment IRT amounts are normalized to IRR.
public sealed record RialPeriodDto(
    Guid Id,
    int Months,
    decimal Price,
    string Currency,
    decimal DiscountPercent,
    bool IsActive,
    int SortOrder,
    AiDisplayRateDto? DisplayRate);

public sealed record RialPackageDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    bool IsActive,
    bool IsPublic,
    int SortOrder,
    decimal? AiPricePerMinuteRial,
    int RetentionDays,
    byte[] RowVersion,
    IReadOnlyList<FeatureDto> Features,
    IReadOnlyList<RialPeriodDto> Periods,
    int IncludedTeamUsers,
    int PaidTeamUserLimit,
    decimal TeamUserMonthlyPriceRial,
    decimal AiPricePerMinuteUsd,
    string Currency,
    AiDisplayRateDto? DisplayRate,
    decimal? GeneralAiPricePerMinuteUsd = null,
    decimal? SpecializedAiPricePerMinuteUsd = null)
{
    public decimal? GeneralAiPricePerMinuteRial => DisplayRate is null || GeneralAiPricePerMinuteUsd is null ? null : decimal.Round(GeneralAiPricePerMinuteUsd.Value * DisplayRate.RialPerUsdt, 6, MidpointRounding.ToEven);
    public decimal? SpecializedAiPricePerMinuteRial => DisplayRate is null || SpecializedAiPricePerMinuteUsd is null ? null : decimal.Round(SpecializedAiPricePerMinuteUsd.Value * DisplayRate.RialPerUsdt, 6, MidpointRounding.ToEven);
}

public sealed record RialCreditPackageDto(
    Guid Id,
    string Name,
    decimal CreditAmountRial,
    decimal SellPriceAmount,
    string SellPriceCurrency,
    bool IsActive,
    bool IsPublic,
    int SortOrder,
    byte[] RowVersion,
    string Currency,
    AiDisplayRateDto? DisplayRate);

public sealed record RialSubscriptionDto(
    Guid Id,
    string PackageCode,
    string PackageName,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    decimal? AiRatePerMinuteRial,
    int RetentionDays,
    IReadOnlyList<EntitlementDto> Features,
    bool IsTrial,
    int IncludedTeamUsers,
    int PaidTeamUserLimit,
    decimal TeamUserMonthlyPriceRial,
    int TeamCapacity,
    string Currency,
    AiDisplayRateDto? DisplayRate);

public sealed record RialWalletDto(
    string Currency,
    decimal Balance,
    decimal ReservedCredit,
    decimal AvailableCredit,
    AiDisplayRateDto? DisplayRate);

public sealed record RialTransactionDto(
    Guid Id,
    CreditTransactionType Type,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string ReferenceType,
    Guid ReferenceId,
    string? Description,
    DateTimeOffset CreatedAtUtc,
    string Currency,
    AiDisplayRateDto? DisplayRate);

public sealed record RialPaymentDto(
    Guid Id,
    PaymentType Type,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string Provider,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset ExpiresAtUtc,
    AiDisplayRateDto? DisplayRate);

public sealed record RialPurchaseResult(
    RialPaymentDto Payment,
    string? RedirectUrl,
    bool GatewayEnabled,
    string Currency,
    AiDisplayRateDto? DisplayRate);

public sealed record RialSubscriptionOverviewDto(
    RialSubscriptionDto? Current,
    IReadOnlyList<RialSubscriptionDto> Scheduled,
    string Currency,
    AiDisplayRateDto? DisplayRate);

public sealed record RialSubscriptionPurchaseDto(
    Guid Id,
    Guid PaymentId,
    string PackageCode,
    string PackageName,
    int PeriodMonths,
    decimal PriceAmount,
    string Currency,
    decimal DiscountPercent,
    decimal? AiRatePerMinuteRial,
    int RetentionDays,
    DateTimeOffset QuotedAtUtc,
    AiDisplayRateDto? DisplayRate);

public sealed record RialUsageDto(
    Guid InterviewSessionId,
    decimal RatePerMinuteRial,
    int BillableSeconds,
    decimal BilledAmountRial,
    InterviewBillingStatus Status,
    DateTimeOffset? SettledAtUtc,
    string Currency,
    AiDisplayRateDto? DisplayRate);

public sealed record RialBillingPanelDto(
    DateTimeOffset UpdatedAtUtc,
    string SubscriptionState,
    RialSubscriptionDto? Current,
    RialSubscriptionDto? LastExpired,
    IReadOnlyList<RialSubscriptionDto> Scheduled,
    int? DaysUntilExpiry,
    RialWalletDto Wallet,
    decimal? AiRatePerMinuteRial,
    decimal? EstimatedAvailableMinutes,
    bool CanPurchase,
    bool GatewayEnabled,
    string Currency,
    AiDisplayRateDto? DisplayRate);

public sealed record RialCheckoutSummaryDto(
    Guid PackagePeriodId,
    string PackageName,
    int Months,
    string Operation,
    string Currency,
    decimal? OriginalAmount,
    decimal? DiscountAmount,
    decimal DiscountPercent,
    decimal SubtotalAmount,
    decimal ProratedCreditAmount,
    decimal TaxPercent,
    decimal TaxAmount,
    decimal TotalAmount,
    DateTimeOffset EstimatedStartsAtUtc,
    DateTimeOffset EstimatedExpiresAtUtc,
    Guid? ReplacesSubscriptionId,
    decimal? AiRatePerMinuteRial,
    int RetentionDays,
    IReadOnlyList<EntitlementDto> Features,
    string? QuoteFingerprint,
    int TeamCapacity,
    decimal TeamSeatAmountRial,
    AiDisplayRateDto? DisplayRate);

public sealed record RialPanelCheckoutResult(
    RialPurchaseResult Checkout,
    RialCheckoutSummaryDto Summary,
    string Currency,
    AiDisplayRateDto? DisplayRate);

public sealed record RialFinancialHistoryItemDto(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string Description,
    string CreditType,
    string TransactionType,
    decimal Amount,
    string Currency,
    decimal? CashPaidRial,
    int? BillableSeconds,
    Guid? InterviewId,
    string? CandidateName,
    string TrackingNumber,
    string? InvoiceUrl,
    AiDisplayRateDto? DisplayRate);

public sealed record RialFinancialTotalsDto(
    decimal SubscriptionPaidRial,
    decimal CreditPurchasedRial,
    decimal CreditConsumedRial,
    long BillableSeconds,
    decimal CreditPaidRial,
    decimal TeamSeatsPaidRial,
    string Currency,
    AiDisplayRateDto? DisplayRate);

public sealed record RialFinancialHistoryDto(
    DateTimeOffset UpdatedAtUtc,
    Page<RialFinancialHistoryItemDto> Transactions,
    RialFinancialTotalsDto Totals,
    string Currency,
    AiDisplayRateDto? DisplayRate);

public sealed record RialInvoiceDto(
    Guid PaymentId,
    string InvoiceNumber,
    string TrackingNumber,
    DateTimeOffset PaidAtUtc,
    string Type,
    string Name,
    string Currency,
    decimal SubtotalAmount,
    decimal ProratedCreditAmount,
    decimal TaxPercent,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal? CreditAmountRial,
    RialCheckoutSummaryDto? Subscription,
    RialTeamSeatQuoteDto? TeamSeats,
    AiDisplayRateDto? DisplayRate);

public sealed record RialTeamSeatStateDto(
    Guid? SubscriptionId,
    int IncludedTeamUsers,
    int PaidTeamUserLimit,
    decimal TeamUserMonthlyPriceRial,
    int Capacity,
    bool IsUnlimited,
    int OccupiedUsers,
    DateTimeOffset? ExpiresAtUtc,
    string Currency,
    AiDisplayRateDto? DisplayRate);

public sealed record RialTeamSeatQuoteDto(
    Guid SubscriptionId,
    int PreviousCapacity,
    int TargetCapacity,
    int AdditionalBillableUsers,
    decimal MonthlyPriceRial,
    decimal RemainingBillingMonths,
    decimal AmountRial,
    string Currency,
    DateTimeOffset QuotedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string QuoteFingerprint,
    AiDisplayRateDto? DisplayRate);

public sealed record RialTeamSeatCheckoutDto(
    RialPurchaseResult Checkout,
    RialTeamSeatQuoteDto Summary,
    string Currency,
    AiDisplayRateDto? DisplayRate);


