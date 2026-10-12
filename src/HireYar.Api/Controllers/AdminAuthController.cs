using System.Security.Claims;
using HireYar.Api.Security;
using HireYar.Application.Auth;
using HireYar.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/admin/auth"), EnableRateLimiting("admin-auth"), ServiceFilter(typeof(AdminEntryFilter))]
public sealed class AdminAuthController(IAdminAccounts accounts, IAdminCaptcha captcha) : ControllerBase
{
    [AllowAnonymous, HttpGet("entry")]
    public IActionResult Entry() => Ok(new { enabled = true });
    [AllowAnonymous, HttpGet("captcha")]
    public AdminCaptchaDto Captcha() => captcha.Create();
    [AllowAnonymous, HttpPost("login")]
    public Task<AdminSessionDto> Login(AdminLoginRequest r, CancellationToken ct) => accounts.LoginAsync(r, ct);
    [Authorize(Policy = "SystemAdmin"), HttpGet("me")]
    public async Task<AdminIdentityDto?> Me(CancellationToken ct) => await accounts.ValidateAsync(Guid.Parse(User.FindFirstValue("sub")!), User.FindFirstValue("admin_stamp")!, ct);
    [Authorize(Policy = "SystemAdmin"), HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await accounts.RevokeSessionsAsync(Guid.Parse(User.FindFirstValue("sub")!), ct);
        return NoContent();
    }
}
