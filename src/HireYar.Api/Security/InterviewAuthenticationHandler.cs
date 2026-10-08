using System.Security.Claims;
using System.Text.Encodings.Web;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using HireYar.Domain.Interviews;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Security;

public sealed class InterviewAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, AppDbContext db, TimeProvider time, IInterviewInvitationSecurity invitationSecurity)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Interview";
    private InterviewAccessStatus? failure;
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();
        var token = header[7..];
        if (!(token.Length == 6 && token.All(char.IsAsciiDigit)) && !(token.Length == 64 && token.All(char.IsAsciiHexDigit)))
            return AuthenticateResult.Fail("Invalid interview access.");
        if (!Guid.TryParse(Request.RouteValues["interviewId"]?.ToString(), out var id)) return AuthenticateResult.Fail("Invalid interview access.");
        var s = await db.InterviewSessions.AsNoTracking().Where(x => x.Id == id && x.DeletedAtUtc == null)
            .Select(x => new { x.Id, x.OrganizationId, x.ProtectedAccessCode, x.AccessTokenHash, x.ExpiresAtUtc, x.AccessRevokedAtUtc,
                x.SupersededByInterviewId, x.Status, x.Stage, x.AccessLockedUntilUtc }).SingleOrDefaultAsync(Context.RequestAborted);
        if (s is null) return AuthenticateResult.Fail("Invalid interview access.");
        failure = InterviewInvitationStatus.Get(s.SupersededByInterviewId, s.AccessRevokedAtUtc, s.Status, s.ExpiresAtUtc, time.GetUtcNow());
        // The final API response already carries company/goodbye stages. Only their acknowledgement/audio can finish playing.
        var closing = s.Status == InterviewStatus.Completed && s.Stage != InterviewStage.Finished && s.SupersededByInterviewId is null && s.AccessRevokedAtUtc is null &&
            s.ExpiresAtUtc > time.GetUtcNow() && (Request.Path.Value!.EndsWith("/stage/advance", StringComparison.Ordinal) || Request.Path.Value.Contains("/stages/", StringComparison.Ordinal));
        if (failure.Status != "available" && !closing) return AuthenticateResult.Fail("Interview access is no longer available.");
        failure = null;
        if (s.AccessLockedUntilUtc > time.GetUtcNow()) { failure = Locked(); return AuthenticateResult.Fail("Interview access is temporarily locked."); }
        var matches = s.ProtectedAccessCode is null ? InterviewTokens.Matches(token, s.AccessTokenHash) : invitationSecurity.Matches(s.Id, token, s.ProtectedAccessCode);
        if (!matches)
        {
            if (s.ProtectedAccessCode is not null)
            {
                await db.WithOrganizationBillingLockAsync(s.OrganizationId, async () => {
                    var current = await db.InterviewSessions.IgnoreAutoIncludes().SingleAsync(x => x.Id == id && x.OrganizationId == s.OrganizationId, Context.RequestAborted);
                    if (current.SupersededByInterviewId is null && current.DeletedAtUtc is null)
                    {
                        current.RecordAccessFailure(time.GetUtcNow());
                        if (current.AccessLockedUntilUtc > time.GetUtcNow()) failure = Locked();
                    }
                    return true;
                }, Context.RequestAborted);
            }
            return AuthenticateResult.Fail("Invalid interview access.");
        }
        var identity = new ClaimsIdentity([new Claim("interview", s.Id.ToString()), new Claim("interview_organization", s.OrganizationId.ToString())], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
    private static InterviewAccessStatus Locked() => new("locked", "interview_access_locked", "تعداد تلاش‌های ناموفق زیاد است. پانزده دقیقه بعد دوباره تلاش کنید.");
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var status = failure?.Status switch { "locked" => 429, "completed" or "changed" or "expired" => 410, _ => 401 };
        Response.StatusCode = status; Response.Headers.CacheControl = "no-store";
        if (status == 429) Response.Headers.RetryAfter = "900";
        await Response.WriteAsJsonAsync(new ProblemDetails { Status = status,
            Title = failure?.Message ?? "کد دسترسی مصاحبه معتبر نیست.",
            Extensions = { ["code"] = failure?.Code ?? "invalid_interview_access", ["traceId"] = Context.TraceIdentifier } }, Context.RequestAborted);
    }
}
public sealed class CurrentInterviewAccess(IHttpContextAccessor http) : ICurrentInterviewAccess
{
    public Guid InterviewId => Guid.TryParse(http.HttpContext?.User.FindFirstValue("interview"), out var id) ? id : Guid.Empty;
}
