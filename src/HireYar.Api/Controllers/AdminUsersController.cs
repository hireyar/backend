using HireYar.Application.Auth;
using HireYar.Application.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HireYar.Api.Controllers;

[ApiController, Route("api/admin/users"), Authorize(Policy = "SystemAdmin"), EnableRateLimiting("admin-auth")]
public sealed class AdminUsersController(IAdminAccounts accounts) : ControllerBase
{
    [HttpGet("super-admins")]
    public Task<Page<AdminUserDto>> List([FromQuery] PageQuery pagination, [FromQuery] SearchQuery filter, CancellationToken ct)
        => accounts.ListAsync(pagination, filter.Search, ct);

    [HttpDelete("super-admins/{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await accounts.RevokeAdminAsync(Guid.Parse(User.FindFirstValue("sub")!), id, ct);
        return NoContent();
    }

    [HttpPost("super-admins")]
    public Task<AdminGrantDto> Grant(AdminGrantRequest request, CancellationToken ct) => accounts.GrantAsync(request, ct);
}
