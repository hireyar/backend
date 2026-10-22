using HireYar.Domain.Organizations;
namespace HireYar.Application.Organizations;

public interface IAccessControl
{
    Task<IReadOnlyList<SectionPermission>> PermissionsAsync(Guid userId, CancellationToken ct);
    Task<AccessRoleDto?> RoleAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<AccessRoleDto>> RolesAsync(bool includeInactive, CancellationToken ct);
    Task<IReadOnlyList<AccessSectionDto>> SectionsAsync(CancellationToken ct);
    Task<AccessRoleDto> SaveRoleAsync(Guid actor, SaveAccessRoleRequest request, CancellationToken ct);
    Task<AccessSectionDto> SaveSectionAsync(Guid actor, string code, UpdateAccessSectionRequest request, CancellationToken ct);
}
