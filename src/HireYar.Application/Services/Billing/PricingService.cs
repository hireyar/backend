using System.Globalization;
using HireYar.Application.Common;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Billing;

public sealed class PricingService(IApplicationDbContext db, TimeProvider time)
{
    private IQueryable<PricingPackage> Packages => db.PricingCatalog.Include(x => x.Periods).Include(x => x.Features).ThenInclude(x => x.Feature);
    public async Task<IReadOnlyList<PackageDto>> ListAsync(bool publicOnly, CancellationToken ct)
    {
        var query = Packages.AsNoTracking();
        if (publicOnly) query = query.Where(x => x.IsActive && x.IsPublic && x.Periods.Any(p => p.IsActive));
        return (await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Take(100).ToListAsync(ct)).Select(x => Map(x, publicOnly)).ToArray();
    }
    public async Task<PackageDto> GetAsync(Guid id, CancellationToken ct) => Map(await Packages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound(), false);
    public async Task<Page<PackageDto>> AdminPackagesAsync(PageQuery page, CancellationToken ct, string? search = null)
    {
        var query = Packages.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term) || x.Description.ToLower().Contains(term));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new(rows.Select(x => Map(x, false)).ToArray(), total, page.Page, page.PageSize);
    }

    public async Task<Page<CreditPackageDto>> AdminCreditsAsync(PageQuery page, CancellationToken ct, string? search = null)
    {
        var query = db.AiCreditPackages.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new(rows.Select(MapCredit).ToArray(), total, page.Page, page.PageSize);
    }
    public async Task<PackageDto> SaveAsync(Guid? id, PackageRequest r, byte[]? version, CancellationToken ct)
    {
        ValidatePackage(r);
        var p = id is { } key ? await Packages.SingleOrDefaultAsync(x => x.Id == key, ct) ?? throw AppException.NotFound()
            : new PricingPackage { CreatedAtUtc = time.GetUtcNow() };
        if (id is null) db.PricingPackages.Add(p); else db.SetOriginalVersion(p, version!);
        p.Code = r.Code; p.Name = r.Name.Trim(); p.Description = r.Description.Trim(); p.IsActive = r.IsActive; p.IsPublic = r.IsPublic;
        p.SortOrder = r.SortOrder; p.AiPricePerMinuteUsd = r.AiPricePerMinuteUsd; p.InterviewRetentionDays = r.InterviewRetentionDays; p.UpdatedAtUtc = time.GetUtcNow();
        p.IncludedTeamUsers = r.IncludedTeamUsers; p.PaidTeamUserLimit = r.PaidTeamUserLimit; p.TeamUserMonthlyPriceIrt = r.TeamUserMonthlyPriceIrt;
        await db.SaveChangesAsync(ct); return Map(p, false);
    }
    public async Task<PackageDto> SavePeriodAsync(Guid packageId, Guid? id, PeriodWriteRequest r, CancellationToken ct)
    {
        var p = await Packages.SingleOrDefaultAsync(x => x.Id == packageId, ct) ?? throw AppException.NotFound();
        var data = r.Period;
        if (data.Months is < 1 or > 120 || data.PriceAmount <= 0 || data.PriceAmount > 1000000000000m || data.Currency != "IRT" ||
            !BillingMath.HasPrecision(data.PriceAmount, 0) || data.DiscountPercent is < 0 or > 100 || !BillingMath.HasPrecision(data.DiscountPercent, 2)) throw AppException.Invalid();
        db.SetOriginalVersion(p, r.RowVersion); p.UpdatedAtUtc = time.GetUtcNow();
        var row = id is { } key ? p.Periods.SingleOrDefault(x => x.Id == key) ?? throw AppException.NotFound()
            : new PackagePeriod { PackageId = p.Id, CreatedAtUtc = time.GetUtcNow() };
        if (id is null) { p.Periods.Add(row); db.PackagePeriods.Add(row); }
        row.Months = data.Months; row.PriceAmount = data.PriceAmount; row.Currency = data.Currency; row.DiscountPercent = data.DiscountPercent;
        row.IsActive = data.IsActive; row.SortOrder = data.SortOrder; row.UpdatedAtUtc = time.GetUtcNow();
        await db.SaveChangesAsync(ct); return Map(p, false);
    }
    public async Task<FeatureDto> SaveFeatureDefinitionAsync(Guid? id, FeatureRequest r, CancellationToken ct)
    {
        // Codes and types are stable integration identifiers. Updating display metadata is safe.
        if (r.Code == "interview_retention_days" || !Enum.IsDefined(r.DataType) || string.IsNullOrWhiteSpace(r.Name)) throw AppException.Invalid();
        var row = id is { } key ? await db.FeatureDefinitions.SingleOrDefaultAsync(x => x.Id == key, ct) ?? throw AppException.NotFound() : new FeatureDefinition { Code = r.Code, DataType = r.DataType };
        if (row.Code != r.Code || row.DataType != r.DataType) throw AppException.Invalid("feature_identifier_immutable");
        row.Name = r.Name.Trim(); row.Description = r.Description.Trim(); if (id is null) db.FeatureDefinitions.Add(row);
        await db.SaveChangesAsync(ct); return new(row.Id, row.Code, row.Name, row.Description, row.DataType, false, null);
    }
    public async Task<IReadOnlyList<FeatureDto>> FeaturesAsync(CancellationToken ct) => await db.FeatureDefinitions.AsNoTracking().OrderBy(x => x.Code).Take(200)
        .Select(x => new FeatureDto(x.Id, x.Code, x.Name, x.Description, x.DataType, false, null)).ToListAsync(ct);
    public async Task<PackageDto> SetFeatureAsync(Guid packageId, FeatureWriteRequest r, CancellationToken ct)
    {
        var p = await Packages.SingleOrDefaultAsync(x => x.Id == packageId, ct) ?? throw AppException.NotFound();
        var f = await db.FeatureDefinitions.SingleOrDefaultAsync(x => x.Id == r.FeatureId, ct) ?? throw AppException.NotFound();
        if (f.DataType == FeatureDataType.Boolean && r.Value is not null || f.DataType == FeatureDataType.Integer && !int.TryParse(r.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
            f.DataType == FeatureDataType.Text && string.IsNullOrWhiteSpace(r.Value)) throw AppException.Invalid("invalid_feature_value");
        db.SetOriginalVersion(p, r.RowVersion); p.UpdatedAtUtc = time.GetUtcNow();
        var row = p.Features.SingleOrDefault(x => x.FeatureId == f.Id);
        if (row is null) { row = new PackageFeature { PackageId = p.Id, FeatureId = f.Id, Feature = f }; p.Features.Add(row); db.PackageFeatures.Add(row); }
        row.IsEnabled = r.IsEnabled; row.Value = r.Value; await db.SaveChangesAsync(ct); return Map(p, false);
    }
    public async Task<IReadOnlyList<CreditPackageDto>> CreditsAsync(bool publicOnly, CancellationToken ct)
    {
        var q = db.AiCreditPackages.AsNoTracking(); if (publicOnly) q = q.Where(x => x.IsActive && x.IsPublic);
        return (await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Take(100).ToListAsync(ct)).Select(MapCredit).ToArray();
    }
    public async Task<CreditPackageDto> SaveCreditAsync(Guid? id, CreditPackageRequest r, byte[]? version, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.CreditAmountRial is <= 0 or > 999999999999m || r.SellPriceAmount is <= 0 or > 1000000000000m ||
            r.SellPriceCurrency != "IRT" || !BillingMath.HasPrecision(r.CreditAmountRial, 6) || !BillingMath.HasPrecision(r.SellPriceAmount, 0) ||
            r.SellPriceAmount / r.CreditAmountRial > 999999999999999999m) throw AppException.Invalid();
        var p = id is { } key ? await db.AiCreditPackages.SingleOrDefaultAsync(x => x.Id == key, ct) ?? throw AppException.NotFound() : new AiCreditPackage { CreatedAtUtc = time.GetUtcNow() };
        if (id is null) db.AiCreditPackages.Add(p); else db.SetOriginalVersion(p, version!);
        p.Name = r.Name.Trim(); p.CreditAmountRial = r.CreditAmountRial; p.SellPriceAmount = r.SellPriceAmount; p.SellPriceCurrency = r.SellPriceCurrency;
        p.IsActive = r.IsActive; p.IsPublic = r.IsPublic; p.SortOrder = r.SortOrder; p.UpdatedAtUtc = time.GetUtcNow(); await db.SaveChangesAsync(ct); return MapCredit(p);
    }
    private static void ValidatePackage(PackageRequest r)
    {
        if (r.IncludedTeamUsers < 1 || r.PaidTeamUserLimit < r.IncludedTeamUsers || r.PaidTeamUserLimit > 10000 ||
            r.TeamUserMonthlyPriceIrt is < 0 or > 1000000000000m || !BillingMath.HasPrecision(r.TeamUserMonthlyPriceIrt, 0)) throw AppException.Invalid("invalid_team_pricing");
        if (string.IsNullOrWhiteSpace(r.Name) || !System.Text.RegularExpressions.Regex.IsMatch(r.Code, "^[a-z][a-z0-9_]{1,63}$") ||
            r.AiPricePerMinuteUsd is <= 0 or > 1000 || !BillingMath.HasPrecision(r.AiPricePerMinuteUsd, 6) || r.InterviewRetentionDays is < 1 or > 3650) throw AppException.Invalid();
    }
    public static PackageDto Map(PricingPackage p, bool publicOnly) => new(p.Id, p.Code, p.Name, p.Description, p.IsActive, p.IsPublic, p.SortOrder,
        p.AiPricePerMinuteUsd, p.InterviewRetentionDays, p.RowVersion,
        p.Features.Where(x => !publicOnly || x.IsEnabled).OrderBy(x => x.Feature.Code).Select(x => new FeatureDto(x.FeatureId, x.Feature.Code, x.Feature.Name, x.Feature.Description, x.Feature.DataType, x.IsEnabled, x.Value)).ToArray(),
        p.Periods.Where(x => !publicOnly || x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Months).Select(x => new PeriodDto(x.Id, x.Months, x.PriceAmount, x.Currency, x.DiscountPercent, x.IsActive, x.SortOrder)).ToArray(),
        p.IncludedTeamUsers, p.PaidTeamUserLimit, p.TeamUserMonthlyPriceIrt);
    private static CreditPackageDto MapCredit(AiCreditPackage p) => new(p.Id, p.Name, p.CreditAmountRial, p.SellPriceAmount, p.SellPriceCurrency, p.IsActive, p.IsPublic, p.SortOrder, p.RowVersion);
}
