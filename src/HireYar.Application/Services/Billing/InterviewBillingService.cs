using HireYar.Application.Common;
using HireYar.Domain.Billing;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Billing;

public sealed class InterviewBillingService(IApplicationDbContext db, ISubscriptionEntitlementService entitlements, WalletService wallet, TimeProvider time, IAiDisplayRateProvider rates)
{
    public Task<AiDisplayRateDto> CurrentRateAsync(CancellationToken ct) => rates.GetAsync(ct);

    // The caller holds the organization lock and persists invitation + reservation atomically.
    public async Task ReserveInvitationLockedAsync(InterviewSession session, AiDisplayRateDto quote, CancellationToken ct)
    {
        await wallet.ExpireLockedAsync(session.OrganizationId, ct);
        if (await db.AiCreditReservations.AnyAsync(x => x.OrganizationId == session.OrganizationId && x.InterviewSessionId == session.Id, ct))
            throw AppException.Conflict();
        var subscription = await entitlements.RequireAsync(session.OrganizationId,
            session.AskOneQuestionPerTopic || session.ProgressiveTopicQuestions
                ? ["ai_interview", "advanced_ai_workflow", "adaptive_questions", "adaptive_difficulty"]
                : ["ai_interview"], ct);
        var now = time.GetUtcNow();
        // ExpiresAtUtc is the next refresh deadline; a last-known provider quote remains
        // usable during an outage, with its original fetched timestamp kept for audit.
        if (quote.Currency != "IRR" || quote.RialPerUsdt <= 0)
            throw new AppException(503, "ai_display_rate_unavailable", "Refresh the current exchange rate.");
        var usdRate = await PricingService.ActivePackageRateAsync(db, session.OrganizationId, subscription.Id, session.PricingInterviewTypeCode ?? (session.RequiredCapabilityCode == "technical_interview" ? "specialized" : "general"), ct);
        var rialRate = decimal.Round(usdRate * quote.RialPerUsdt, 6, MidpointRounding.ToEven);
        if (rialRate <= 0) throw AppException.Invalid("invalid_ai_rate");
        var maximum = BillingMath.Cost(session.MaxDurationSeconds, rialRate);
        var w = await wallet.WalletLockedAsync(session.OrganizationId, ct);
        if (w.Balance - await wallet.ReservedAsync(session.OrganizationId, ct) < maximum)
            throw new AppException(409, "insufficient_ai_credit", "AI credit must be replenished before creating the invitation.");
        db.AiCreditReservations.Add(new() { OrganizationId = session.OrganizationId, WalletId = w.Id, InterviewSessionId = session.Id,
            ReservedAmountRial = maximum, CreatedAtUtc = now, ExpiresAtUtc = session.ExpiresAtUtc });
        db.InterviewBillings.Add(new() { OrganizationId = session.OrganizationId, InterviewSessionId = session.Id, SubscriptionId = subscription.Id,
            RatePerMinuteUsd = usdRate, RatePerMinuteRial = rialRate,
            RialPerUsdt = quote.RialPerUsdt, RateQuotedAtUtc = quote.FetchedAtUtc,
            RetentionDays = subscription.RetentionDays, CreatedAtUtc = now });
    }

    public Task<T> StartAsync<T>(InterviewSession session, Func<Task<T>> start, CancellationToken ct) =>
        db.WithOrganizationBillingLockAsync(session.OrganizationId, async () => {
            await wallet.ExpireLockedAsync(session.OrganizationId, ct);
            await db.ReloadInterviewAsync(session, ct);
            if (session.Status == InterviewStatus.Created)
                await entitlements.RequireAsync(session.OrganizationId, session.AskOneQuestionPerTopic || session.ProgressiveTopicQuestions
                    ? ["ai_interview", "advanced_ai_workflow", "adaptive_questions", "adaptive_difficulty"]
                    : ["ai_interview"], ct);
            await EnsureProcessingAllowedAsync(session, ct);
            // No live exchange lookup and no second reservation at candidate start.
            return await start();
        }, ct);

    public Task<decimal> SettleReplacementLockedAsync(InterviewSession session, CancellationToken ct) => wallet.SettleLockedAsync(session, false, ct);
    public async Task FinishAsync(InterviewSession session, bool release, CancellationToken ct)
    {
        var amount = await db.WithOrganizationBillingLockAsync(session.OrganizationId, async () => {
            var settled = await wallet.SettleLockedAsync(session, release, ct);
            return settled;
        }, ct);
        if (amount > 0) BillingMetrics.Consumed.Add(amount);
    }
    public async Task<T> FinishAsync<T>(InterviewSession session, bool release, Func<Task<T>> finish, CancellationToken ct)
    {
        decimal amount = 0;
        var value = await db.WithOrganizationBillingLockAsync(session.OrganizationId, async () => {
            var result = await finish(); amount = await wallet.SettleLockedAsync(session, release, ct);
            return result;
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
        if (subscription.IsTrial)
            return (await db.RegistrationTrials.AsNoTracking().Include(x => x.Entitlements).SingleAsync(x => x.OrganizationId == s.OrganizationId && x.Id == subscription.Id, ct))
                .Entitlements.Select(x => x.Code).ToHashSet(StringComparer.Ordinal);
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
