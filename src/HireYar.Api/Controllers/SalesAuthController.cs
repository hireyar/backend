using System.Security.Claims;
using HireYar.Application.Common;
using HireYar.Application.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HireYar.Api.Controllers;
[ApiController,Route("api/sales/auth"),EnableRateLimiting("auth")]
public sealed class SalesAuthController(ISalesAccounts accounts) : ControllerBase
{
    [AllowAnonymous,HttpPost("login")]
    public async Task<SalesSessionDto> Login(SalesLoginRequest r,CancellationToken ct) {
        var result=await accounts.LoginAsync(r,ct);
        return result.AccessToken.Length==0 ? throw AppException.Unauthorized() : result;
    }
    [Authorize(Policy="SalesAgent"),HttpGet("me")]
    public Task<SalesAgentDto?> Me(CancellationToken ct) => accounts.ValidateAsync(Guid.Parse(User.FindFirstValue("sub")!),Guid.Parse(User.FindFirstValue("sales_stamp")!),ct);
    [Authorize(Policy="SalesAgent"),HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct) { await accounts.LogoutAsync(Guid.Parse(User.FindFirstValue("sub")!),ct); return NoContent(); }
}
