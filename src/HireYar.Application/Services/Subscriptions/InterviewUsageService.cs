using HireYar.Application.Common;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Subscriptions;

public sealed class InterviewUsageService(IApplicationDbContext db, TimeProvider time)
{
    private IQueryable<InterviewSession> Sessions(Guid org) => db.InterviewSessions.IgnoreAutoIncludes().AsNoTracking().Where(s => s.OrganizationId == org);

    private IQueryable<InterviewUsageDto> Project(IQueryable<InterviewSession> sessions, Guid org)
    {
        var now = time.GetUtcNow();
        return from s in sessions
               join c in db.CandidateApplications.AsNoTracking() on s.CandidateApplicationId equals c.Id
               join r in db.InterviewQuotaReservations.AsNoTracking() on s.Id equals r.InterviewSessionId into reservations
               from r in reservations.DefaultIfEmpty()
               where s.OrganizationId == org && c.OrganizationId == org && (r == null || r.OrganizationId == org)
               select new InterviewUsageDto(s.Id, c.FirstName + " " + c.LastName, s.PositionTitleSnapshot,
                   s.TemplateTitleSnapshot, s.InterviewType,
                   s.DeletedAtUtc != null ? "Deleted" : s.SupersededByInterviewId != null ? "Replaced" :
                   s.Status == InterviewStatus.Cancelled ? "Cancelled" : s.Status == InterviewStatus.Completed ? "Completed" :
                   s.AccessRevokedAtUtc != null ? "Revoked" : s.ExpiresAtUtc <= now ? "Expired" :
                   s.StartedAtUtc != null ? "InProgress" : "Created",
                   s.CreatedAtUtc, s.ExpiresAtUtc, s.MaxDurationSeconds, s.StartedAtUtc, s.CompletedAtUtc,
                   s.StartedAtUtc != null && s.CompletedAtUtc != null ? (int?)(s.CompletedAtUtc.Value - s.StartedAtUtc.Value).TotalSeconds : null,
                   r != null && r.State == "Consumed" ? 1 : 0,
                   r != null && r.State == "Reserved" && s.Status == InterviewStatus.Created && s.ExpiresAtUtc > now &&
                       s.AccessRevokedAtUtc == null && s.DeletedAtUtc == null, s.SupersededByInterviewId);
    }

    public async Task<Page<InterviewUsageDto>> ListAsync(Guid org, InterviewUsageQuery query, CancellationToken ct)
    {
        if (query.FromUtc?.Offset != null && query.FromUtc.Value.Offset != TimeSpan.Zero ||
            query.ToUtc?.Offset != null && query.ToUtc.Value.Offset != TimeSpan.Zero || query.FromUtc > query.ToUtc)
            throw AppException.Invalid("invalid_usage_date_range");
        var rows = Sessions(org);
        var now = time.GetUtcNow();
        if (query.Status is not null) rows = rows.Where(s =>
            (s.DeletedAtUtc != null ? "Deleted" : s.SupersededByInterviewId != null ? "Replaced" :
             s.Status == InterviewStatus.Cancelled ? "Cancelled" : s.Status == InterviewStatus.Completed ? "Completed" :
             s.AccessRevokedAtUtc != null ? "Revoked" : s.ExpiresAtUtc <= now ? "Expired" :
             s.StartedAtUtc != null ? "InProgress" : "Created") == query.Status);
        if (query.FromUtc is { } from) rows = rows.Where(s => s.CreatedAtUtc >= from);
        if (query.ToUtc is { } to) rows = rows.Where(s => s.CreatedAtUtc < to);
        var total = await rows.CountAsync(ct);
        var skip = (int)Math.Min((long)(query.Page - 1) * query.PageSize, int.MaxValue);
        var items = await Project(rows.OrderByDescending(s => s.CreatedAtUtc).ThenByDescending(s => s.Id)
            .Skip(skip).Take(query.PageSize), org).ToListAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }

    public async Task<InterviewUsageDetailDto> GetAsync(Guid org, Guid id, CancellationToken ct)
    {
        var row = await Project(Sessions(org).Where(s => s.Id == id), org).SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();
        var events = await db.InterviewUsageEvents.AsNoTracking().Where(e => e.OrganizationId == org && e.InterviewSessionId == id)
            .OrderBy(e => e.OccurredAtUtc).ThenBy(e => e.EventType).ThenBy(e => e.Id)
            .Select(e => new InterviewUsageEventDto(e.Id, e.EventType, e.OccurredAtUtc, e.EventType == "CreditConsumed" ? 1 : 0, e.IsHistorical))
            .ToListAsync(ct);
        return new(row, events);
    }
}
