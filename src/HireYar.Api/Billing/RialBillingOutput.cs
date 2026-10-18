using HireYar.Application.Billing;
using HireYar.Application.Common;

namespace HireYar.Api.Billing;

public static class RialBillingOutput
{
    private static decimal Usd(decimal value, AiDisplayRateDto? rate) => value * (rate?.RialPerUsdt ?? throw new InvalidOperationException("A live rate is required."));
    private static decimal? Usd(decimal? value, AiDisplayRateDto? rate) => value.HasValue ? Usd(value.Value, rate) : null;
    private static decimal Money(decimal value, string currency, AiDisplayRateDto? rate) => currency switch
    {
        "USD" => Usd(value, rate), "IRT" => value * 10m, "IRR" => value,
        _ => throw new InvalidOperationException("Unsupported financial currency.")
    };
    private static decimal? Money(decimal? value, string currency, AiDisplayRateDto? rate) => value.HasValue ? Money(value.Value, currency, rate) : null;

    public static RialPeriodDto Map(PeriodDto x, AiDisplayRateDto? rate) => new(
        x.Id,
        x.Months,
        Money(x.Price, x.Currency, rate),
        "IRR",
        x.DiscountPercent,
        x.IsActive,
        x.SortOrder,
        rate);

    public static RialPackageDto Map(PackageDto x, AiDisplayRateDto? rate) => new(
        x.Id,
        x.Code,
        x.Name,
        x.Description,
        x.IsActive,
        x.IsPublic,
        x.SortOrder,
        rate is null ? null : decimal.Round(x.AiPricePerMinuteUsd * rate.RialPerUsdt, 6, MidpointRounding.ToEven),
        x.RetentionDays,
        x.RowVersion,
        x.Features,
        x.Periods.Select(v => Map(v, rate)).ToArray(),
        x.IncludedTeamUsers,
        x.PaidTeamUserLimit,
        x.TeamUserMonthlyPriceIrt * 10m,
        x.AiPricePerMinuteUsd,
        "IRR",
        rate,
        x.SpecializedAiPricePerMinuteUsd,
        (x.InterviewTypes ?? []).Select(t => new RialPackageInterviewTypeDto(t.Code, t.Name, t.EngineType, t.IsActive,
            t.TypeIsActive, t.PricePerMinuteUsd, rate is null ? null : decimal.Round(t.PricePerMinuteUsd * rate.RialPerUsdt, 6, MidpointRounding.ToEven), "IRR")).ToArray());

    public static RialCreditPackageDto Map(CreditPackageDto x, AiDisplayRateDto? rate) => new(
        x.Id,
        x.Name,
        x.CreditAmountRial,
        Money(x.SellPriceAmount, x.SellPriceCurrency, rate),
        "IRR",
        x.IsActive,
        x.IsPublic,
        x.SortOrder,
        x.RowVersion,
        "IRR",
        rate);

    public static RialSubscriptionDto Map(SubscriptionDto x, AiDisplayRateDto? rate) => new(
        x.Id,
        x.PackageCode,
        x.PackageName,
        x.Status,
        x.StartedAtUtc,
        x.ExpiresAtUtc,
        rate is null ? null : Usd(x.AiRatePerMinuteUsd, rate),
        x.RetentionDays,
        x.Features,
        x.IsTrial,
        x.IncludedTeamUsers,
        x.PaidTeamUserLimit,
        x.TeamUserMonthlyPriceIrt * 10m,
        x.TeamCapacity,
        "IRR",
        rate);

    public static RialWalletDto Map(WalletDto x, AiDisplayRateDto? rate) => new(
        "IRR",
        Money(x.Balance, x.Currency, rate),
        Money(x.ReservedCredit, x.Currency, rate),
        Money(x.AvailableCredit, x.Currency, rate),
        rate);

    public static RialTransactionDto Map(TransactionDto x, AiDisplayRateDto? rate) => new(
        x.Id,
        x.Type,
        x.Amount,
        x.BalanceBefore,
        x.BalanceAfter,
        x.ReferenceType,
        x.ReferenceId,
        x.Description,
        x.CreatedAtUtc,
        "IRR",
        rate);

    public static RialPaymentDto Map(PaymentDto x, AiDisplayRateDto? rate) => new(
        x.Id,
        x.Type,
        Money(x.Amount, x.Currency, rate),
        "IRR",
        x.Status,
        x.Provider,
        x.CreatedAtUtc,
        x.PaidAtUtc,
        x.ExpiresAtUtc,
        rate);

    public static RialPurchaseResult Map(PurchaseResult x, AiDisplayRateDto? rate) => new(
        Map(x.Payment, rate),
        x.RedirectUrl,
        x.GatewayEnabled,
        "IRR",
        rate);

    public static RialSubscriptionOverviewDto Map(SubscriptionOverviewDto x, AiDisplayRateDto? rate) => new(
        x.Current is null ? null : Map(x.Current, rate),
        x.Scheduled.Select(v => Map(v, rate)).ToArray(),
        "IRR",
        rate);

    public static RialSubscriptionPurchaseDto Map(SubscriptionPurchaseDto x, AiDisplayRateDto? rate) => new(
        x.Id,
        x.PaymentId,
        x.PackageCode,
        x.PackageName,
        x.PeriodMonths,
        Money(x.PriceAmount, x.Currency, rate),
        "IRR",
        x.DiscountPercent,
        rate is null ? null : Usd(x.AiRatePerMinuteUsd, rate),
        x.RetentionDays,
        x.QuotedAtUtc,
        rate);

    public static RialUsageDto Map(UsageDto x, AiDisplayRateDto? rate) => new(
        x.InterviewSessionId,
        x.RatePerMinuteRial,
        x.BillableSeconds,
        x.BilledAmountRial,
        x.Status,
        x.SettledAtUtc,
        "IRR",
        rate);

    public static RialBillingPanelDto Map(BillingPanelDto x, AiDisplayRateDto? rate) => new(
        x.UpdatedAtUtc,
        x.SubscriptionState,
        x.Current is null ? null : Map(x.Current, rate),
        x.LastExpired is null ? null : Map(x.LastExpired, rate),
        x.Scheduled.Select(v => Map(v, rate)).ToArray(),
        x.DaysUntilExpiry,
        Map(x.Wallet, rate),
        rate is null ? null : Usd(x.AiRatePerMinuteUsd, rate),
        x.EstimatedAvailableMinutes,
        x.CanPurchase,
        x.GatewayEnabled,
        "IRR",
        rate);

    public static RialCheckoutSummaryDto Map(CheckoutSummaryDto x, AiDisplayRateDto? rate) => new(
        x.PackagePeriodId,
        x.PackageName,
        x.Months,
        x.Operation,
        "IRR",
        Money(x.OriginalAmount, x.Currency, rate),
        Money(x.DiscountAmount, x.Currency, rate),
        x.DiscountPercent,
        Money(x.SubtotalAmount, x.Currency, rate),
        Money(x.ProratedCreditAmount, x.Currency, rate),
        x.TaxPercent,
        Money(x.TaxAmount, x.Currency, rate),
        Money(x.TotalAmount, x.Currency, rate),
        x.EstimatedStartsAtUtc,
        x.EstimatedExpiresAtUtc,
        x.ReplacesSubscriptionId,
        rate is null ? null : Usd(x.AiRatePerMinuteUsd, rate),
        x.RetentionDays,
        x.Features,
        x.QuoteFingerprint,
        x.TeamCapacity,
        x.TeamSeatAmountIrt * 10m,
        rate);

    public static RialPanelCheckoutResult Map(PanelCheckoutResult x, AiDisplayRateDto? rate) => new(
        Map(x.Checkout, rate),
        Map(x.Summary, rate),
        "IRR",
        rate);

    public static RialFinancialHistoryItemDto Map(FinancialHistoryItemDto x, AiDisplayRateDto? rate) => new(
        x.Id,
        x.OccurredAtUtc,
        x.Description,
        x.CreditType,
        x.TransactionType,
        Money(x.Amount, x.Currency, rate),
        "IRR",
        x.CashPaidIrt * 10m,
        x.BillableSeconds,
        x.InterviewId,
        x.CandidateName,
        x.TrackingNumber,
        x.InvoiceUrl,
        rate);

    public static RialFinancialTotalsDto Map(FinancialTotalsDto x, AiDisplayRateDto? rate) => new(
        x.SubscriptionPaidIrt * 10m,
        x.CreditPurchasedRial,
        x.CreditConsumedRial,
        x.BillableSeconds,
        x.CreditPaidIrt * 10m,
        x.TeamSeatsPaidIrt * 10m,
        "IRR",
        rate);

    public static RialFinancialHistoryDto Map(FinancialHistoryDto x, AiDisplayRateDto? rate) => new(
        x.UpdatedAtUtc,
        new(x.Transactions.Items.Select(v => Map(v, rate)).ToArray(), x.Transactions.Total, x.Transactions.PageNumber, x.Transactions.PageSize),
        Map(x.Totals, rate),
        "IRR",
        rate);

    public static RialInvoiceDto Map(InvoiceDto x, AiDisplayRateDto? rate) => new(
        x.PaymentId,
        x.InvoiceNumber,
        x.TrackingNumber,
        x.PaidAtUtc,
        x.Type,
        x.Name,
        "IRR",
        Money(x.SubtotalAmount, x.Currency, rate),
        Money(x.ProratedCreditAmount, x.Currency, rate),
        x.TaxPercent,
        Money(x.TaxAmount, x.Currency, rate),
        Money(x.TotalAmount, x.Currency, rate),
        x.CreditAmountRial,
        x.Subscription is null ? null : Map(x.Subscription, rate),
        x.TeamSeats is null ? null : Map(x.TeamSeats, rate),
        rate);

    public static RialTeamSeatStateDto Map(TeamSeatStateDto x, AiDisplayRateDto? rate) => new(
        x.SubscriptionId,
        x.IncludedTeamUsers,
        x.PaidTeamUserLimit,
        x.TeamUserMonthlyPriceIrt * 10m,
        x.Capacity,
        x.IsUnlimited,
        x.OccupiedUsers,
        x.ExpiresAtUtc,
        "IRR",
        rate);

    public static RialTeamSeatQuoteDto Map(TeamSeatQuoteDto x, AiDisplayRateDto? rate) => new(
        x.SubscriptionId,
        x.PreviousCapacity,
        x.TargetCapacity,
        x.AdditionalBillableUsers,
        x.MonthlyPriceIrt * 10m,
        x.RemainingBillingMonths,
        x.AmountIrt * 10m,
        "IRR",
        x.QuotedAtUtc,
        x.ExpiresAtUtc,
        x.QuoteFingerprint,
        rate);

    public static RialTeamSeatCheckoutDto Map(TeamSeatCheckoutDto x, AiDisplayRateDto? rate) => new(
        Map(x.Checkout, rate),
        Map(x.Summary, rate),
        "IRR",
        rate);
}

