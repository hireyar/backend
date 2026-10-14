using System.ComponentModel.DataAnnotations;

namespace HireYar.Application.Billing;

public sealed record TeamSeatStateDto(Guid? SubscriptionId, int IncludedTeamUsers, int PaidTeamUserLimit,
    decimal TeamUserMonthlyPriceIrt, int Capacity, bool IsUnlimited, int OccupiedUsers, DateTimeOffset? ExpiresAtUtc);
public sealed record TeamSeatQuoteRequest([Range(1, 10000)] int TargetCapacity,
    DateTimeOffset? QuotedAtUtc = null, [RegularExpression("^[A-F0-9]{64}$")] string? QuoteFingerprint = null);
public sealed record TeamSeatQuoteDto(Guid SubscriptionId, int PreviousCapacity, int TargetCapacity,
    int AdditionalBillableUsers, decimal MonthlyPriceIrt, decimal RemainingBillingMonths, decimal AmountIrt,
    string Currency, DateTimeOffset QuotedAtUtc, DateTimeOffset ExpiresAtUtc, string QuoteFingerprint);
public sealed record TeamSeatCheckoutDto(PurchaseResult Checkout, TeamSeatQuoteDto Summary);
