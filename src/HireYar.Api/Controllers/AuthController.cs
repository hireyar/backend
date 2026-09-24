using HireYar.Api.Security;
using HireYar.Application.Auth;
using HireYar.Application.Members;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/auth"), EnableRateLimiting("auth"), ServiceFilter(typeof(CsrfFilter))]
public sealed class AuthController(AuthService service, MemberService members, IWebHostEnvironment env) : ControllerBase
{
    private const string RefreshCookie = "hireyar.refresh";
    private CookieOptions Cookie(DateTimeOffset? expires = null) => new() { HttpOnly = true, Secure = !env.IsDevelopment(), SameSite = SameSiteMode.Strict, Path = "/api/auth", Expires = expires, IsEssential = true };
    private AccessResponse Session(SessionResult r) { Response.Cookies.Append(RefreshCookie, r.RefreshToken, Cookie(r.RefreshExpiresAtUtc)); return new(r.AccessToken, r.AccessExpiresAtUtc); }
    [AllowAnonymous, HttpGet("csrf")]
    public ActionResult<CsrfResponse> Csrf([FromServices] IAntiforgery antiforgery)
    { Response.Headers.CacheControl = "no-store"; return new CsrfResponse(antiforgery.GetAndStoreTokens(HttpContext).RequestToken!); }
    [AllowAnonymous, HttpPost("register")]
    public Task<RegistrationResult> Register(RegisterRequest r, CancellationToken ct) => service.RegisterAsync(r, ct);
    [AllowAnonymous, HttpPost("confirm-email")]
    public async Task<IActionResult> Confirm(TokenRequest r, CancellationToken ct) { await service.ConfirmAsync(r, ct); return NoContent(); }
    [AllowAnonymous, HttpPost("resend-confirmation")]
    public async Task<IActionResult> Resend(EmailRequest r, CancellationToken ct)
    {
        await service.ResendAsync(r, ct);
        return Accepted(new GenericEmailResponse());
    }
    [AllowAnonymous, HttpPost("login")]
    public async Task<AccessResponse> Login(LoginRequest r, CancellationToken ct) => Session(await service.LoginAsync(r, ct));
    [AllowAnonymous, HttpPost("refresh")]
    public async Task<AccessResponse> Refresh(CancellationToken ct) => Session(await service.RefreshAsync(Request.Cookies[RefreshCookie] ?? "", ct));
    [AllowAnonymous, HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct) { await service.LogoutAsync(Request.Cookies[RefreshCookie] ?? "", ct); Response.Cookies.Delete(RefreshCookie, Cookie()); return NoContent(); }
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct) { await service.LogoutAllAsync(ct); Response.Cookies.Delete(RefreshCookie, Cookie()); return NoContent(); }
    [AllowAnonymous, HttpPost("forgot-password")]
    public async Task<IActionResult> Forgot(EmailRequest r, CancellationToken ct) { await service.ForgotAsync(r, ct); return Accepted(new GenericEmailResponse()); }
    [AllowAnonymous, HttpPost("reset-password")]
    public async Task<IActionResult> Reset(ResetPasswordRequest r, CancellationToken ct) { await service.ResetAsync(r, ct); Response.Cookies.Delete(RefreshCookie, Cookie()); return NoContent(); }
    [HttpPost("change-password")]
    public async Task<IActionResult> Change(ChangePasswordRequest r, CancellationToken ct) { await service.ChangePasswordAsync(r, ct); Response.Cookies.Delete(RefreshCookie, Cookie()); return NoContent(); }
    [HttpGet("me")]
    public Task<CurrentUserDto> Me(CancellationToken ct) => service.GetAsync(ct);
    [HttpPut("me")]
    public Task<CurrentUserDto> Update(NameRequest r, CancellationToken ct) => service.UpdateAsync(r, ct);
    [AllowAnonymous, HttpPost("invitations/accept")]
    public async Task<IActionResult> Accept(AcceptInviteRequest r, CancellationToken ct) { await members.AcceptAsync(r, ct); return NoContent(); }
}
