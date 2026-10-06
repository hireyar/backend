using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Domain.JobPositions;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.JobPositions;

public sealed class JobService(IApplicationDbContext db, ICurrentUser user, TimeProvider time)
{
    private IQueryable<JobPosition> Scoped => db.JobPositions.Where(x => x.OrganizationId == user.OrganizationId && x.DeletedAtUtc == null);
    private static JobDto Map(JobPosition x) => new(x.Id, x.Title, x.Description, x.Seniority, x.Status, x.CreatedAtUtc, x.UpdatedAtUtc, x.RowVersion);
    public async Task<Page<JobDto>> ListAsync(PageQuery page, string? search, JobStatus? status, CancellationToken ct)
    {
        var query = Scoped.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Title.Contains(search));
        if (status.HasValue) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page.Skip).Take(page.PageSize)
            .Select(x => new JobDto(x.Id, x.Title, x.Description, x.Seniority, x.Status, x.CreatedAtUtc, x.UpdatedAtUtc, x.RowVersion)).ToListAsync(ct);
        return new(rows, total, page.Page, page.PageSize);
    }
    public async Task<JobDto> GetAsync(Guid id, CancellationToken ct) => Map(await Scoped.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound());
    public async Task<JobDto> CreateAsync(JobRequest request, CancellationToken ct)
    {
        var row = new JobPosition
        {
            OrganizationId = user.OrganizationId,
            CreatedAtUtc = time.GetUtcNow()
        };
        Apply(row, request);
        db.JobPositions.Add(row);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }
    public async Task<JobDto> UpdateAsync(Guid id, UpdateJobRequest request, CancellationToken ct)
    {
        var row = await Scoped.SingleOrDefaultAsync(x => x.Id == id, ct) ??
            throw AppException.NotFound();
        db.SetOriginalVersion(row, request.RowVersion); Apply(row, request.Job);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }
    public Task DeleteAsync(Guid id, CancellationToken ct) => db.WithOrganizationBillingLockAsync(user.OrganizationId, async () =>
    {
        var row = await Scoped.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        var now = time.GetUtcNow();
        row.DeletedAtUtc = now; row.Status = JobStatus.Archived; row.UpdatedAtUtc = now;
        var templates = await db.InterviewTemplates.Where(x => x.OrganizationId == user.OrganizationId && x.JobPositionId == id && x.DeletedAtUtc == null).ToListAsync(ct);
        foreach (var template in templates) { template.DeletedAtUtc = now; template.IsArchived = true; template.UpdatedAtUtc = now; }
        // Existing invitations keep their snapshots; no cascade destroys interview or financial history.
        return true;
    }, ct);
    private void Apply(JobPosition row, JobRequest request)
    {
        row.Title = request.Title.Trim();
        row.Description = request.Description.Trim();
        row.Seniority = request.Seniority.Trim();
        row.Status = request.Status;
        row.UpdatedAtUtc = time.GetUtcNow();
    }
}
