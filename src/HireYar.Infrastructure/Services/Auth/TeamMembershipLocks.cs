using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Infrastructure.Auth;

internal static class TeamMembershipLocks
{
    // Serialize signup and invitations for one mobile, including a mobile with no user yet.
    // The hash is used only as a transaction lock key and is never persisted or logged.
    internal static Task LockMobileAsync(AppDbContext db, string mobile, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({mobile}, 0))", ct);
}
