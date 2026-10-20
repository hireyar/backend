using Microsoft.EntityFrameworkCore;
namespace HireYar.Infrastructure.Persistence;
public sealed partial class AppDbContext
{
    // A short database transaction lock serializes this small platform sales module.
    // Never take mobile/organization locks or perform network I/O inside this callback.
    public async Task<T> WithSalesLockAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var tx = Database.CurrentTransaction is null ? await Database.BeginTransactionAsync(ct) : null;
        await Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7219327641001)", ct);
        var result = await action();
        await SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return result;
    }
}
