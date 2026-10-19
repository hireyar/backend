using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Subscriptions;

public sealed class SubscriptionOrderService(IApplicationDbContext db, CatalogService catalog, SubscriptionService subscriptions,
    IOrganizationTeamUsage team, TimeProvider time)
{
    private SubscriptionOrderDto Map(SubscriptionOrder o) => new(o.Id, o.OrganizationId, o.Product, o.ProductName, o.Status == "Pending" && o.ExpiresAtUtc <= time.GetUtcNow() ? "Expired" : o.Status, "IRR",
        o.AmountRial, o.OriginalAmountRial, o.DiscountPercent, o.Months, o.TeamUsers, o.UnlimitedTeam, o.BundleCode,
        o.Quantity, o.CreatedAtUtc, o.ExpiresAtUtc, o.ConfirmedAtUtc);
    public async Task<SubscriptionOrderDto> GetAsync(Guid org, Guid id, CancellationToken ct) => Map(await db.SubscriptionOrders.AsNoTracking()
        .SingleOrDefaultAsync(o => o.OrganizationId == org && o.Id == id, ct) ?? throw AppException.NotFound());
    public async Task<Page<SubscriptionOrderDto>> ListAsync(Guid? org, PageQuery page, CancellationToken ct)
    {
        var q = db.SubscriptionOrders.AsNoTracking().Where(o => org == null || o.OrganizationId == org);
        var count = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(o => o.CreatedAtUtc).ThenBy(o => o.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToArray(), count, page.Page, page.PageSize);
    }
    public Task<SubscriptionOrderDto> CreateAsync(Guid org, CreateSubscriptionOrderRequest r, CancellationToken ct) =>
        db.WithOrganizationLockAsync(org, async () =>
        {
            if (r.RequestId == Guid.Empty) throw AppException.Invalid("request_id_required");
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r.Selection))));
            var old = await db.SubscriptionOrders.AsNoTracking().SingleOrDefaultAsync(o => o.OrganizationId == org && o.RequestId == r.RequestId, ct);
            if (old is not null)
            {
                if (old.RequestFingerprint != fingerprint) throw new AppException(409, "idempotency_conflict", "Reuse the request id only for the same selection.");
                return Map(old);
            }
            var q = await catalog.QuoteAsync(r.Selection, ct);
            if (!q.PlanVersion.SequenceEqual(r.PlanVersion) || (q.BundleVersion is not null &&
                (r.BundleVersion is null || !q.BundleVersion.SequenceEqual(r.BundleVersion))))
                throw new AppException(409, "pricing_changed", "Refresh the quote before purchasing.");
            if (q.Product == "interviews") await subscriptions.RequireAsync(org, ct);
            else if (!q.UnlimitedTeam && await team.OccupiedAsync(org, ct) > q.TeamUsers) throw AppException.Invalid("team_capacity_too_small");
            var now = time.GetUtcNow();
            var o = new SubscriptionOrder { OrganizationId = org, RequestId = r.RequestId, RequestFingerprint = fingerprint,
                Product = q.Product, ProductName = q.ProductName, BundleCode = q.BundleCode, Months = q.Months,
                TeamUsers = q.TeamUsers, UnlimitedTeam = q.UnlimitedTeam, Quantity = q.Quantity, UnitPriceRial = q.UnitPriceRial,
                TeamMonthlyPriceRial = q.TeamMonthlyPriceRial, OriginalAmountRial = q.OriginalAmountRial, AmountRial = q.AmountRial,
                DiscountPercent = q.DiscountPercent, RetentionDays = q.RetentionDays, CreatedAtUtc = now, ExpiresAtUtc = now.AddHours(24) };
            db.SubscriptionOrders.Add(o); return Map(o);
        }, ct);
    public async Task<SubscriptionOrderDto> ConfirmAsync(Guid id, Guid actor, string paymentReference, CancellationToken ct)
    {
        var org = await db.SubscriptionOrders.Where(o => o.Id == id).Select(o => (Guid?)o.OrganizationId).SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();
        return await db.WithOrganizationLockAsync(org, async () =>
        {
            var o = await db.SubscriptionOrders.SingleAsync(o => o.OrganizationId == org && o.Id == id, ct);
            var reference = paymentReference.Trim();
            if (actor == Guid.Empty || reference.Length is < 1 or > 120) throw AppException.Invalid();
            if (o.Status == "Confirmed")
            {
                if (o.PaymentReference != reference) throw new AppException(409, "order_already_confirmed", "The order was already confirmed.");
                return Map(o);
            }
            var now = time.GetUtcNow();
            if (o.Status != "Pending" || o.ExpiresAtUtc <= now) throw new AppException(409, "order_not_payable", "The order is cancelled or expired.");
            if (await db.SubscriptionOrders.AnyAsync(x => x.PaymentReference == reference && x.Id != id, ct))
                throw new AppException(409, "payment_reference_used", "This payment reference already belongs to another order.");
            if (o.Product == "subscription")
            {
                if (!o.UnlimitedTeam && await team.OccupiedAsync(org, ct) > o.TeamUsers) throw AppException.Invalid("team_capacity_too_small");
                var last = await subscriptions.CoverageEndAsync(org, ct);
                var start = last ?? now;
                db.OrganizationMemberships.Add(new() { OrganizationId = org, OrderId = o.Id, StartedAtUtc = start,
                    ExpiresAtUtc = start.AddMonths(o.Months), TeamUsers = o.TeamUsers, UnlimitedTeam = o.UnlimitedTeam, RetentionDays = o.RetentionDays });
            }
            else
            {
                await subscriptions.RequireAsync(org, ct);
                db.InterviewQuotaGrants.Add(new() { OrganizationId = org, OrderId = o.Id, Quantity = o.Quantity, CreatedAtUtc = now });
            }
            o.Status = "Confirmed"; o.ConfirmedAtUtc = now; o.ConfirmedByUserId = actor; o.PaymentReference = reference;
            return Map(o);
        }, ct);
    }
    public Task CancelAsync(Guid org, Guid id, CancellationToken ct) => db.WithOrganizationLockAsync(org, async () =>
    {
        var o = await db.SubscriptionOrders.SingleOrDefaultAsync(o => o.OrganizationId == org && o.Id == id, ct) ?? throw AppException.NotFound();
        if (o.Status == "Confirmed") throw new AppException(409, "order_already_confirmed", "Confirmed purchases cannot be cancelled.");
        o.Status = "Cancelled"; return true;
    }, ct);
}
