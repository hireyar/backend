using HireYar.Application.Common;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Infrastructure.Persistence;

public sealed partial class AppDbContext
{
    public async Task<T> WithOrganizationLockAsync<T>(Guid organizationId, Func<Task<T>> action, CancellationToken ct)
    {
        if (Database.CurrentTransaction is not null) throw new InvalidOperationException("Nested organization transaction.");
        await using var tx = await Database.BeginTransactionAsync(ct);
        // Serialize related organization mutations without any pricing or subscription dependency.
        var rows = await Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {organizationId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        if (rows.Count == 0) throw AppException.NotFound();
        try
        {
            var result = await action(); await SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
        }
        catch { ChangeTracker.Clear(); throw; }
    }
}
