using HireYar.Application.Common;
using HireYar.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Subscriptions;

public sealed class SubscriptionService(IApplicationDbContext db, TimeProvider time, IOrganizationTeamUsage team)
{
    public async Task<OrganizationMembership?> CurrentAsync(Guid org, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var paid = await db.OrganizationMemberships.AsNoTracking().Where(m => m.OrganizationId == org && m.StartedAtUtc <= now && m.ExpiresAtUtc > now)
            .OrderByDescending(m => m.StartedAtUtc).FirstOrDefaultAsync(ct);
        if (paid is not null) return paid;
        var trial = await db.SignupTrials.AsNoTracking().SingleOrDefaultAsync(t => t.OrganizationId == org && t.StartedAtUtc <= now && t.ExpiresAtUtc > now, ct);
        return trial is null ? null : new OrganizationMembership { OrganizationId = org, StartedAtUtc = trial.StartedAtUtc,
            ExpiresAtUtc = trial.ExpiresAtUtc, TeamUsers = trial.TeamUsers, RetentionDays = trial.RetentionDays };
    }
    public async Task<DateTimeOffset?> CoverageEndAsync(Guid org, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var paidEnd = await db.OrganizationMemberships.Where(m => m.OrganizationId == org && m.ExpiresAtUtc > now)
            .Select(m => (DateTimeOffset?)m.ExpiresAtUtc).MaxAsync(ct);
        var trialEnd = await db.SignupTrials.Where(t => t.OrganizationId == org && t.ExpiresAtUtc > now)
            .Select(t => (DateTimeOffset?)t.ExpiresAtUtc).SingleOrDefaultAsync(ct);
        return paidEnd is null ? trialEnd : trialEnd is null || paidEnd >= trialEnd ? paidEnd : trialEnd;
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
        var end = await CoverageEndAsync(org, ct);
        var total = await db.InterviewQuotaGrants.Where(g => g.OrganizationId == org).SumAsync(g => (long)g.Quantity, ct);
        total += await db.SignupTrials.Where(t => t.OrganizationId == org).SumAsync(t => (long)t.InterviewQuantity, ct);
        var consumed = await db.InterviewQuotaReservations.CountAsync(r => r.OrganizationId == org && r.State == "Consumed", ct);
        var held = await (from r in db.InterviewQuotaReservations join s in db.InterviewSessions on r.InterviewSessionId equals s.Id
            where r.OrganizationId == org && s.OrganizationId == org && r.State == "Reserved" &&
                s.Status == HireYar.Domain.Interviews.InterviewStatus.Created && s.ExpiresAtUtc > now && s.AccessRevokedAtUtc == null && s.DeletedAtUtc == null
            select r.Id).CountAsync(ct);
        var remaining = total - consumed;
        return new(m is not null, m?.StartedAtUtc, end, m is null || end is null ? 0 : (int)Math.Ceiling((end.Value - now).TotalDays),
            remaining, remaining - held, held, consumed, m is null || m.UnlimitedTeam ? null : m.TeamUsers, m?.UnlimitedTeam ?? false, await team.OccupiedAsync(org, ct), m is not null && m.OrderId == Guid.Empty);
    }
}
