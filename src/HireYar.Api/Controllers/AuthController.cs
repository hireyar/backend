using HireYar.Api.Security;
using HireYar.Application.Auth;
using HireYar.Application.Common;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/auth"), EnableRateLimiting("auth"), ServiceFilter(typeof(CsrfFilter))]
public sealed class AuthController(IAccounts service, ICurrentUser user, IWebHostEnvironment env) : ControllerBase
{
    private const string RefreshCookie = "hireyar.refresh";
    private CookieOptions Cookie(DateTimeOffset? expires = null) => new() { HttpOnly = true, Secure = !env.IsDevelopment(), SameSite = SameSiteMode.Strict, Path = "/api/auth", Expires = expires, IsEssential = true };
    private AccessResponse Session(SessionResult r) { Response.Cookies.Append(RefreshCookie, r.RefreshToken, Cookie(r.RefreshExpiresAtUtc)); return new(r.AccessToken, r.AccessExpiresAtUtc); }
    [AllowAnonymous, HttpGet("csrf")]
    public ActionResult<CsrfResponse> Csrf([FromServices] IAntiforgery antiforgery)
    { Response.Headers.CacheControl = "no-store"; return new CsrfResponse(antiforgery.GetAndStoreTokens(HttpContext).RequestToken!); }
    [AllowAnonymous, HttpPost("otp/request")]
    public Task<OtpRequestResult> RequestOtp(RequestOtpRequest r, CancellationToken ct) => service.RequestOtpAsync(r, ct);
    [AllowAnonymous, HttpPost("otp/verify")]
    public async Task<AccessResponse> VerifyOtp(VerifyOtpRequest r, CancellationToken ct) => Session(await service.VerifyOtpAsync(r, ct));
    [AllowAnonymous, HttpPost("refresh")]
    public async Task<AccessResponse> Refresh(CancellationToken ct) => Session(await service.RefreshAsync(Request.Cookies[RefreshCookie] ?? "", ct));
    [AllowAnonymous, HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct) { await service.LogoutAsync(Request.Cookies[RefreshCookie] ?? "", ct); Response.Cookies.Delete(RefreshCookie, Cookie()); return NoContent(); }
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct) { await service.LogoutAllAsync(user.UserId, ct); Response.Cookies.Delete(RefreshCookie, Cookie()); return NoContent(); }
    [HttpGet("me")]
    public Task<CurrentUserDto> Me(CancellationToken ct) => service.GetAsync(user.UserId, ct);
    [HttpPut("me")]
    public Task<CurrentUserDto> Update(NameRequest r, CancellationToken ct) => service.UpdateAsync(user.UserId, r, ct);

}
