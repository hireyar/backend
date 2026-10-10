namespace HireYar.Domain.Billing;

public enum PaymentType { Subscription, AiCredit }
public enum PaymentStatus { Pending, Paid, Failed, Cancelled, Expired }
public sealed class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public PaymentType Type { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "IRT";
    public PaymentStatus Status { get; set; }
    public string Provider { get; set; } = "";
    public string? ProviderAuthority { get; set; }
    public string? ProviderTransactionId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? PaidAtUtc { get; set; }
    public DateTimeOffset? FailedAtUtc { get; set; }
    public Guid? CreditPackageId { get; set; }
    public string? CreditPackageNameSnapshot { get; set; }
    public decimal? CreditAmountUsdSnapshot { get; set; }
    // Fixed-price credit products: this is the effective contracted IRT/USD ratio, not a live forex quote.
    public decimal? EffectiveUsdToIrtSnapshot { get; set; }
    public decimal? SubtotalAmountSnapshot { get; set; }
    public decimal? TaxPercentSnapshot { get; set; }
    public decimal? TaxAmountSnapshot { get; set; }
}
public enum SubscriptionStatus { Scheduled, Active, Expired, Cancelled, Suspended }
// Append-only cutover: preserve the purchased contract and interviews that started against it.
public sealed class SubscriptionReplacement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SubscriptionId { get; set; }
    public Guid PaymentId { get; set; }
    public DateTimeOffset EndedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
public sealed class OrganizationSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid? PaymentId { get; set; }
    public bool IsTrial { get; set; }
    public Guid PackageId { get; set; }
    public Guid PackagePeriodId { get; set; }
    public SubscriptionStatus Status { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
// Created with the pending payment, then never edited: the quote is the purchased contract.
public sealed class SubscriptionPurchase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid PackageId { get; set; }
    public Guid PackagePeriodId { get; set; }
    public string PackageCodeSnapshot { get; set; } = "";
    public string PackageNameSnapshot { get; set; } = "";
    public int PeriodMonthsSnapshot { get; set; }
    public decimal PriceAmountSnapshot { get; set; }
    public string CurrencySnapshot { get; set; } = "IRT";
    public decimal DiscountPercentSnapshot { get; set; }
    public decimal AiPricePerMinuteUsdSnapshot { get; set; }
    public int RetentionDaysSnapshot { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public List<SubscriptionEntitlement> Entitlements { get; set; } = [];
    public string Operation { get; set; } = "purchase";
    public decimal? OriginalAmountSnapshot { get; set; }
    public decimal? DiscountAmountSnapshot { get; set; }
    public decimal ProratedCreditAmountSnapshot { get; set; }
    public Guid? ReplacesSubscriptionId { get; set; }
}
public sealed class SubscriptionEntitlement
{
    public Guid PurchaseId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public FeatureDataType DataType { get; set; }
    public string? Value { get; set; }
}
public sealed class OrganizationAiWallet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal Balance { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
public enum CreditTransactionType { CreditPurchase, InterviewUsage, Refund, AdminAdjustment, PromotionalCredit, Correction }
public sealed class AiCreditTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid WalletId { get; set; }
    public CreditTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string ReferenceType { get; set; } = "";
    public Guid ReferenceId { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
public enum ReservationStatus { Active, Consumed, Released, Expired }
public sealed class AiCreditReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid WalletId { get; set; }
    public Guid InterviewSessionId { get; set; }
    public decimal ReservedAmountUsd { get; set; }
    public decimal ConsumedAmountUsd { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
public enum InterviewBillingStatus { Reserved, Settled, Released }
public sealed class InterviewBilling
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid InterviewSessionId { get; set; }
    public Guid SubscriptionId { get; set; }
    public decimal RatePerMinuteUsd { get; set; }
    public int RetentionDays { get; set; }
    public int BillableSeconds { get; set; }
    public decimal BilledAmountUsd { get; set; }
    public InterviewBillingStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? SettledAtUtc { get; set; }
}
