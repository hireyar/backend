using HireYar.Application.Common;
using HireYar.Application.Members;
using HireYar.Domain.JobPositions;
using HireYar.Domain.Hiring;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Dashboard;

public sealed class DashboardService(IApplicationDbContext db, ITeamMembers members, ICurrentUser user, TimeProvider time)
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var counts = await db.JobPositions.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId).GroupBy(x => x.Status).Select(g => new JobStatusCount(g.Key, g.Count())).ToListAsync(ct);
        var jobs = Enum.GetValues<JobStatus>().Select(s => new JobStatusCount(s, counts.SingleOrDefault(x => x.Status == s)?.Count ?? 0)).ToArray();
        var now = time.GetUtcNow();
        var sessionCounts = await db.InterviewSessions.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId)
            .GroupBy(x => x.Status == SessionStatus.NotStarted && x.ExpiresAtUtc <= now || x.Status == SessionStatus.InProgress && x.DeadlineAtUtc <= now ? SessionStatus.Expired : x.Status)
            .Select(g => new SessionStatusCount(g.Key, g.Count())).ToListAsync(ct);
        var sessions = Enum.GetValues<SessionStatus>().Select(s => new SessionStatusCount(s, sessionCounts.SingleOrDefault(x => x.Status == s)?.Count ?? 0)).ToArray();
        return new(jobs, await db.InterviewTemplates.CountAsync(x => x.OrganizationId == user.OrganizationId, ct),
            await members.ActiveCountAsync(user.OrganizationId, ct), await members.PendingCountAsync(user.OrganizationId, ct),
            await db.Candidates.CountAsync(x => x.OrganizationId == user.OrganizationId, ct),
            await db.JobApplications.CountAsync(x => x.OrganizationId == user.OrganizationId && x.Status == ApplicationStatus.Active, ct), sessions,
            await db.InterviewInvitations.CountAsync(x => x.OrganizationId == user.OrganizationId && x.DeliveryStatus == DeliveryStatus.Failed, ct));
    }
}
