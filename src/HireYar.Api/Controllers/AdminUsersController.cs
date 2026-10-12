using HireYar.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HireYar.Api.Controllers;

[ApiController, Route("api/admin/users"), Authorize(Policy = "SystemAdmin"), EnableRateLimiting("admin-auth")]
public sealed class AdminUsersController(IAdminAccounts accounts) : ControllerBase
{
    [HttpPost("super-admins")]
    public Task<AdminGrantDto> Grant(AdminGrantRequest request, CancellationToken ct) => accounts.GrantAsync(request, ct);
}
