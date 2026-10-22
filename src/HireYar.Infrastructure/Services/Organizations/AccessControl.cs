using HireYar.Application.Common;
using HireYar.Application.Organizations;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Infrastructure.Organizations;

public sealed class AccessControl(AppDbContext db, TimeProvider time) : IAccessControl
{
    private static AccessRoleDto Map(AccessRole r) => new(r.Code, r.Name, r.Description, r.IsActive, r.IsAdministrator, r.Revision,
        r.Sections.OrderBy(x => x.SectionCode).Select(x => new SectionPermission(x.SectionCode, x.Access)).ToArray(), AccessRoleRules.RequiresOwnerAssignment(r));
    public async Task<AccessRoleDto?> RoleAsync(Guid userId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive && x.PhoneNumberConfirmed, ct);
        if (u is null) throw AppException.Unauthorized();
        var code = u.Role == OrganizationRole.Owner ? "admin" : u.AccessRoleCode ?? "interview_manager";
        var r = await db.AccessRoles.AsNoTracking().Include(x => x.Sections).SingleOrDefaultAsync(x => x.Code == code, ct);
        return r is null ? null : Map(r);
    }
    public async Task<IReadOnlyList<SectionPermission>> PermissionsAsync(Guid userId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive && x.PhoneNumberConfirmed, ct);
        if (u is null) throw AppException.Unauthorized();
        // Ownership is an invariant; catalog edits cannot lock the organization owner out.
        if (u.Role == OrganizationRole.Owner) return AccessSections.Codes.Select(x => new SectionPermission(x, SectionAccess.Write)).ToArray();
        var code = u.AccessRoleCode ?? "interview_manager";
        return await (from grant in db.RoleSections.AsNoTracking()
            join role in db.AccessRoles.AsNoTracking() on grant.RoleCode equals role.Code
            join section in db.AccessSections.AsNoTracking() on grant.SectionCode equals section.Code
            where grant.RoleCode == code && role.IsActive && section.IsActive && grant.Access != SectionAccess.None
            select new SectionPermission(grant.SectionCode, grant.Access)).ToArrayAsync(ct);
    }
    public async Task<IReadOnlyList<AccessRoleDto>> RolesAsync(bool includeInactive, CancellationToken ct) =>
        (await db.AccessRoles.AsNoTracking().Include(x => x.Sections).Where(x => includeInactive || x.IsActive).OrderBy(x => x.Code).ToListAsync(ct)).Select(Map).ToArray();
    public async Task<IReadOnlyList<AccessSectionDto>> SectionsAsync(CancellationToken ct) =>
        await db.AccessSections.AsNoTracking().OrderBy(x => x.Code).Select(x => new AccessSectionDto(x.Code, x.Name, x.Description, x.IsActive, x.Revision)).ToListAsync(ct);
    public async Task<AccessRoleDto> SaveRoleAsync(Guid actor, SaveAccessRoleRequest q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.Name) || string.IsNullOrWhiteSpace(q.Description) || q.Sections is null ||
            q.Sections.Any(x => x is null) || q.Sections.Select(x => x.Code).Distinct().Count() != q.Sections.Count || q.Sections.Any(x => !Enum.IsDefined(x.Access)))
            throw AppException.Invalid("invalid_access_role");
        var known = await db.AccessSections.Select(x => x.Code).ToListAsync(ct);
        if (q.Sections.Any(x => !known.Contains(x.Code))) throw AppException.Invalid("unknown_access_section");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AccessCatalogLocks.ExclusiveAsync(db, ct);
        var role = await db.AccessRoles.Include(x => x.Sections).SingleOrDefaultAsync(x => x.Code == q.Code, ct);
        if (role is null) {
            if (q.Revision is not null) throw AppException.NotFound();
            role = new() { Code = q.Code }; db.AccessRoles.Add(role);
        } else {
            if (q.Revision != role.Revision) throw new AppException(409, "access_revision_conflict", "Refresh the access catalog.");
        }
        var before = Map(role);
        role.Name = q.Name.Trim(); role.Description = q.Description.Trim(); role.IsActive = q.IsActive; role.IsAdministrator = q.IsAdministrator;
        role.Revision = Guid.NewGuid();
        foreach (var code in known) {
            var row = role.Sections.SingleOrDefault(x => x.SectionCode == code);
            if (row is null) { row = new() { RoleCode = role.Code, SectionCode = code }; role.Sections.Add(row); }
            row.Access = q.Sections.SingleOrDefault(x => x.Code == code)?.Access ?? SectionAccess.None;
        }
        db.AccessAudits.Add(new() { ActorId = actor, Action = "RoleSaved", Detail = System.Text.Json.JsonSerializer.Serialize(new { Before = before, After = Map(role) }), CreatedAtUtc = time.GetUtcNow() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(role);
    }
    public async Task<AccessSectionDto> SaveSectionAsync(Guid actor, string code, UpdateAccessSectionRequest q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.Name) || string.IsNullOrWhiteSpace(q.Description)) throw AppException.Invalid("invalid_access_section");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AccessCatalogLocks.ExclusiveAsync(db, ct);
        var s = await db.AccessSections.SingleOrDefaultAsync(x => x.Code == code, ct) ?? throw AppException.NotFound();
        if (s.Revision != q.Revision) throw new AppException(409, "access_revision_conflict", "Refresh the access catalog.");
        var before = new AccessSectionDto(s.Code, s.Name, s.Description, s.IsActive, s.Revision);
        s.Name = q.Name.Trim(); s.Description = q.Description.Trim(); s.IsActive = q.IsActive; s.Revision = Guid.NewGuid();
        var after = new AccessSectionDto(s.Code, s.Name, s.Description, s.IsActive, s.Revision);
        db.AccessAudits.Add(new() { ActorId = actor, Action = "SectionSaved", Detail = System.Text.Json.JsonSerializer.Serialize(new { Before = before, After = after }), CreatedAtUtc = time.GetUtcNow() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return after;
    }
}
