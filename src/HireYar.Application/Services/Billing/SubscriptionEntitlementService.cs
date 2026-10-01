using HireYar.Application.Common;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Billing;

public sealed class SubscriptionEntitlementService(IApplicationDbContext db, TimeProvider time) : ISubscriptionEntitlementService
{
    public async Task<SubscriptionDto?> CurrentAsync(Guid organizationId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var s = await db.OrganizationSubscriptions.AsNoTracking().Where(x => x.OrganizationId == organizationId &&
            (x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.Scheduled) && x.StartedAtUtc <= now && x.ExpiresAtUtc > now)
            .OrderBy(x => x.StartedAtUtc).FirstOrDefaultAsync(ct);
        return s is null ? null : await MapAsync(s, "Active", ct);
    }
    public async Task<SubscriptionDto> RequireAsync(Guid organizationId, IEnumerable<string> capabilities, CancellationToken ct)
    {
        var s = await CurrentAsync(organizationId, ct) ?? throw new AppException(403, "active_subscription_required", "An active subscription is required.");
        if (capabilities.Any(code => s.Features.All(x => x.Code != code))) throw new AppException(403, "subscription_feature_required", "The subscription does not include the required capability.");
        return s;
    }
    public async Task<SubscriptionDto> MapAsync(OrganizationSubscription s, string status, CancellationToken ct)
    {
        var p = await db.SubscriptionPurchases.AsNoTracking().Include(x => x.Entitlements).SingleAsync(x => x.OrganizationId == s.OrganizationId && x.PaymentId == s.PaymentId, ct);
        return Map(s, p, status);
    }
    public static SubscriptionDto Map(OrganizationSubscription s, SubscriptionPurchase p, string status) => new(s.Id, p.PackageCodeSnapshot, p.PackageNameSnapshot, status, s.StartedAtUtc, s.ExpiresAtUtc,
            p.AiPricePerMinuteUsdSnapshot, p.RetentionDaysSnapshot, p.Entitlements.OrderBy(x => x.Code).Select(x => new EntitlementDto(x.Code, x.Name, x.DataType, x.Value)).ToArray());
}
