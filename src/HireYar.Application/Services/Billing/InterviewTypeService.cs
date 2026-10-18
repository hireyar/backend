using System.Text.RegularExpressions;
using HireYar.Application.Common;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Billing;

public sealed class InterviewTypeService(IApplicationDbContext db, TimeProvider time)
{
    public async Task<IReadOnlyList<InterviewTypeDto>> ListAsync(bool publicOnly, CancellationToken ct)
        => await db.InterviewTypes.AsNoTracking().Where(x => !publicOnly || x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(x => new InterviewTypeDto(x.Code, x.Name, x.EngineType, x.IsActive, x.SortOrder, x.RowVersion)).ToListAsync(ct);

    public async Task<InterviewTypeDto> SaveAsync(string? code, InterviewTypeRequest r, byte[]? version, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Code) || !Regex.IsMatch(r.Code, "^[a-z][a-z0-9_]{1,63}$") ||
            string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 200 || r.SortOrder is < 0 or > 10000 ||
            r.EngineType is not ("general" or "specialized")) throw AppException.Invalid();
        var row = code is null ? new InterviewTypeDefinition { Code = r.Code, EngineType = r.EngineType }
            : await db.InterviewTypes.SingleOrDefaultAsync(x => x.Code == code, ct) ?? throw AppException.NotFound();
        // A code/strategy identifies historical interviews and cannot be repurposed.
        if (row.Code != r.Code || row.EngineType != r.EngineType) throw AppException.Invalid("interview_type_identifier_immutable");
        if (code is null) db.InterviewTypes.Add(row); else db.SetOriginalVersion(row, version!);
        row.Name = r.Name.Trim(); row.IsActive = r.IsActive; row.SortOrder = r.SortOrder;
        await db.SaveChangesAsync(ct);
        return new(row.Code, row.Name, row.EngineType, row.IsActive, row.SortOrder, row.RowVersion);
    }

    public async Task<PackageDto> SetPackageAsync(Guid packageId, string code, PackageInterviewTypeRequest r, CancellationToken ct)
    {
        if (r.PricePerMinuteUsd is <= 0 or > 1000 || !BillingMath.HasPrecision(r.PricePerMinuteUsd, 6))
            throw AppException.Invalid("invalid_interview_price");
        var package = await db.PricingCatalog.Include(x => x.Periods).Include(x => x.Features).ThenInclude(x => x.Feature)
            .Include(x => x.InterviewTypes).ThenInclude(x => x.InterviewType).SingleOrDefaultAsync(x => x.Id == packageId, ct)
            ?? throw AppException.NotFound();
        var type = await db.InterviewTypes.SingleOrDefaultAsync(x => x.Code == code, ct) ?? throw AppException.NotFound();
        db.SetOriginalVersion(package, r.RowVersion); package.UpdatedAtUtc = time.GetUtcNow();
        var row = package.InterviewTypes.SingleOrDefault(x => x.InterviewTypeCode == code);
        if (row is null)
        {
            row = new() { PackageId = package.Id, InterviewTypeCode = code, InterviewType = type };
            package.InterviewTypes.Add(row); db.PackageInterviewTypes.Add(row);
        }
        row.PricePerMinuteUsd = r.PricePerMinuteUsd; row.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        return PricingService.Map(package, false);
    }

    internal static async Task<InterviewTypeDefinition> RequireTypeAsync(IApplicationDbContext db, string code, CancellationToken ct)
        => await db.InterviewTypes.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code && x.IsActive, ct)
            ?? throw new AppException(409, "interview_type_unavailable", "The interview type is unknown or inactive.");

    internal static string Capability(InterviewTypeDefinition type)
        => type.EngineType == "specialized" ? "technical_interview" : "hr_interview";
}
