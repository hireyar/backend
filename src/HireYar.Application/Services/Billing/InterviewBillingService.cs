using HireYar.Application.Common;
using HireYar.Domain.Billing;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Billing;

public sealed class InterviewBillingService(IApplicationDbContext db, ISubscriptionEntitlementService entitlements, WalletService wallet, TimeProvider time)
{
    public async Task<T> StartAsync<T>(InterviewSession session, Func<Task<T>> start, CancellationToken ct)
    {
      bool created = false;
      try
      {
       var result = await db.WithOrganizationBillingLockAsync(session.OrganizationId, async () => {
        await wallet.ExpireLockedAsync(session.OrganizationId, ct);
        // Reload after waiting for the lock. A second start must see the first transaction's session.
        await db.ReloadInterviewAsync(session, ct);
        if (session.Status == InterviewStatus.Created)
        {
            var subscription = await entitlements.RequireAsync(session.OrganizationId, [session.RequiredCapabilityCode, "ai_interview"], ct);
            var maximum = BillingMath.Cost(session.MaxDurationSeconds, subscription.AiRatePerMinuteUsd);
            var w = await wallet.WalletLockedAsync(session.OrganizationId, ct);
            if (w.Balance - await wallet.ReservedAsync(session.OrganizationId, ct) < maximum)
                throw new AppException(409, "insufficient_ai_credit", "AI credit must be replenished before starting the interview.");
            var now = time.GetUtcNow(); var end = now.AddSeconds(session.MaxDurationSeconds); if (end > session.ExpiresAtUtc) end = session.ExpiresAtUtc;
            db.AiCreditReservations.Add(new() { OrganizationId = session.OrganizationId, WalletId = w.Id, InterviewSessionId = session.Id,
                ReservedAmountUsd = maximum, CreatedAtUtc = now, ExpiresAtUtc = end });
            db.InterviewBillings.Add(new() { OrganizationId = session.OrganizationId, InterviewSessionId = session.Id, SubscriptionId = subscription.Id,
                RatePerMinuteUsd = subscription.AiRatePerMinuteUsd, RetentionDays = subscription.RetentionDays, CreatedAtUtc = now });
            created = true;
        }
        else await EnsureProcessingAllowedAsync(session, ct);
        return await start();
    }, ct);
       if (created) BillingMetrics.Reservations.Add(1); return result;
      }
      catch (AppException) { BillingMetrics.ReservationFailures.Add(1); throw; }
    }
    public async Task FinishAsync(InterviewSession session, bool release, CancellationToken ct)
    {
        var amount = await db.WithOrganizationBillingLockAsync(session.OrganizationId, () => wallet.SettleLockedAsync(session, release, ct), ct);
        if (amount > 0) BillingMetrics.Consumed.Add(amount);
    }
    public async Task<T> FinishAsync<T>(InterviewSession session, bool release, Func<Task<T>> finish, CancellationToken ct)
    {
        decimal amount = 0;
        var value = await db.WithOrganizationBillingLockAsync(session.OrganizationId, async () => {
            var result = await finish(); amount = await wallet.SettleLockedAsync(session, release, ct); return result;
        }, ct);
        if (amount > 0) BillingMetrics.Consumed.Add(amount); return value;
    }
    public async Task EnsureProcessingAllowedAsync(InterviewSession s, CancellationToken ct)
    {
        if (!await db.InterviewBillings.AnyAsync(x => x.OrganizationId == s.OrganizationId && x.InterviewSessionId == s.Id && x.Status == InterviewBillingStatus.Reserved, ct))
            throw new AppException(409, "interview_billing_closed", "Interview billing is closed.");
    }
    public async Task<IReadOnlySet<string>> CapabilitiesAsync(InterviewSession s, CancellationToken ct)
    {
        var billing = await db.InterviewBillings.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == s.OrganizationId && x.InterviewSessionId == s.Id, ct) ?? throw AppException.NotFound();
        var subscription = await db.OrganizationSubscriptions.AsNoTracking().SingleAsync(x => x.OrganizationId == s.OrganizationId && x.Id == billing.SubscriptionId, ct);
        var purchase = await db.SubscriptionPurchases.AsNoTracking().Include(x => x.Entitlements).SingleAsync(x => x.OrganizationId == s.OrganizationId && x.PaymentId == subscription.PaymentId, ct);
        // An in-flight interview uses its immutable contract even when the subscription expires.
        return purchase.Entitlements.Select(x => x.Code).ToHashSet(StringComparer.Ordinal);
    }
    public async Task RequireMediaRetentionAsync(InterviewSession s, CancellationToken ct)
    {
        var snapshot = await db.InterviewBillings.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == s.OrganizationId && x.InterviewSessionId == s.Id, ct);
        // Legacy media has no purchased retention contract; do not retroactively shorten it.
        if (snapshot is null) return;
        var hardEnd = (s.StartedAtUtc ?? s.CreatedAtUtc).AddSeconds(s.MaxDurationSeconds);
        if (s.ExpiresAtUtc < hardEnd) hardEnd = s.ExpiresAtUtc;
        var from = s.CompletedAtUtc is { } completed && completed < hardEnd ? completed : hardEnd;
        if (time.GetUtcNow() >= from.AddDays(snapshot.RetentionDays)) throw AppException.NotFound();
    }
}
