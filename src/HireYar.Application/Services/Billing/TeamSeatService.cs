using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Billing;

public sealed class TeamSeatService(IApplicationDbContext db, ISubscriptionEntitlementService subscriptions,
    ITeamSeatUsage usage, TimeProvider time)
{
    public Task<TeamSeatStateDto> StateAsync(Guid org, CancellationToken ct) =>
        db.WithOrganizationBillingLockAsync(org, () => StateLockedAsync(org, ct), ct);

    public async Task<TeamSeatStateDto> StateLockedAsync(Guid org, CancellationToken ct)
    {
        var sub = await subscriptions.CurrentAsync(org, ct);
        var occupied = await usage.OccupiedAsync(org, ct);
        if (sub is null) return new(null, 3, 30, 200_000m, 3, false, occupied, null);
        var capacity = sub.TeamCapacity;
        var granted = await (from seat in db.TeamSeatPurchases.AsNoTracking()
                             join pay in db.Payments on seat.PaymentId equals pay.Id
                             where seat.OrganizationId == org && pay.OrganizationId == org &&
                                seat.SubscriptionId == sub.Id && pay.Status == PaymentStatus.Paid
                             select (int?)seat.TargetCapacity).MaxAsync(ct);
        capacity = Math.Max(capacity, granted ?? 0);
        return new(sub.Id, sub.IncludedTeamUsers, sub.PaidTeamUserLimit, sub.TeamUserMonthlyPriceIrt,
            capacity, capacity >= sub.PaidTeamUserLimit || sub.TeamUserMonthlyPriceIrt == 0,
            occupied, sub.ExpiresAtUtc);
    }

    public async Task RequireInvitationCapacityLockedAsync(Guid org, bool accepting, CancellationToken ct)
    {
        var state = await StateLockedAsync(org, ct);
        // A live pending invitation already reserves its seat.
        if (!state.IsUnlimited && state.OccupiedUsers + (accepting ? 0 : 1) > state.Capacity)
            throw new AppException(409, "team_capacity_payment_required", "Purchase additional team capacity before inviting or accepting a member.");
    }

    internal async Task<int> PurchaseCapacityLockedAsync(Guid org, PricingPackage package, int? requested, CancellationToken ct)
    {
        var state = await StateLockedAsync(org, ct);
        var minimum = Math.Max(package.IncludedTeamUsers, Math.Min(state.OccupiedUsers, package.PaidTeamUserLimit));
        var capacity = Math.Min(requested ?? Math.Max(minimum, state.Capacity), package.PaidTeamUserLimit);
        if (requested is < 1 or > 10000 || capacity < minimum) throw AppException.Invalid("team_capacity_too_small");
        return capacity;
    }

    public Task<TeamSeatQuoteDto> QuoteAsync(Guid org, TeamSeatQuoteRequest r, CancellationToken ct) =>
        db.WithOrganizationBillingLockAsync(org, () => BuildQuoteLockedAsync(org, r, ct), ct);

    internal async Task<TeamSeatQuoteDto> BuildQuoteLockedAsync(Guid org, TeamSeatQuoteRequest r, CancellationToken ct)
    {
        var state = await StateLockedAsync(org, ct);
        if (state.SubscriptionId is not { } sub || state.ExpiresAtUtc is not { } expiry)
            throw new AppException(403, "active_subscription_required", "An active subscription is required.");
        var now = time.GetUtcNow();
        var at = r.QuotedAtUtc?.ToUniversalTime() ?? new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
        var started = await db.OrganizationSubscriptions.Where(x => x.OrganizationId == org && x.Id == sub).Select(x => x.StartedAtUtc).SingleAsync(ct);
        if (at.Ticks % 10 != 0 || at > now || at < now.AddMinutes(-5) || at < started || at >= expiry) throw AppException.Invalid("team_quote_expired");
        var target = Math.Min(r.TargetCapacity, state.PaidTeamUserLimit);
        if (r.TargetCapacity is < 1 or > 10000 || state.IsUnlimited || target <= state.Capacity)
            throw AppException.Invalid("team_capacity_not_increased");
        var additional = TeamSeatMath.BillableUsers(target, state.IncludedTeamUsers, state.PaidTeamUserLimit)
            - TeamSeatMath.BillableUsers(state.Capacity, state.IncludedTeamUsers, state.PaidTeamUserLimit);
        var amount = TeamSeatMath.ProratedAmount(additional, state.TeamUserMonthlyPriceIrt, at, expiry);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            org, sub, state.Capacity, target, additional, state.TeamUserMonthlyPriceIrt, at, expiry, amount
        }))));
        return new(sub, state.Capacity, target, additional, state.TeamUserMonthlyPriceIrt,
            (expiry - at).Ticks / (30m * TimeSpan.TicksPerDay), amount, "IRT", at, expiry, fingerprint);
    }
}
