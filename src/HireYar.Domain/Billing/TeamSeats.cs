namespace HireYar.Domain.Billing;

// Append-only quote; a verified payment grants capacity only for this subscription.
public sealed class TeamSeatPurchase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SubscriptionId { get; set; }
    public Guid PaymentId { get; set; }
    public int PreviousCapacity { get; set; }
    public int TargetCapacity { get; set; }
    public decimal MonthlyPriceIrt { get; set; }
    public decimal AmountIrt { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset QuotedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

public static class TeamSeatMath
{
    public static int BillableUsers(int users, int included, int paidLimit) => Math.Max(0, Math.Min(users, paidLimit) - included);
    public static decimal ProratedAmount(int users, decimal monthlyPrice, DateTimeOffset from, DateTimeOffset until)
    {
        if (users < 0 || monthlyPrice < 0 || until <= from) throw new ArgumentOutOfRangeException(nameof(users));
        // A billing month is exactly 30 UTC days; round only the total, to whole toman.
        var seconds = (until - from).Ticks / (decimal)TimeSpan.TicksPerSecond;
        return decimal.Ceiling(users * monthlyPrice * seconds / (30m * 86400));
    }
}
