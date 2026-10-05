using HireYar.Application.Common;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Infrastructure.Persistence;

public sealed partial class AppDbContext
{
    public async Task<T> WithOrganizationBillingLockAsync<T>(Guid organizationId, Func<Task<T>> action, CancellationToken ct)
    {
        if (Database.CurrentTransaction is not null) throw new InvalidOperationException("Nested billing transaction.");
        await using var tx = await Database.BeginTransactionAsync(ct);
        // The organization exists before its lazy wallet. This serializes wallet creation as well as all subsequent writes.
        var rows = await Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {organizationId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        if (rows.Count == 0) throw AppException.NotFound();
        try
        {
            var result = await action(); await SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
        }
        catch { ChangeTracker.Clear(); throw; }
    }
    private void ValidateBillingHistory()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted)) continue;
            if (entry.Entity is AiCreditTransaction or SubscriptionPurchase or SubscriptionEntitlement or OrganizationSubscription or SubscriptionReplacement)
                throw new InvalidOperationException("Financial history is immutable.");
            if (entry.Entity is Payment)
            {
                if (entry.State == EntityState.Deleted || (PaymentStatus)entry.Property(nameof(Payment.Status)).OriginalValue! == PaymentStatus.Paid)
                    throw new InvalidOperationException("Paid payments are immutable.");
                var allowed = new[] { nameof(Payment.Status), nameof(Payment.ProviderAuthority), nameof(Payment.ProviderTransactionId), nameof(Payment.PaidAtUtc), nameof(Payment.FailedAtUtc) };
                if (entry.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name))) throw new InvalidOperationException("Payment quotes are immutable.");
            }
            if (entry.Entity is InterviewBilling && (entry.State == EntityState.Deleted || (InterviewBillingStatus)entry.Property(nameof(InterviewBilling.Status)).OriginalValue! != InterviewBillingStatus.Reserved))
                throw new InvalidOperationException("Settled interview billing is immutable.");
            if (entry.Entity is AiCreditReservation && (entry.State == EntityState.Deleted || (ReservationStatus)entry.Property(nameof(AiCreditReservation.Status)).OriginalValue! != ReservationStatus.Active))
                throw new InvalidOperationException("Final reservations are immutable.");
        }
    }
}
