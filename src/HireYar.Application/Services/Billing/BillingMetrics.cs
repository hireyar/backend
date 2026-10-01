using System.Diagnostics.Metrics;
namespace HireYar.Application.Billing;

internal static class BillingMetrics
{
    private static readonly Meter Meter = new("HireYar.Billing", "1.0");
    public static readonly Counter<long> Subscriptions = Meter.CreateCounter<long>("subscription.purchase.completed");
    public static readonly Counter<long> CreditPurchases = Meter.CreateCounter<long>("ai_credit.purchase.completed");
    public static readonly Counter<long> VerificationFailures = Meter.CreateCounter<long>("payment.verification.failed");
    public static readonly Counter<long> Reservations = Meter.CreateCounter<long>("ai_credit.reservation.created");
    public static readonly Counter<long> ReservationFailures = Meter.CreateCounter<long>("ai_credit.reservation.failed");
    public static readonly Counter<decimal> Consumed = Meter.CreateCounter<decimal>("ai_credit.consumed", "USD");
}
