using HireYar.Application.Billing;
using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Infrastructure.Organizations;

public sealed class TeamSeatUsage(AppDbContext db, TimeProvider time) : ITeamSeatUsage
{
    public async Task<int> OccupiedAsync(Guid organizationId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return await db.Users.CountAsync(x => x.OrganizationId == organizationId && x.IsActive, ct)
            + await db.MobileTeamInvitations.CountAsync(x => x.OrganizationId == organizationId &&
                x.AcceptedAtUtc == null && x.CancelledAtUtc == null && x.ExpiresAtUtc > now, ct);
    }
}
