using System.ComponentModel.DataAnnotations;
namespace HireYar.Application.Subscriptions;

public sealed record PlanDto(Guid Id, string Name, string Currency, long MonthlyPriceRial, long BaseInterviewPriceRial,
    long ExtraUserMonthlyPriceRial, int IncludedUsers, int PaidUserCeiling, int RetentionDays, int MaximumInterviewMinutes,
    bool IsActive, byte[] RowVersion, IReadOnlyList<BundleDto> Bundles);
public sealed record BundleDto(Guid Id, string Code, string Name, int MinimumQuantity, int MaximumQuantity, int QuantityStep,
    decimal DiscountPercent, long StartingOriginalAmountRial, long StartingAmountRial, bool IsActive, int SortOrder, byte[] RowVersion);
public sealed record PlanUpdateRequest([Required, StringLength(200)] string Name,
    [Range(1, 1_000_000_000_000)] long MonthlyPriceRial, [Range(1, 1_000_000_000_000)] long BaseInterviewPriceRial,
    [Range(0, 1_000_000_000_000)] long ExtraUserMonthlyPriceRial,
    [Range(1, 100)] int IncludedUsers, [Range(2, 10000)] int PaidUserCeiling,
    [Range(1, 3650)] int RetentionDays, bool IsActive, [Required, MinLength(16), MaxLength(16)] byte[] RowVersion);
public sealed record BundleUpdateRequest([Required, StringLength(200)] string Name,
    [Range(1, 1_000_000)] int MinimumQuantity, [Range(1, 1_000_000)] int MaximumQuantity,
    [Range(1, 1_000_000)] int QuantityStep, [Range(0, 99.99)] decimal DiscountPercent,
    [Range(0, 100)] int SortOrder, bool IsActive, [Required, MinLength(16), MaxLength(16)] byte[] RowVersion);
public sealed record PurchaseSelection([Required, RegularExpression("^(subscription|interviews)$")] string Product,
    int Months = 1, int TeamUsers = 5, [StringLength(64)] string? BundleCode = null, int? Quantity = null);
public sealed record PurchaseQuote(string Product, string ProductName, string Currency, int Months, int TeamUsers,
    bool UnlimitedTeam, int RetentionDays, string? BundleCode, int Quantity, long UnitPriceRial, long TeamMonthlyPriceRial,
    decimal DiscountPercent, long OriginalAmountRial, long DiscountAmountRial, long AmountRial,
    byte[] PlanVersion, byte[]? BundleVersion);
public sealed record CreateSubscriptionOrderRequest(Guid RequestId, [Required] PurchaseSelection Selection,
    [Required, MinLength(16), MaxLength(16)] byte[] PlanVersion, byte[]? BundleVersion = null);
public sealed record ConfirmSubscriptionOrderRequest([Required, StringLength(120, MinimumLength = 1)] string PaymentReference);
public sealed record SubscriptionOrderDto(Guid Id, Guid OrganizationId, string Product, string ProductName, string Status, string Currency,
    long AmountRial, long OriginalAmountRial, decimal DiscountPercent, int Months, int TeamUsers, bool UnlimitedTeam,
    string? BundleCode, int Quantity, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc, DateTimeOffset? ConfirmedAtUtc);
public sealed record SubscriptionStateDto(bool HasActiveSubscription, DateTimeOffset? StartedAtUtc, DateTimeOffset? ExpiresAtUtc,
    int RemainingDays, long RemainingInterviews, long AvailableInterviews, int ReservedInterviews, int ConsumedInterviews,
    int? TeamCapacity, bool UnlimitedTeam, int OccupiedTeamUsers);
