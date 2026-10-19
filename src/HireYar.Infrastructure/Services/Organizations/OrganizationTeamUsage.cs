using HireYar.Application.Subscriptions;
using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Infrastructure.Organizations;

public sealed class OrganizationTeamUsage(AppDbContext db, TimeProvider time) : IOrganizationTeamUsage
{
    public async Task<int> OccupiedAsync(Guid organizationId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return await db.Users.CountAsync(u => u.OrganizationId == organizationId && u.IsActive, ct) +
            await db.MobileTeamInvitations.CountAsync(i => i.OrganizationId == organizationId && i.AcceptedAtUtc == null &&
                i.CancelledAtUtc == null && i.ExpiresAtUtc > now, ct);
    }
}
