using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;

namespace HireYar.Application.Billing;

public sealed class BillingPanelOptions
{
    public decimal SubscriptionTaxPercent { get; set; } = 10m;
}
public sealed record BillingPanelDto(DateTimeOffset UpdatedAtUtc, string SubscriptionState,
    SubscriptionDto? Current, SubscriptionDto? LastExpired, IReadOnlyList<SubscriptionDto> Scheduled,
    int? DaysUntilExpiry, WalletDto Wallet, decimal? AiRatePerMinuteUsd, decimal? EstimatedAvailableMinutes,
    bool CanPurchase, bool GatewayEnabled);
public sealed record SubscriptionQuoteRequest(Guid PackagePeriodId,
    [Required, RegularExpression("^(purchase|renewal|upgrade)$")] string Operation = "purchase",
    [RegularExpression("^[A-F0-9]{64}$")] string? QuoteFingerprint = null, [Range(1, 10000)] int? TeamCapacity = null);
public sealed record CheckoutSummaryDto(Guid PackagePeriodId, string PackageName, int Months, string Operation,
    string Currency, decimal? OriginalAmount, decimal? DiscountAmount, decimal DiscountPercent,
    decimal SubtotalAmount, decimal ProratedCreditAmount, decimal TaxPercent, decimal TaxAmount, decimal TotalAmount,
    DateTimeOffset EstimatedStartsAtUtc, DateTimeOffset EstimatedExpiresAtUtc, Guid? ReplacesSubscriptionId,
    decimal AiRatePerMinuteUsd, int RetentionDays, IReadOnlyList<EntitlementDto> Features, string? QuoteFingerprint = null,
    int TeamCapacity = 3, decimal TeamSeatAmountIrt = 0);
public sealed record PanelCheckoutResult(PurchaseResult Checkout, CheckoutSummaryDto Summary);
public sealed class FinancialHistoryQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    public int Skip => (int)Math.Min((long)(Page - 1) * PageSize, int.MaxValue);
    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
    [RegularExpression("^(subscription|aiCredit|teamSeats)$")] public string? CreditType { get; set; }
    [RegularExpression("^(payment|usage|adjustment)$")] public string? TransactionType { get; set; }
}
public sealed record FinancialHistoryItemDto(Guid Id, DateTimeOffset OccurredAtUtc, string Description,
    string CreditType, string TransactionType, decimal Amount, string Currency, decimal? CashPaidIrt,
    int? BillableSeconds, Guid? InterviewId, string? CandidateName, string TrackingNumber, string? InvoiceUrl);
public sealed record FinancialTotalsDto(decimal SubscriptionPaidIrt, decimal CreditPurchasedRial,
    decimal CreditConsumedRial, long BillableSeconds, decimal CreditPaidIrt, decimal TeamSeatsPaidIrt = 0);
public sealed record FinancialHistoryDto(DateTimeOffset UpdatedAtUtc, Page<FinancialHistoryItemDto> Transactions, FinancialTotalsDto Totals);
public sealed record InvoiceDto(Guid PaymentId, string InvoiceNumber, string TrackingNumber, DateTimeOffset PaidAtUtc,
    string Type, string Name, string Currency, decimal SubtotalAmount, decimal ProratedCreditAmount,
    decimal TaxPercent, decimal TaxAmount, decimal TotalAmount, decimal? CreditAmountRial, CheckoutSummaryDto? Subscription,
    TeamSeatQuoteDto? TeamSeats = null);
