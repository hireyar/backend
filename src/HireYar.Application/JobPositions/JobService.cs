using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Domain.JobPositions;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.JobPositions;

public sealed record JobRequest(
    [Required, StringLength(200)] string Title,
    [Required, StringLength(10000)] string Description,
    [Required, StringLength(100)] string Seniority,
    [EnumDataType(typeof(JobStatus))] JobStatus Status);
public sealed record UpdateJobRequest(
    [Required] JobRequest Job,
    [Required, MinLength(8), MaxLength(8)] byte[] RowVersion);
public sealed record JobDto(Guid Id, string Title, string Description, string Seniority, JobStatus Status, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, byte[] RowVersion);
public sealed class JobService(IApplicationDbContext db, ICurrentUser user, TimeProvider time)
{
    private IQueryable<JobPosition> Scoped => db.JobPositions.Where(x => x.OrganizationId == user.OrganizationId);
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
    private void Apply(JobPosition row, JobRequest request)
    {
        row.Title = request.Title.Trim();
        row.Description = request.Description.Trim();
        row.Seniority = request.Seniority.Trim();
        row.Status = request.Status;
        row.UpdatedAtUtc = time.GetUtcNow();
    }
}

