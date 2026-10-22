using System.Security.Claims;
using HireYar.Application.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/admin/access"), Authorize(Policy = "SystemAdmin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminAccessController(IAccessControl access) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue("sub")!);
    [HttpGet("roles")] public Task<IReadOnlyList<AccessRoleDto>> Roles(CancellationToken ct) => access.RolesAsync(true, ct);
    [HttpPut("roles")] public Task<AccessRoleDto> SaveRole(SaveAccessRoleRequest request, CancellationToken ct) => access.SaveRoleAsync(Actor, request, ct);
    [HttpGet("sections")] public Task<IReadOnlyList<AccessSectionDto>> Sections(CancellationToken ct) => access.SectionsAsync(ct);
    [HttpPut("sections/{code}")] public Task<AccessSectionDto> SaveSection(string code, UpdateAccessSectionRequest request, CancellationToken ct) => access.SaveSectionAsync(Actor, code, request, ct);
}
