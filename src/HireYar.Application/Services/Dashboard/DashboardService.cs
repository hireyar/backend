using HireYar.Application.Common;
using HireYar.Application.Members;
using HireYar.Domain.JobPositions;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Dashboard;

public sealed class DashboardService(IApplicationDbContext db, ITeamMembers members, ICurrentUser user,
    TimeProvider time, HireYar.Application.Interviews.PanelWizardService panel)
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var counts = await db.JobPositions.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId).GroupBy(x => x.Status).Select(g => new JobStatusCount(g.Key, g.Count())).ToListAsync(ct);
        var jobs = Enum.GetValues<JobStatus>().Select(s => new JobStatusCount(s, counts.SingleOrDefault(x => x.Status == s)?.Count ?? 0)).ToArray();
        return new(jobs,
            await db.InterviewTemplates.CountAsync(x => x.OrganizationId == user.OrganizationId, ct),
            await members.ActiveCountAsync(user.OrganizationId, ct), await members.PendingCountAsync(user.OrganizationId, ct),
            await db.InterviewSessions.CountAsync(x => x.OrganizationId == user.OrganizationId && x.Status == HireYar.Domain.Interviews.InterviewStatus.Completed, ct),
            await db.InterviewSessions.CountAsync(x => x.OrganizationId == user.OrganizationId && x.Status == HireYar.Domain.Interviews.InterviewStatus.Created && x.ExpiresAtUtc > time.GetUtcNow() && x.AccessRevokedAtUtc == null, ct),
            (await panel.ListAsync(new() { PageSize = 5 }, ct)).Items);
    }
}
