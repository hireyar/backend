using HireYar.Application.Common;
using HireYar.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Subscriptions;

public sealed class SubscriptionService(IApplicationDbContext db, TimeProvider time, IOrganizationTeamUsage team)
{
    public Task<OrganizationMembership?> CurrentAsync(Guid org, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return db.OrganizationMemberships.AsNoTracking().Where(m => m.OrganizationId == org && m.StartedAtUtc <= now && m.ExpiresAtUtc > now)
            .OrderByDescending(m => m.StartedAtUtc).FirstOrDefaultAsync(ct);
    }
    public async Task<OrganizationMembership> RequireAsync(Guid org, CancellationToken ct) => await CurrentAsync(org, ct)
        ?? throw new AppException(403, "active_subscription_required", "An active subscription is required.");
    public async Task RequireTeamCapacityLockedAsync(Guid org, bool accepting, CancellationToken ct)
    {
        var m = await RequireAsync(org, ct);
        if (!m.UnlimitedTeam && await team.OccupiedAsync(org, ct) + (accepting ? 0 : 1) > m.TeamUsers)
            throw new AppException(409, "team_capacity_exceeded", "The selected subscription does not include enough team users.");
    }
    public async Task<SubscriptionStateDto> StateAsync(Guid org, CancellationToken ct)
    {
        var m = await CurrentAsync(org, ct); var now = time.GetUtcNow();
        var end = await db.OrganizationMemberships.Where(m => m.OrganizationId == org && m.ExpiresAtUtc > now)
            .Select(m => (DateTimeOffset?)m.ExpiresAtUtc).MaxAsync(ct);
        var total = await db.InterviewQuotaGrants.Where(g => g.OrganizationId == org).SumAsync(g => (long)g.Quantity, ct);
        var consumed = await db.InterviewQuotaReservations.CountAsync(r => r.OrganizationId == org && r.State == "Consumed", ct);
        var held = await (from r in db.InterviewQuotaReservations join s in db.InterviewSessions on r.InterviewSessionId equals s.Id
            where r.OrganizationId == org && s.OrganizationId == org && r.State == "Reserved" &&
                s.Status == HireYar.Domain.Interviews.InterviewStatus.Created && s.ExpiresAtUtc > now && s.AccessRevokedAtUtc == null && s.DeletedAtUtc == null
            select r.Id).CountAsync(ct);
        var remaining = total - consumed;
        return new(m is not null, m?.StartedAtUtc, end, m is null || end is null ? 0 : (int)Math.Ceiling((end.Value - now).TotalDays),
            remaining, remaining - held, held, consumed, m is null || m.UnlimitedTeam ? null : m.TeamUsers, m?.UnlimitedTeam ?? false, await team.OccupiedAsync(org, ct));
    }
}
