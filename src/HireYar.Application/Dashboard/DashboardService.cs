using HireYar.Application.Common;
using HireYar.Application.Members;
using HireYar.Domain.JobPositions;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Dashboard;

public sealed record JobStatusCount(JobStatus Status, int Count);
public sealed record DashboardDto(IReadOnlyList<JobStatusCount> Jobs, int Templates, int ActiveMembers, int PendingInvitations);
public sealed class DashboardService(IApplicationDbContext db, ITeamMembers members, ICurrentUser user)
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var counts = await db.JobPositions.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId).GroupBy(x => x.Status).Select(g => new JobStatusCount(g.Key, g.Count())).ToListAsync(ct);
        var jobs = Enum.GetValues<JobStatus>().Select(s => new JobStatusCount(s, counts.SingleOrDefault(x => x.Status == s)?.Count ?? 0)).ToArray();
        return new(jobs,
            await db.InterviewTemplates.CountAsync(x => x.OrganizationId == user.OrganizationId, ct),
            await members.ActiveCountAsync(user.OrganizationId, ct), await members.PendingCountAsync(user.OrganizationId, ct));
    }
}
