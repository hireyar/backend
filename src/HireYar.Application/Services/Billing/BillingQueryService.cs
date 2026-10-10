using HireYar.Application.Common;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Billing;

public sealed class BillingQueryService(IApplicationDbContext db, ISubscriptionEntitlementService entitlements, TimeProvider time)
{
    public async Task<SubscriptionOverviewDto> SubscriptionsAsync(Guid org, CancellationToken ct)
    {
        if (!await db.Organizations.AnyAsync(x => x.Id == org, ct)) throw AppException.NotFound();
        var current = await entitlements.CurrentAsync(org, ct); var now = time.GetUtcNow();
        var scheduled = await db.OrganizationSubscriptions.AsNoTracking().Where(x => x.OrganizationId == org && x.StartedAtUtc > now && x.Status == SubscriptionStatus.Scheduled && !db.SubscriptionReplacements.Any(r => r.OrganizationId == org && r.SubscriptionId == x.Id))
            .OrderBy(x => x.StartedAtUtc).Take(100).ToListAsync(ct);
        var paymentIds = scheduled.Select(x => x.PaymentId).ToArray();
        var purchases = await db.SubscriptionPurchases.AsNoTracking().Include(x => x.Entitlements).Where(x => x.OrganizationId == org && paymentIds.Contains(x.PaymentId)).ToDictionaryAsync(x => x.PaymentId, ct);
        var items = scheduled.Select(s => SubscriptionEntitlementService.Map(s, purchases[s.PaymentId!.Value], "Scheduled")).ToArray();
        return new(current, items);
    }
    public async Task<Page<PaymentDto>> PaymentsAsync(Guid org, PageQuery page, CancellationToken ct)
    {
        var q = db.Payments.AsNoTracking().Where(x => x.OrganizationId == org);
        var total = await q.CountAsync(ct); var rows = await q.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new(rows.Select(PaymentService.Map).ToArray(), total, page.Page, page.PageSize);
    }
    public async Task<Page<TransactionDto>> TransactionsAsync(Guid org, PageQuery page, CancellationToken ct)
    {
        var q = db.AiCreditTransactions.AsNoTracking().Where(x => x.OrganizationId == org); var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page.Skip).Take(page.PageSize)
            .Select(x => new TransactionDto(x.Id, x.Type, x.Amount, x.BalanceBefore, x.BalanceAfter, x.ReferenceType, x.ReferenceId, x.Description, x.CreatedAtUtc)).ToListAsync(ct);
        return new(rows, total, page.Page, page.PageSize);
    }
    public async Task<Page<UsageDto>> UsageAsync(Guid org, PageQuery page, CancellationToken ct)
    {
        var q = db.InterviewBillings.AsNoTracking().Where(x => x.OrganizationId == org); var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page.Skip).Take(page.PageSize)
            .Select(x => new UsageDto(x.InterviewSessionId, x.RatePerMinuteUsd, x.BillableSeconds, x.BilledAmountUsd, x.Status, x.SettledAtUtc)).ToListAsync(ct);
        return new(rows, total, page.Page, page.PageSize);
    }
    public async Task<PaymentDto> PaymentAsync(Guid org, Guid id, CancellationToken ct) => PaymentService.Map(await db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == org, ct) ?? throw AppException.NotFound());
    public async Task<Page<SubscriptionPurchaseDto>> PurchasesAsync(Guid org, PageQuery page, CancellationToken ct)
    {
        var q = from purchase in db.SubscriptionPurchases.AsNoTracking()
                join pay in db.Payments.AsNoTracking() on purchase.PaymentId equals pay.Id
                where purchase.OrganizationId == org && pay.OrganizationId == org && pay.Status == PaymentStatus.Paid
                select purchase;
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page.Skip).Take(page.PageSize)
            .Select(x => new SubscriptionPurchaseDto(x.Id, x.PaymentId, x.PackageCodeSnapshot, x.PackageNameSnapshot, x.PeriodMonthsSnapshot, x.PriceAmountSnapshot, x.CurrencySnapshot,
                x.DiscountPercentSnapshot, x.AiPricePerMinuteUsdSnapshot, x.RetentionDaysSnapshot, x.CreatedAtUtc)).ToListAsync(ct);
        return new(rows, total, page.Page, page.PageSize);
    }
}
