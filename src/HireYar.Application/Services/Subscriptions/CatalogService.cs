using HireYar.Application.Common;
using HireYar.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Subscriptions;

public sealed class CatalogService(IApplicationDbContext db)
{
    internal Task<SubscriptionPlan> PlanAsync(CancellationToken ct) => db.SubscriptionPlans.AsNoTracking()
        .SingleAsync(p => p.Id == SubscriptionDefaults.PlanId, ct);
    public async Task<PlanDto> ReadAsync(bool admin, CancellationToken ct)
    {
        var p = await PlanAsync(ct);
        if (!admin && !p.IsActive) throw new AppException(503, "catalog_unavailable", "Subscriptions are temporarily unavailable.");
        var bundles = await db.InterviewBundles.AsNoTracking().Where(b => b.PlanId == p.Id && (admin || b.IsActive))
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Id).ToListAsync(ct);
        return new(p.Id, p.Name, "IRR", p.MonthlyPriceRial, p.BaseInterviewPriceRial, p.ExtraUserMonthlyPriceRial,
            p.IncludedUsers, p.PaidUserCeiling, p.RetentionDays, 30, p.IsActive, p.RowVersion,
            bundles.Select(b => new BundleDto(b.Id, b.Code, b.Name, b.MinimumQuantity, b.MaximumQuantity, b.QuantityStep,
                b.DiscountPercent, SubscriptionDefaults.Price(p.BaseInterviewPriceRial, b.MinimumQuantity),
                SubscriptionDefaults.Price(p.BaseInterviewPriceRial, b.MinimumQuantity, b.DiscountPercent), b.IsActive, b.SortOrder, b.RowVersion)).ToArray(),
            new SubscriptionPeriodDto[] { new(1, 0), new(3, p.ThreeMonthDiscountPercent), new(6, p.SixMonthDiscountPercent), new(12, p.TwelveMonthDiscountPercent) });
    }
    public async Task<PlanDto> UpdateAsync(PlanUpdateRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.IncludedUsers >= r.PaidUserCeiling) throw AppException.Invalid();
        foreach (var discount in new[] { r.ThreeMonthDiscountPercent, r.SixMonthDiscountPercent, r.TwelveMonthDiscountPercent })
            if (discount is { } value && (value < 0 || value > 99.99m || decimal.Round(value, 2) != value)) throw AppException.Invalid();
        var p = await db.SubscriptionPlans.SingleAsync(ct); db.SetOriginalVersion(p, r.RowVersion);
        p.ThreeMonthDiscountPercent = r.ThreeMonthDiscountPercent ?? p.ThreeMonthDiscountPercent;
        p.SixMonthDiscountPercent = r.SixMonthDiscountPercent ?? p.SixMonthDiscountPercent;
        p.TwelveMonthDiscountPercent = r.TwelveMonthDiscountPercent ?? p.TwelveMonthDiscountPercent;
        p.Name = r.Name.Trim(); p.MonthlyPriceRial = r.MonthlyPriceRial; p.BaseInterviewPriceRial = r.BaseInterviewPriceRial;
        p.ExtraUserMonthlyPriceRial = r.ExtraUserMonthlyPriceRial; p.IncludedUsers = r.IncludedUsers;
        p.PaidUserCeiling = r.PaidUserCeiling; p.RetentionDays = r.RetentionDays; p.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct); return await ReadAsync(true, ct);
    }
    public async Task<PlanDto> UpdateBundleAsync(string code, BundleUpdateRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.MaximumQuantity < r.MinimumQuantity || r.QuantityStep < 1 ||
            (r.MaximumQuantity - r.MinimumQuantity) % r.QuantityStep != 0 || decimal.Round(r.DiscountPercent, 2) != r.DiscountPercent)
            throw AppException.Invalid();
        var b = await db.InterviewBundles.SingleOrDefaultAsync(b => b.Code == code, ct) ?? throw AppException.NotFound();
        db.SetOriginalVersion(b, r.RowVersion); b.Name = r.Name.Trim(); b.MinimumQuantity = r.MinimumQuantity;
        b.MaximumQuantity = r.MaximumQuantity; b.QuantityStep = r.QuantityStep; b.DiscountPercent = r.DiscountPercent;
        b.SortOrder = r.SortOrder; b.IsActive = r.IsActive; await db.SaveChangesAsync(ct); return await ReadAsync(true, ct);
    }
    public async Task<PurchaseQuote> QuoteAsync(PurchaseSelection r, CancellationToken ct)
    {
        var p = await PlanAsync(ct);
        if (!p.IsActive) throw new AppException(503, "catalog_unavailable", "Subscriptions are temporarily unavailable.");
        if (r.Product == "subscription")
        {
            if (r.Months is not (1 or 3 or 6 or 12) || r.TeamUsers < p.IncludedUsers || r.TeamUsers > 10000 || r.BundleCode is not null || r.Quantity is not null)
                throw AppException.Invalid();
            var team = SubscriptionDefaults.Price(p.ExtraUserMonthlyPriceRial, Math.Min(r.TeamUsers, p.PaidUserCeiling) - p.IncludedUsers);
            var discount = r.Months switch { 3 => p.ThreeMonthDiscountPercent, 6 => p.SixMonthDiscountPercent, 12 => p.TwelveMonthDiscountPercent, _ => 0m };
            var originalAmount = checked((p.MonthlyPriceRial + team) * r.Months);
            var amount = SubscriptionDefaults.Price(p.MonthlyPriceRial + team, r.Months, discount);
            return new(r.Product, p.Name, "IRR", r.Months, r.TeamUsers, r.TeamUsers >= p.PaidUserCeiling,
                p.RetentionDays, null, 0, p.MonthlyPriceRial, team, discount, originalAmount, originalAmount - amount, amount, p.RowVersion, null);
        }
        if (r.Product != "interviews" || r.BundleCode is null || r.Months != 1 || r.TeamUsers != 5) throw AppException.Invalid();
        var b = await db.InterviewBundles.AsNoTracking().SingleOrDefaultAsync(b => b.Code == r.BundleCode && b.IsActive, ct) ?? throw AppException.NotFound();
        var quantity = r.Quantity ?? b.MinimumQuantity;
        if (quantity < b.MinimumQuantity || quantity > b.MaximumQuantity || (quantity - b.MinimumQuantity) % b.QuantityStep != 0) throw AppException.Invalid("invalid_bundle_quantity");
        var original = SubscriptionDefaults.Price(p.BaseInterviewPriceRial, quantity);
        var total = SubscriptionDefaults.Price(p.BaseInterviewPriceRial, quantity, b.DiscountPercent);
        return new(r.Product, b.Name, "IRR", 0, 0, false, p.RetentionDays, b.Code, quantity, p.BaseInterviewPriceRial, 0,
            b.DiscountPercent, original, original - total, total, p.RowVersion, b.RowVersion);
    }
}
