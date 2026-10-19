namespace HireYar.Domain.Subscriptions;

public static class SubscriptionDefaults
{
    public static readonly Guid PlanId = Guid.Parse("d1000000-0000-0000-0000-000000000001");
    public static long Price(long unitPriceRial, int quantity, decimal discountPercent = 0)
        => checked((long)decimal.Ceiling(unitPriceRial * (decimal)quantity * (100m - discountPercent) / 100m));
}
public sealed class SubscriptionPlan
{
    public Guid Id { get; set; } = SubscriptionDefaults.PlanId;
    public string Name { get; set; } = "اشتراک هایریار";
    public long MonthlyPriceRial { get; set; } = 90_000_000;
    public long BaseInterviewPriceRial { get; set; } = 4_000_000;
    public long ExtraUserMonthlyPriceRial { get; set; } = 2_000_000;
    public int IncludedUsers { get; set; } = 5;
    public int PaidUserCeiling { get; set; } = 30;
    public int RetentionDays { get; set; } = 60;
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}
public sealed class InterviewBundle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanId { get; set; } = SubscriptionDefaults.PlanId;
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int MinimumQuantity { get; set; }
    public int MaximumQuantity { get; set; }
    public int QuantityStep { get; set; } = 1;
    public decimal DiscountPercent { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}
public sealed class SubscriptionOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid RequestId { get; set; }
    public string RequestFingerprint { get; set; } = "";
    public string Product { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string ProductName { get; set; } = "";
    public string? BundleCode { get; set; }
    public int Quantity { get; set; }
    public int Months { get; set; }
    public int TeamUsers { get; set; }
    public bool UnlimitedTeam { get; set; }
    public long UnitPriceRial { get; set; }
    public long TeamMonthlyPriceRial { get; set; }
    public long OriginalAmountRial { get; set; }
    public long AmountRial { get; set; }
    public decimal DiscountPercent { get; set; }
    public int RetentionDays { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public Guid? ConfirmedByUserId { get; set; }
    public string? PaymentReference { get; set; }
}
public sealed class OrganizationMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid OrderId { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public int TeamUsers { get; set; }
    public bool UnlimitedTeam { get; set; }
    public int RetentionDays { get; set; }
}
public sealed class InterviewQuotaGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid OrderId { get; set; }
    public int Quantity { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
public sealed class InterviewQuotaReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid InterviewSessionId { get; set; }
    public string State { get; set; } = "Reserved";
    public int RetentionDays { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
    public DateTimeOffset? ReleasedAtUtc { get; set; }
}
