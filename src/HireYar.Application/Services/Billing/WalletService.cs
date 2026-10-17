using HireYar.Application.Common;
using HireYar.Domain.Billing;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Billing;

public sealed class WalletService(IApplicationDbContext db, TimeProvider time)
{
    public Task<WalletDto> GetAsync(Guid org, CancellationToken ct) => db.WithOrganizationBillingLockAsync(org, async () => {
        await ExpireLockedAsync(org, ct); var w = await WalletLockedAsync(org, ct);
        var reserved = await ReservedAsync(org, ct); return new WalletDto("IRR", w.Balance, reserved, w.Balance - reserved);
    }, ct);
    internal async Task<OrganizationAiWallet> WalletLockedAsync(Guid org, CancellationToken ct)
    {
        var w = db.OrganizationAiWallets.Local.SingleOrDefault(x => x.OrganizationId == org)
            ?? await db.OrganizationAiWallets.SingleOrDefaultAsync(x => x.OrganizationId == org, ct);
        if (w is null) { w = new() { OrganizationId = org, CreatedAtUtc = time.GetUtcNow(), UpdatedAtUtc = time.GetUtcNow() }; db.OrganizationAiWallets.Add(w); }
        return w;
    }
    internal Task<decimal> ReservedAsync(Guid org, CancellationToken ct) => db.AiCreditReservations.Where(x => x.OrganizationId == org && x.Status == ReservationStatus.Active).SumAsync(x => x.ReservedAmountRial, ct);
    internal async Task AddEntryLockedAsync(Guid org, decimal amount, CreditTransactionType type, string referenceType, Guid referenceId, string? description, CancellationToken ct, decimal? amountUsd = null)
    {
        var existing = await db.AiCreditTransactions.SingleOrDefaultAsync(x => x.OrganizationId == org && x.ReferenceType == referenceType && x.ReferenceId == referenceId, ct);
        if (existing is not null)
        {
            if (existing.Amount != amount || existing.AmountUsd != amountUsd || existing.Type != type || existing.Description != description) throw AppException.Conflict();
            return;
        }
        if (amount is < -999999999999999999m or > 999999999999999999m || !BillingMath.HasPrecision(amount, 6) || referenceId == Guid.Empty) throw AppException.Invalid();
        var w = await WalletLockedAsync(org, ct); var after = w.Balance + amount;
        if (after < await ReservedAsync(org, ct) || after > 999999999999999999m) throw new AppException(409, "insufficient_ai_credit", "Insufficient available AI credit.");
        db.AiCreditTransactions.Add(new() { OrganizationId = org, WalletId = w.Id, Type = type, Amount = amount,
            AmountUsd = amountUsd, BalanceBefore = w.Balance, BalanceAfter = after, ReferenceType = referenceType, ReferenceId = referenceId, Description = description, CreatedAtUtc = time.GetUtcNow() });
        w.Balance = after; w.UpdatedAtUtc = time.GetUtcNow();
    }
    public Task<WalletDto> AdjustAsync(Guid org, AdjustmentRequest r, CancellationToken ct) => db.WithOrganizationBillingLockAsync(org, async () => {
        if (r.Amount == 0 || r.IdempotencyKey == Guid.Empty || string.IsNullOrWhiteSpace(r.Reason)) throw AppException.Invalid();
        await ExpireLockedAsync(org, ct);
        await AddEntryLockedAsync(org, r.Amount, CreditTransactionType.AdminAdjustment, "AdminAdjustment", r.IdempotencyKey, r.Reason.Trim(), ct);
        var w = await WalletLockedAsync(org, ct); var reserved = await ReservedAsync(org, ct); return new WalletDto("IRR", w.Balance, reserved, w.Balance - reserved);
    }, ct);
    // Expiry settles persisted eligible answers; it never charges browser idle time. No background infrastructure required.
    internal async Task ExpireLockedAsync(Guid org, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var reservations = await (from r in db.AiCreditReservations
            join s in db.InterviewSessions on r.InterviewSessionId equals s.Id
            where r.OrganizationId == org && s.OrganizationId == org && r.Status == ReservationStatus.Active &&
                (r.ExpiresAtUtc <= now || s.StartedAtUtc != null && s.StartedAtUtc.Value.AddSeconds(s.MaxDurationSeconds) <= now)
            select r).ToListAsync(ct);
        foreach (var r in reservations)
        {
            var s = await db.InterviewSessions.Include(x => x.Answers).SingleAsync(x => x.OrganizationId == org && x.Id == r.InterviewSessionId, ct);
            await SettleLockedAsync(s, false, ct);
            // Persist before the next reservation so SQL sums match the financial state.
            await db.SaveChangesAsync(ct);
        }
    }
    internal async Task<decimal> SettleLockedAsync(InterviewSession s, bool release, CancellationToken ct)
    {
        var r = await db.AiCreditReservations.SingleOrDefaultAsync(x => x.OrganizationId == s.OrganizationId && x.InterviewSessionId == s.Id, ct);
        if (r is null || r.Status != ReservationStatus.Active) return 0;
        var billing = await db.InterviewBillings.SingleAsync(x => x.OrganizationId == s.OrganizationId && x.InterviewSessionId == s.Id, ct);
        var seconds = release ? 0 : Math.Min(s.MaxDurationSeconds, s.Answers.Sum(x => x.BillableSeconds));
        var cost = BillingMath.Cost(seconds, billing.RatePerMinuteRial);
        if (cost > r.ReservedAmountRial) throw new InvalidOperationException("Charge exceeds reserved maximum.");
        var now = time.GetUtcNow();
        var expired = now >= r.ExpiresAtUtc || s.StartedAtUtc is { } started && now >= started.AddSeconds(s.MaxDurationSeconds);
        r.ConsumedAmountRial = cost; r.Status = release ? ReservationStatus.Released : expired ? ReservationStatus.Expired : ReservationStatus.Consumed;
        billing.BillableSeconds = seconds; billing.BilledAmountRial = cost; billing.BilledAmountUsd = BillingMath.Cost(seconds, billing.RatePerMinuteUsd); billing.Status = release ? InterviewBillingStatus.Released : InterviewBillingStatus.Settled; billing.SettledAtUtc = time.GetUtcNow();
        // Make the released reservation visible to the ledger's available-credit check in this transaction.
        await db.SaveChangesAsync(ct);
        await AddEntryLockedAsync(s.OrganizationId, -cost, CreditTransactionType.InterviewUsage, "Interview", s.Id, null, ct, -billing.BilledAmountUsd);
        return cost;
    }
}
