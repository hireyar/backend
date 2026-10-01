using HireYar.Application.Common;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Billing;

public sealed class PaymentService(IApplicationDbContext db, IPaymentGateway gateway, TimeProvider time, WalletService wallet)
{
    public async Task<PurchaseResult> PurchaseSubscriptionAsync(Guid org, Guid periodId, CancellationToken ct)
    {
        // A single PostgreSQL statement snapshots the entire contract consistently despite the global split-query default.
        var package = await db.PricingCatalog.AsNoTracking().Include(x => x.Periods).Include(x => x.Features).ThenInclude(x => x.Feature)
            .SingleOrDefaultAsync(x => x.IsActive && x.IsPublic && x.Periods.Any(p => p.Id == periodId && p.IsActive), ct) ?? throw AppException.NotFound();
        var period = package.Periods.Single(x => x.Id == periodId); var now = time.GetUtcNow();
        var payment = NewPayment(org, PaymentType.Subscription, period.PriceAmount, period.Currency, now);
        var purchase = new SubscriptionPurchase { OrganizationId = org, PaymentId = payment.Id, PackageId = package.Id, PackagePeriodId = period.Id,
            PackageCodeSnapshot = package.Code, PackageNameSnapshot = package.Name, PeriodMonthsSnapshot = period.Months,
            PriceAmountSnapshot = period.PriceAmount, CurrencySnapshot = period.Currency, DiscountPercentSnapshot = period.DiscountPercent,
            AiPricePerMinuteUsdSnapshot = package.AiPricePerMinuteUsd, RetentionDaysSnapshot = package.InterviewRetentionDays, CreatedAtUtc = now };
        purchase.Entitlements = package.Features.Where(x => x.IsEnabled).Select(x => new SubscriptionEntitlement {
            PurchaseId = purchase.Id, Code = x.Feature.Code, Name = x.Feature.Name, DataType = x.Feature.DataType, Value = x.Value }).ToList();
        db.Payments.Add(payment); db.SubscriptionPurchases.Add(purchase); await db.SaveChangesAsync(ct);
        return await InitiateAsync(payment, ct);
    }
    public async Task<PurchaseResult> PurchaseCreditAsync(Guid org, Guid creditId, CancellationToken ct)
    {
        var product = await db.AiCreditPackages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == creditId && x.IsActive && x.IsPublic, ct) ?? throw AppException.NotFound();
        var payment = NewPayment(org, PaymentType.AiCredit, product.SellPriceAmount, product.SellPriceCurrency, time.GetUtcNow());
        payment.CreditPackageId = product.Id; payment.CreditPackageNameSnapshot = product.Name; payment.CreditAmountUsdSnapshot = product.CreditAmountUsd;
        payment.EffectiveUsdToIrtSnapshot = decimal.Round(product.SellPriceAmount / product.CreditAmountUsd, 6, MidpointRounding.ToEven);
        db.Payments.Add(payment); await db.SaveChangesAsync(ct); return await InitiateAsync(payment, ct);
    }
    private Payment NewPayment(Guid org, PaymentType type, decimal amount, string currency, DateTimeOffset now) => new() {
        OrganizationId = org, Type = type, Amount = amount, Currency = currency, Provider = gateway.Provider,
        Status = PaymentStatus.Pending, CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(30) };
    private async Task<PurchaseResult> InitiateAsync(Payment payment, CancellationToken ct)
    {
        if (!gateway.IsEnabled) return new(Map(payment), null, false);
        var result = await gateway.CreatePaymentAsync(new(payment.Id, payment.Amount, payment.Currency), ct);
        if (string.IsNullOrWhiteSpace(result.Authority) || result.Authority.Length > 512 || !Uri.TryCreate(result.RedirectUrl, UriKind.Absolute, out var redirect) || redirect.Scheme != "https")
            throw new AppException(503, "payment_gateway_invalid_response", "Payment initiation failed.");
        payment.ProviderAuthority = result.Authority; await db.SaveChangesAsync(ct); return new(Map(payment), result.RedirectUrl, true);
    }
    public async Task<PaymentDto> VerifyAsync(Guid org, Guid id, CancellationToken ct)
    {
        var quote = await db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == org, ct) ?? throw AppException.NotFound();
        if (quote.Status == PaymentStatus.Paid) return Map(quote);
        if (quote.Status != PaymentStatus.Pending) return Map(quote);
        if (!gateway.IsEnabled || quote.Provider != gateway.Provider || quote.ProviderAuthority is null)
            throw new AppException(503, "payment_gateway_unavailable", "Payment verification is unavailable.");
        // Provider verification is authoritative. No client-supplied amount, success flag, authority or transaction id.
        var result = await gateway.VerifyPaymentAsync(new(id, quote.Amount, quote.Currency), quote.ProviderAuthority, ct);
        bool fulfilled = false, failed = false;
        var verified = await db.WithOrganizationBillingLockAsync(org, async () => {
            var payment = await db.Payments.SingleAsync(x => x.Id == id && x.OrganizationId == org, ct);
            if (payment.Status != PaymentStatus.Pending) return Map(payment);
            var now = time.GetUtcNow();
            if (!result.IsPaid || result.Amount != payment.Amount || result.Currency != payment.Currency || string.IsNullOrWhiteSpace(result.TransactionId) || result.TransactionId.Length > 200)
            {
                payment.Status = PaymentStatus.Failed; payment.FailedAtUtc = now; failed = true; return Map(payment);
            }
            // Late verified money must be fulfilled, even after the checkout display timeout.
            payment.Status = PaymentStatus.Paid; payment.PaidAtUtc = now; payment.ProviderTransactionId = result.TransactionId;
            if (payment.Type == PaymentType.Subscription)
            {
                var p = await db.SubscriptionPurchases.SingleAsync(x => x.OrganizationId == org && x.PaymentId == id, ct);
                var end = await db.OrganizationSubscriptions.Where(x => x.OrganizationId == org &&
                    (x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.Scheduled) && x.ExpiresAtUtc > now)
                    .MaxAsync(x => (DateTimeOffset?)x.ExpiresAtUtc, ct);
                var start = end ?? now;
                db.OrganizationSubscriptions.Add(new OrganizationSubscription { OrganizationId = org, PaymentId = id, PackageId = p.PackageId,
                    PackagePeriodId = p.PackagePeriodId, StartedAtUtc = start, ExpiresAtUtc = start.AddMonths(p.PeriodMonthsSnapshot),
                    Status = start > now ? SubscriptionStatus.Scheduled : SubscriptionStatus.Active, CreatedAtUtc = now });
            }
            else await wallet.AddEntryLockedAsync(org, payment.CreditAmountUsdSnapshot!.Value, CreditTransactionType.CreditPurchase, "Payment", id, null, ct);
            fulfilled = true; return Map(payment);
        }, ct);
        if (fulfilled) (verified.Type == PaymentType.Subscription ? BillingMetrics.Subscriptions : BillingMetrics.CreditPurchases).Add(1);
        if (failed) BillingMetrics.VerificationFailures.Add(1);
        return verified;
    }
    public static PaymentDto Map(Payment p) => new(p.Id, p.Type, p.Amount, p.Currency, p.Status, p.Provider, p.CreatedAtUtc, p.PaidAtUtc, p.ExpiresAtUtc);
    public async Task VerifyCallbackAsync(Guid id, CancellationToken ct)
    {
        var org = await db.Payments.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.OrganizationId).SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();
        await VerifyAsync(org, id, ct);
    }
}
