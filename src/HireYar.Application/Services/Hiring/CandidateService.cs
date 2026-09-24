using HireYar.Application.Common;
using HireYar.Domain.Hiring;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Hiring;

public sealed class CandidateService(IApplicationDbContext db, ICurrentUser user, TimeProvider time)
{
    private IQueryable<Candidate> Scoped => db.Candidates.Where(x => x.OrganizationId == user.OrganizationId);
    private static CandidateDto Map(Candidate x) => new(x.Id, x.FullName, x.Email, x.PhoneNumber, x.CreatedAtUtc, x.UpdatedAtUtc, x.RowVersion);
    private static ApplicationDto Map(JobApplication x) => new(x.Id, x.CandidateId, x.JobPositionId, x.Status, x.CreatedAtUtc, x.RowVersion);
    public async Task<Page<CandidateDto>> ListAsync(PageQuery page, string? search, CancellationToken ct)
    {
        var query = Scoped.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToUpperInvariant(); query = query.Where(x => x.FullName.ToUpper().Contains(term) || x.NormalizedEmail.Contains(term)); }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToArray(), total, page.Page, page.PageSize);
    }
    public async Task<CandidateDto> GetAsync(Guid id, CancellationToken ct) => Map(await Scoped.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound());
    public async Task<CandidateDto> CreateAsync(CandidateRequest r, CancellationToken ct)
    {
        await using var tx = await db.BeginHiringTransactionAsync(user.OrganizationId, ct);
        var row = new Candidate { OrganizationId = user.OrganizationId, CreatedAtUtc = InterviewRules.Now(time) }; Apply(row, r);
        db.Candidates.Add(row); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(row);
    }
    public async Task<CandidateDto> UpdateAsync(Guid id, UpdateCandidateRequest r, CancellationToken ct)
    {
        await using var tx = await db.BeginHiringTransactionAsync(user.OrganizationId, ct);
        var row = await Scoped.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        db.SetOriginalVersion(row, r.RowVersion); Apply(row, r.Candidate); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(row);
    }
    private void Apply(Candidate row, CandidateRequest r)
    {
        row.FullName = r.FullName.Trim(); row.Email = r.Email.Trim(); row.NormalizedEmail = row.Email.ToUpperInvariant(); row.PhoneNumber = r.PhoneNumber?.Trim(); row.UpdatedAtUtc = InterviewRules.Now(time);
    }
    public async Task<ApplicationDto> CreateApplicationAsync(ApplicationRequest r, CancellationToken ct)
    {
        await using var tx = await db.BeginHiringTransactionAsync(user.OrganizationId, ct);
        if (!await Scoped.AnyAsync(x => x.Id == r.CandidateId, ct) || !await db.JobPositions.AnyAsync(x => x.Id == r.JobPositionId && x.OrganizationId == user.OrganizationId, ct)) throw AppException.NotFound();
        var row = new JobApplication { OrganizationId = user.OrganizationId, CandidateId = r.CandidateId, JobPositionId = r.JobPositionId, CreatedAtUtc = InterviewRules.Now(time), UpdatedAtUtc = InterviewRules.Now(time) };
        db.JobApplications.Add(row); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(row);
    }
    public async Task<Page<ApplicationDto>> ApplicationsAsync(PageQuery page, Guid? candidateId, Guid? jobPositionId, CancellationToken ct)
    {
        if (candidateId.HasValue && !await Scoped.AnyAsync(x => x.Id == candidateId, ct)) throw AppException.NotFound();
        if (jobPositionId.HasValue && !await db.JobPositions.AnyAsync(x => x.Id == jobPositionId && x.OrganizationId == user.OrganizationId, ct)) throw AppException.NotFound();
        var query = db.JobApplications.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId);
        if (candidateId.HasValue) query = query.Where(x => x.CandidateId == candidateId);
        if (jobPositionId.HasValue) query = query.Where(x => x.JobPositionId == jobPositionId);
        var total = await query.CountAsync(ct); var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToArray(), total, page.Page, page.PageSize);
    }
    public async Task<ApplicationDto> UpdateApplicationAsync(Guid id, ApplicationStatusRequest r, CancellationToken ct)
    {
        await using var tx = await db.BeginHiringTransactionAsync(user.OrganizationId, ct);
        var row = await db.JobApplications.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId, ct) ?? throw AppException.NotFound();
        db.SetOriginalVersion(row, r.RowVersion); row.Status = r.Status; row.UpdatedAtUtc = InterviewRules.Now(time);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(row);
    }
}

