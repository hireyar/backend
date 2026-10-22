using System.ComponentModel.DataAnnotations;
using HireYar.Domain.Organizations;
namespace HireYar.Application.Organizations;

public sealed record SectionPermission(string Code, SectionAccess Access);
public sealed record AccessRoleDto(string Code, string Name, string Description, bool IsActive, bool IsAdministrator, Guid Revision, IReadOnlyList<SectionPermission> Sections);
public sealed record AccessSectionDto(string Code, string Name, string Description, bool IsActive, Guid Revision);
public sealed record SaveAccessRoleRequest(
    [Required, RegularExpression("^[a-z][a-z0-9_]{1,63}$")] string Code,
    [Required, StringLength(100)] string Name, [Required, StringLength(1000)] string Description,
    bool IsActive, bool IsAdministrator, Guid? Revision, [Required, MaxLength(30)] IReadOnlyList<SectionPermission> Sections);
public sealed record UpdateAccessSectionRequest([Required, StringLength(100)] string Name,
    [Required, StringLength(1000)] string Description, bool IsActive, Guid Revision);
public sealed record ChangeMemberAccessRequest([Required, StringLength(64)] string AccessRoleCode);
public sealed record TeamEmailResult(string Status);
