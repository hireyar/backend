using HireYar.Api.Security;
using HireYar.Application.Hiring;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/candidate-interview"), AllowAnonymous, ServiceFilter(typeof(CsrfFilter)), EnableRateLimiting("auth")]
public sealed class CandidateInterviewController(CandidateInterviewService service, IAntiforgery antiforgery, IWebHostEnvironment environment) : ControllerBase
{
    private const string CookieName = "hireyar.candidate";
    private string? Credential => Request.Cookies[CookieName];
    [HttpGet("csrf")] public object Csrf() => new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken };
    [HttpPost("preview")] public Task<PublicSessionDto> Preview(InvitationTokenRequest r, CancellationToken ct) => service.PreviewAsync(r.Token, ct);
    [HttpPost("exchange")]
    public async Task<PublicSessionDto> Exchange(InvitationTokenRequest r, CancellationToken ct)
    {
        var result = await service.ExchangeAsync(r.Token, Credential, ct);
        if (result.Credential is not null) Response.Cookies.Append(CookieName, result.Credential, new CookieOptions { HttpOnly = true, Secure = !environment.IsDevelopment() || Request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/api/candidate-interview", Expires = result.ExpiresAtUtc, IsEssential = true });
        return result.Session;
    }
    [HttpGet("session")] public Task<PublicSessionDto> Session(CancellationToken ct) => service.GetAsync(Credential, ct);
    [HttpPost("start")] public Task<PublicSessionDto> Start(ConsentRequest r, CancellationToken ct) => service.StartAsync(Credential, r, ct);
    [HttpPut("answers/{questionId:guid}")] public Task<PublicSessionDto> Answer(Guid questionId, AnswerRequest r, CancellationToken ct) => service.AnswerAsync(Credential, questionId, r, ct);
    [HttpPost("complete")] public Task<PublicSessionDto> Complete(CancellationToken ct) => service.CompleteAsync(Credential, ct);
}
