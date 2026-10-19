using HireYar.Application.Common;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Subscriptions;

public sealed class InterviewQuotaService(IApplicationDbContext db, SubscriptionService subscriptions, TimeProvider time)
{
    // All mutation methods join their caller's existing organization transaction.
    public async Task ReserveLockedAsync(InterviewSession session, CancellationToken ct)
    {
        var membership = await subscriptions.RequireAsync(session.OrganizationId, ct);
        var state = await subscriptions.StateAsync(session.OrganizationId, ct);
        if (session.MaxDurationSeconds > 1800) throw AppException.Invalid("interview_duration_exceeds_subscription");
        if (state.AvailableInterviews < 1) throw new AppException(409, "interview_quota_exhausted", "Purchase more interviews before inviting a candidate.");
        db.InterviewQuotaReservations.Add(new() { OrganizationId = session.OrganizationId, InterviewSessionId = session.Id,
            CreatedAtUtc = time.GetUtcNow(), RetentionDays = membership.RetentionDays });
    }
    public async Task ConsumeLockedAsync(InterviewSession session, CancellationToken ct)
    {
        var row = await db.InterviewQuotaReservations.SingleOrDefaultAsync(r => r.OrganizationId == session.OrganizationId && r.InterviewSessionId == session.Id, ct);
        // Historic invitations predate this model and are never retroactively charged.
        if (row is null) return;
        if (row.State == "Consumed") return;
        if (row.State != "Reserved") throw new AppException(409, "interview_quota_closed", "This invitation is no longer reserved.");
        await subscriptions.RequireAsync(session.OrganizationId, ct);
        row.State = "Consumed"; row.ConsumedAtUtc = time.GetUtcNow();
    }
    public async Task ReleaseLockedAsync(InterviewSession session, CancellationToken ct)
    {
        var row = await db.InterviewQuotaReservations.SingleOrDefaultAsync(r => r.OrganizationId == session.OrganizationId && r.InterviewSessionId == session.Id, ct);
        if (row?.State != "Reserved") return;
        row.State = "Released"; row.ReleasedAtUtc = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }
    public async Task RequireMediaAsync(InterviewSession session, CancellationToken ct)
    {
        var row = await db.InterviewQuotaReservations.AsNoTracking().SingleOrDefaultAsync(r => r.OrganizationId == session.OrganizationId && r.InterviewSessionId == session.Id, ct);
        if (row is null) return;
        var end = (session.StartedAtUtc ?? session.CreatedAtUtc).AddSeconds(session.MaxDurationSeconds);
        if (session.ExpiresAtUtc < end) end = session.ExpiresAtUtc;
        if (session.CompletedAtUtc is { } completed && completed < end) end = completed;
        if (time.GetUtcNow() >= end.AddDays(row.RetentionDays)) throw AppException.NotFound();
    }
}
