using HireYar.Application.Common;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HireYar.Application.Billing;

public sealed class BillingPanelService(IApplicationDbContext db, WalletService wallet, BillingQueryService query,
    ISubscriptionEntitlementService entitlements, IPaymentGateway gateway, TimeProvider time, IOptions<BillingPanelOptions> options, TeamSeatService seats)
{
    public Task<BillingPanelDto> OverviewAsync(Guid org, bool owner, CancellationToken ct) => db.WithOrganizationBillingLockAsync(org, async () =>
    {
        await wallet.ExpireLockedAsync(org, ct); var w = await wallet.WalletLockedAsync(org, ct);
        var reserved = await wallet.ReservedAsync(org, ct); var subscriptions = await query.SubscriptionsAsync(org, ct); var now = time.GetUtcNow();
        SubscriptionDto? expired = null;
        if (subscriptions.Current is null)
        {
            var previous = await db.OrganizationSubscriptions.AsNoTracking().Where(x => x.OrganizationId == org && x.ExpiresAtUtc <= now &&
                !db.SubscriptionReplacements.Any(r => r.OrganizationId == org && r.SubscriptionId == x.Id)).OrderByDescending(x => x.ExpiresAtUtc).FirstOrDefaultAsync(ct);
            if (previous is not null)
            {
                expired = await entitlements.MapAsync(previous, "Expired", ct);
            }
        }
        var current = subscriptions.Current; decimal? minutes = null;
        return new BillingPanelDto(now, current is not null ? "active" : subscriptions.Scheduled.Count > 0 ? "scheduled" : expired is not null ? "expired" : "none", current, expired, subscriptions.Scheduled,
            current is null ? expired is not null ? 0 : null : (int)Math.Ceiling((current.ExpiresAtUtc - now).TotalDays),
            new("IRR", w.Balance, reserved, w.Balance - reserved), current?.AiRatePerMinuteUsd, minutes, owner && gateway.IsEnabled, gateway.IsEnabled);
    }, ct);

    public Task<CheckoutSummaryDto> QuoteAsync(Guid org, SubscriptionQuoteRequest r, CancellationToken ct) =>
        db.WithOrganizationBillingLockAsync(org, async () => (await BuildQuoteLockedAsync(org, r, ct)).Summary, ct);

    internal async Task<(PricingPackage Package, PackagePeriod Period, CheckoutSummaryDto Summary)> BuildQuoteLockedAsync(Guid org, SubscriptionQuoteRequest r, CancellationToken ct)
    {
        var package = await db.PricingCatalog.AsNoTracking().Include(x => x.InterviewTypes).Include(x => x.Periods).Include(x => x.Features).ThenInclude(x => x.Feature)
            .SingleOrDefaultAsync(x => x.IsActive && x.IsPublic && x.Periods.Any(p => p.Id == r.PackagePeriodId && p.IsActive), ct) ?? throw AppException.NotFound();
        var period = package.Periods.Single(x => x.Id == r.PackagePeriodId); var now = time.GetUtcNow();
        var teamCapacity = await seats.PurchaseCapacityLockedAsync(org, package, r.TeamCapacity, ct);
        var seatAmount = TeamSeatMath.BillableUsers(teamCapacity, package.IncludedTeamUsers, package.PaidTeamUserLimit) * package.TeamUserMonthlyPriceIrt * period.Months;
        var contractAmount = period.PriceAmount + seatAmount;
        var current = await entitlements.CurrentAsync(org, ct); Guid? replaces = null; decimal credit = 0;
        var source = current is null ? null : await db.OrganizationSubscriptions.AsNoTracking().SingleAsync(x => x.OrganizationId == org && x.Id == current.Id, ct);
        if (r.Operation == "renewal" && source is not null && source.PackageId != package.Id) throw AppException.Invalid("renewal_requires_same_package");
        if (r.Operation == "upgrade")
        {
            if (source is null) throw AppException.Conflict();
            if (source.IsTrial) throw AppException.Invalid("trial_requires_purchase");
            var purchase = await db.SubscriptionPurchases.AsNoTracking().Include(x => x.Entitlements).SingleAsync(x => x.OrganizationId == org && x.PaymentId == source.PaymentId, ct);
            var target = package.Features.Where(x => x.IsEnabled).ToDictionary(x => x.Feature.Code);
            var preserves = purchase.Entitlements.All(x => target.TryGetValue(x.Code, out var f) && f.Feature.DataType == x.DataType &&
                (x.DataType == FeatureDataType.Boolean || x.Value == f.Value ||
                 (x.DataType == FeatureDataType.Integer && long.TryParse(x.Value, out var oldValue) && long.TryParse(f.Value, out var newValue) && newValue >= oldValue)));
            var increasesLimit = purchase.Entitlements.Any(x => x.DataType == FeatureDataType.Integer && target.TryGetValue(x.Code, out var f) &&
                long.TryParse(x.Value, out var oldValue) && long.TryParse(f.Value, out var newValue) && newValue > oldValue);
            if (source.PackageId == package.Id || !preserves ||
                (target.Count <= purchase.Entitlements.Count && package.InterviewRetentionDays <= purchase.RetentionDaysSnapshot && !increasesLimit))
                throw AppException.Invalid("package_is_not_upgrade");
            if (await db.OrganizationSubscriptions.AnyAsync(x => x.OrganizationId == org && x.Id != source.Id && x.ExpiresAtUtc > now &&
                    (x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.Scheduled) && !db.SubscriptionReplacements.Any(rp => rp.OrganizationId == org && rp.SubscriptionId == x.Id), ct) ||
                await db.Payments.AnyAsync(x => x.OrganizationId == org && x.Type == PaymentType.Subscription && x.Status == PaymentStatus.Pending, ct))
                throw new AppException(409, "subscription_queue_or_checkout_exists", "Resolve the scheduled subscription or pending checkout before upgrading.");
            var payment = await db.Payments.AsNoTracking().SingleAsync(x => x.OrganizationId == org && x.Id == source.PaymentId, ct);
            // Non-cash grants have no paid value to credit. Round whole remaining UTC days down, never over-credit a partial day.
            var totalDays = (decimal)Math.Ceiling((source.ExpiresAtUtc - source.StartedAtUtc).TotalDays);
            var remainingDays = (decimal)Math.Floor((source.ExpiresAtUtc - now).TotalDays);
            var sourceNet = purchase.PriceAmountSnapshot + purchase.TeamSeatAmountIrtSnapshot;
            credit = payment.Provider == "InternalTestGrant" ? 0 : decimal.Floor(sourceNet * Math.Max(0, remainingDays) / totalDays);
            var additions = await (from seat in db.TeamSeatPurchases.AsNoTracking() join pay in db.Payments on seat.PaymentId equals pay.Id
                                   where seat.OrganizationId == org && pay.OrganizationId == org && seat.SubscriptionId == source.Id && pay.Status == PaymentStatus.Paid
                                   select seat).ToListAsync(ct);
            credit += additions.Sum(x => decimal.Floor(x.AmountIrt * Math.Max(0, remainingDays) /
                Math.Max(1m, (decimal)Math.Ceiling((x.ExpiresAtUtc - x.QuotedAtUtc).TotalDays))));
            credit = Math.Min(credit, contractAmount); replaces = source.Id;
        }
        var tail = await db.OrganizationSubscriptions.Where(x => x.OrganizationId == org && x.ExpiresAtUtc > now &&
            (x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.Scheduled) &&
            !db.SubscriptionReplacements.Any(rp => rp.OrganizationId == org && rp.SubscriptionId == x.Id)).MaxAsync(x => (DateTimeOffset?)x.ExpiresAtUtc, ct);
        var start = replaces.HasValue ? now : tail ?? now;
        var subtotal = contractAmount - credit; if (subtotal <= 0) throw AppException.Invalid("upgrade_has_no_payable_amount");
        var taxPercent = options.Value.SubscriptionTaxPercent; var tax = decimal.Round(subtotal * taxPercent / 100m, 0, MidpointRounding.AwayFromZero);
        decimal? original = period.DiscountPercent < 100 ? decimal.Round(period.PriceAmount / (1 - period.DiscountPercent / 100m), 0, MidpointRounding.AwayFromZero) + seatAmount : null;
        var features = package.Features.Where(x => x.IsEnabled).Select(x => new EntitlementDto(x.Feature.Code, x.Feature.Name, x.Feature.DataType, x.Value)).ToArray();
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            org, period.Id, package.RowVersion, r.Operation, credit, taxPercent, period.PriceAmount, replaces, teamCapacity, seatAmount, QueueEnd = replaces.HasValue ? null : tail
        }))));
        return (package, period, new(period.Id, package.Name, period.Months, r.Operation, period.Currency, original,
            original - contractAmount, period.DiscountPercent, contractAmount, credit, taxPercent, tax, subtotal + tax,
            start, start.AddMonths(period.Months), replaces, package.AiPricePerMinuteUsd, package.InterviewRetentionDays, features, fingerprint, teamCapacity, seatAmount));
    }

    public async Task<InvoiceDto> InvoiceAsync(Guid org, Guid id, CancellationToken ct)
    {
        var pay = await db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == id && x.Status == PaymentStatus.Paid && x.Provider != "InternalTestGrant", ct) ?? throw AppException.NotFound();
        CheckoutSummaryDto? subscription = null;
        if (pay.Type == PaymentType.Subscription)
        {
            var p = await db.SubscriptionPurchases.AsNoTracking().Include(x => x.Entitlements).SingleAsync(x => x.OrganizationId == org && x.PaymentId == id, ct);
            var s = await db.OrganizationSubscriptions.AsNoTracking().SingleAsync(x => x.OrganizationId == org && x.PaymentId == id, ct);
            subscription = new(p.PackagePeriodId, p.PackageNameSnapshot, p.PeriodMonthsSnapshot, p.Operation, p.CurrencySnapshot,
                p.OriginalAmountSnapshot, p.DiscountAmountSnapshot, p.DiscountPercentSnapshot, p.PriceAmountSnapshot + p.TeamSeatAmountIrtSnapshot, p.ProratedCreditAmountSnapshot,
                pay.TaxPercentSnapshot ?? 0, pay.TaxAmountSnapshot ?? 0, pay.Amount, s.StartedAtUtc, s.ExpiresAtUtc, p.ReplacesSubscriptionId,
                p.AiPricePerMinuteUsdSnapshot, p.RetentionDaysSnapshot, p.Entitlements.Select(x => new EntitlementDto(x.Code, x.Name, x.DataType, x.Value)).ToArray(), null, p.TeamCapacitySnapshot, p.TeamSeatAmountIrtSnapshot);
        }
        TeamSeatQuoteDto? team = null;
        if (pay.Type == PaymentType.TeamSeats)
        {
            var seat = await db.TeamSeatPurchases.AsNoTracking().SingleAsync(x => x.OrganizationId == org && x.PaymentId == id, ct);
            team = new(seat.SubscriptionId, seat.PreviousCapacity, seat.TargetCapacity, seat.TargetCapacity - seat.PreviousCapacity,
                seat.MonthlyPriceIrt, (seat.ExpiresAtUtc - seat.QuotedAtUtc).Ticks / (30m * TimeSpan.TicksPerDay), seat.AmountIrt,
                "IRT", seat.QuotedAtUtc, seat.ExpiresAtUtc, "");
        }
        return new(id, $"HY-{id:N}", pay.ProviderTransactionId!, pay.PaidAtUtc!.Value, pay.Type.ToString(), subscription?.PackageName ?? (team is not null ? "ظرفیت کاربران تیم" : pay.CreditPackageNameSnapshot!),
            pay.Currency, subscription?.SubtotalAmount ?? pay.SubtotalAmountSnapshot ?? pay.Amount, subscription?.ProratedCreditAmount ?? 0,
            pay.TaxPercentSnapshot ?? 0, pay.TaxAmountSnapshot ?? 0, pay.Amount, pay.CreditAmountRialSnapshot, subscription, team);
    }

    public Task<FinancialHistoryDto> HistoryAsync(Guid org, FinancialHistoryQuery r, CancellationToken ct) => db.WithOrganizationBillingLockAsync(org, async () =>
    {
        if (r.FromUtc > r.ToUtc) throw AppException.Invalid("invalid_date_range");
        var paymentRows = from p in db.Payments.AsNoTracking()
                          where p.OrganizationId == org && p.Status == PaymentStatus.Paid && p.Provider != "InternalTestGrant"
                          join purchase in db.SubscriptionPurchases on p.Id equals purchase.PaymentId into purchases
                          from purchase in purchases.DefaultIfEmpty()
                          select new FinancialRow { Id = p.Id, Date = p.PaidAtUtc!.Value, Category = p.Type == PaymentType.Subscription ? "subscription" : p.Type == PaymentType.TeamSeats ? "teamSeats" : "aiCredit", Kind = "payment",
                              Name = p.Type == PaymentType.Subscription ? purchase.PackageNameSnapshot : p.Type == PaymentType.TeamSeats ? "ظرفیت کاربران تیم" : p.CreditPackageNameSnapshot!,
                              Amount = p.Type != PaymentType.AiCredit ? -p.Amount : p.CreditAmountRialSnapshot!.Value,
                              Currency = p.Type != PaymentType.AiCredit ? "IRT" : "IRR", Cash = p.Amount,
                              Seconds = null, InterviewId = null, Candidate = null, Tracking = p.ProviderTransactionId!, PaymentId = p.Id };
        var ledgerRows = from t in db.AiCreditTransactions.AsNoTracking()
                         where t.OrganizationId == org && t.Type != CreditTransactionType.CreditPurchase
                         join bill in db.InterviewBillings.Where(x => x.OrganizationId == org) on t.ReferenceId equals bill.InterviewSessionId into bills
                         from bill in bills.DefaultIfEmpty()
                         join session in db.InterviewSessions.IgnoreAutoIncludes().Where(x => x.OrganizationId == org) on t.ReferenceId equals session.Id into sessions
                         from session in sessions.DefaultIfEmpty()
                         join candidate in db.CandidateApplications.Where(x => x.OrganizationId == org) on session.CandidateApplicationId equals candidate.Id into candidates
                         from candidate in candidates.DefaultIfEmpty()
                         select new FinancialRow { Id = t.Id, Date = t.CreatedAtUtc, Category = "aiCredit", Kind = t.Type == CreditTransactionType.InterviewUsage ? "usage" : "adjustment",
                             Name = t.Type == CreditTransactionType.Refund ? "بازگشت اعتبار" : t.Type == CreditTransactionType.PromotionalCredit ? "اعتبار هدیه" : "اصلاح اعتبار", Amount = t.Amount, Currency = "IRR", Cash = null,
                             Seconds = t.Type == CreditTransactionType.InterviewUsage && bill != null ? bill.BillableSeconds : null,
                             InterviewId = t.Type == CreditTransactionType.InterviewUsage ? (Guid?)t.ReferenceId : null,
                             Candidate = candidate == null ? null : candidate.FirstName + " " + candidate.LastName, Tracking = "CR-" + t.Id.ToString(), PaymentId = null };
        var rows = paymentRows.Concat(ledgerRows);
        if (r.FromUtc.HasValue) rows = rows.Where(x => x.Date >= r.FromUtc.Value);
        if (r.ToUtc.HasValue) rows = rows.Where(x => x.Date <= r.ToUtc.Value);
        if (r.CreditType is not null) rows = rows.Where(x => x.Category == r.CreditType);
        if (r.TransactionType is not null) rows = rows.Where(x => x.Kind == r.TransactionType);
        var total = await rows.CountAsync(ct);
        var sums = await rows.GroupBy(x => 1).Select(g => new FinancialTotalsDto(
            g.Sum(x => x.Category == "subscription" && x.Kind == "payment" ? x.Cash ?? 0 : 0),
            g.Sum(x => x.Category == "aiCredit" && x.Kind == "payment" ? x.Amount : 0),
            g.Sum(x => x.Kind == "usage" ? -x.Amount : 0), g.Sum(x => x.Kind == "usage" ? (long)(x.Seconds ?? 0) : 0),
            g.Sum(x => x.Category == "aiCredit" && x.Kind == "payment" ? x.Cash ?? 0 : 0),
            g.Sum(x => x.Category == "teamSeats" && x.Kind == "payment" ? x.Cash ?? 0 : 0))).SingleOrDefaultAsync(ct) ?? new(0, 0, 0, 0, 0);
        var page = await rows.OrderByDescending(x => x.Date).ThenBy(x => x.Id).Skip(r.Skip).Take(r.PageSize).ToListAsync(ct);
        return new FinancialHistoryDto(time.GetUtcNow(), new(page.Select(x => new FinancialHistoryItemDto(x.Id, x.Date,
            x.Kind == "usage" ? "مصرف اعتبار مصاحبه" : x.Kind == "payment" ? "خرید " + x.Name : "تغییر اعتبار: " + x.Name,
            x.Category, x.Kind, x.Amount, x.Currency, x.Cash, x.Seconds, x.InterviewId, x.Candidate, x.Tracking,
            x.PaymentId.HasValue ? $"/api/billing/payments/{x.PaymentId}/invoice" : null)).ToArray(), total, r.Page, r.PageSize), sums);
    }, ct);

    private sealed class FinancialRow
    {
        public Guid Id { get; init; }
        public DateTimeOffset Date { get; init; }
        public string Category { get; init; } = "";
        public string Kind { get; init; } = "";
        public string Name { get; init; } = "";
        public decimal Amount { get; init; }
        public string Currency { get; init; } = "";
        public decimal? Cash { get; init; }
        public int? Seconds { get; init; }
        public Guid? InterviewId { get; init; }
        public string? Candidate { get; init; }
        public string Tracking { get; init; } = "";
        public Guid? PaymentId { get; init; }
    }
}
