using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Infrastructure.Organizations;

// Two-int advisory key space is separate from mobile-hash locks. Always acquire before mobile/org/member locks.
// Catalog edits are exclusive; team mutations and invitation acceptance share a stable catalog until commit.
internal static class AccessCatalogLocks
{
    internal static Task SharedAsync(AppDbContext db, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock_shared(1213813313, 1)", ct);
    internal static Task ExclusiveAsync(AppDbContext db, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1213813313, 1)", ct);
}
