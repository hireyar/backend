using System.Security.Claims;
using System.Text.Encodings.Web;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace HireYar.Api.Security;

public sealed class InterviewAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, AppDbContext db, TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Interview";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();
        var token = header[7..];
        if (token.Length != 64 || !token.All(char.IsAsciiHexDigit)) return AuthenticateResult.Fail("Invalid interview access.");
        var hash = InterviewTokens.Hash(token);
        var s = await db.InterviewSessions.AsNoTracking().Where(x => x.AccessTokenHash == hash && x.DeletedAtUtc == null)
            .Select(x => new { x.Id, x.AccessTokenHash, x.ExpiresAtUtc, x.AccessRevokedAtUtc }).SingleOrDefaultAsync(Context.RequestAborted);
        if (s is null || s.AccessRevokedAtUtc is not null || s.ExpiresAtUtc <= time.GetUtcNow() || !InterviewTokens.Matches(token, s.AccessTokenHash))
            return AuthenticateResult.Fail("Invalid interview access.");
        var identity = new ClaimsIdentity([new Claim("interview", s.Id.ToString())], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
public sealed class CurrentInterviewAccess(IHttpContextAccessor http) : ICurrentInterviewAccess
{
    public Guid InterviewId => Guid.TryParse(http.HttpContext?.User.FindFirstValue("interview"), out var id) ? id : Guid.Empty;
}
