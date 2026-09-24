using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Auth;
using HireYar.Application.Members;
using HireYar.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Tests;

[Collection("Sql")]
public sealed class AuthTests(ApiFixture f)
{
    [SqlFact]
    public async Task Register_is_atomic_and_duplicate_email_is_rejected()
    {
        var c = f.Browser(); var email = $"{Guid.NewGuid():N}@example.test"; var r = new RegisterRequest("Name", "Org", email, "invalidpassword");
        var before = 0; await f.Db(async db => before = await db.Organizations.CountAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/api/auth/register", r)).StatusCode);
        await f.Db(async db => { Assert.Equal(before, await db.Organizations.CountAsync()); Assert.False(await db.Users.AnyAsync(x => x.Email == email)); });
        r = r with { Password = "Valid-test-password1!" };
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/api/auth/register", r)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, "/api/auth/register", r with { Email = email.ToUpperInvariant() })).StatusCode);
        await f.Db(async db => { var u = await db.Users.SingleAsync(x => x.Email == email); Assert.Equal(OrganizationRole.Owner, u.Role); Assert.True(await db.Organizations.AnyAsync(x => x.Id == u.OrganizationId)); });
    }
    [SqlFact]
    public async Task Confirmation_is_required_and_single_use()
    {
        var c = f.Browser(); var email = $"{Guid.NewGuid():N}@example.test"; const string password = "Valid-test-password1!";
        var response = await c.Send(HttpMethod.Post, "/api/auth/register", new RegisterRequest("Name", "Org", email, password));
        var r = (await response.Content.ReadFromJsonAsync<RegistrationResult>())!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.Send(HttpMethod.Post, "/api/auth/login", new LoginRequest(email, password))).StatusCode);
        var token = new TokenRequest(r.UserId, f.Email.Token(email));
        Assert.Equal(HttpStatusCode.NoContent, (await c.Send(HttpMethod.Post, "/api/auth/confirm-email", token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/api/auth/confirm-email", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/api/auth/login", new LoginRequest(email, password))).StatusCode);
    }
    [SqlFact]
    public async Task Email_failure_leaves_complete_account_and_resend_can_recover()
    {
        var c = f.Browser(); var email = $"{Guid.NewGuid():N}@example.test"; f.Email.Fail = true;
        try { var response = await c.Send(HttpMethod.Post, "/api/auth/register", new RegisterRequest("Name", "Org", email, "Valid-test-password1!")); Assert.False((await response.Content.ReadFromJsonAsync<RegistrationResult>())!.ConfirmationEmailSent); }
        finally { f.Email.Fail = false; }
        Assert.Equal(HttpStatusCode.Accepted, (await c.Send(HttpMethod.Post, "/api/auth/resend-confirmation", new EmailRequest(email))).StatusCode); Assert.True(f.Email.Messages.ContainsKey(email));
        Assert.Equal(HttpStatusCode.Accepted, (await c.Send(HttpMethod.Post, "/api/auth/resend-confirmation", new EmailRequest("missing@example.test"))).StatusCode);
    }
    [SqlFact]
    public async Task Wrong_password_lockout_inactive_and_invalid_access_are_rejected()
    {
        var a = await f.RegisterConfirmed(); var c = f.Browser();
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await c.Send(HttpMethod.Post, "/api/auth/login", new LoginRequest(a.Email, "Wrong-password1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.Send(HttpMethod.Post, "/api/auth/login", new LoginRequest(a.Email, a.Password))).StatusCode);
        await f.Db(async db => { var u = await db.Users.FindAsync(a.Id); Assert.True(u!.LockoutEnd > DateTimeOffset.UtcNow); u.LockoutEnd = null; u.IsActive = false; await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Get, "/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.Send(HttpMethod.Post, "/api/auth/login", new LoginRequest(a.Email, a.Password))).StatusCode);
        c.AccessToken = "invalid.token.signature"; Assert.Equal(HttpStatusCode.Unauthorized, (await c.Send(HttpMethod.Get, "/api/auth/me")).StatusCode);
    }
    [SqlFact]
    public async Task Expired_access_token_is_rejected()
    {
        var a = await f.RegisterConfirmed(); f.Clock.Offset = TimeSpan.FromHours(-1);
        try { var response = await a.Client.Send(HttpMethod.Post, "/api/auth/login", new LoginRequest(a.Email, a.Password)); a.Client.AccessToken = (await response.Content.ReadFromJsonAsync<AccessResponse>())!.AccessToken; }
        finally { f.Clock.Offset = TimeSpan.Zero; }
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Get, "/api/auth/me")).StatusCode);
    }
    [SqlFact]
    public async Task Refresh_rotation_replay_revokes_only_its_session_family()
    {
        var a = await f.RegisterConfirmed(); var old = a.Client.RefreshToken; var other = f.Browser();
        Assert.Equal(HttpStatusCode.OK, (await other.Send(HttpMethod.Post, "/api/auth/login", new LoginRequest(a.Email, a.Password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode); var next = a.Client.RefreshToken; Assert.NotEqual(old, next);
        a.Client.SetRefresh(old); Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
        a.Client.SetRefresh(next); Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
    }
    [SqlFact]
    public async Task Concurrent_refresh_has_one_successor_and_replay_revokes_it()
    {
        var a = await f.RegisterConfirmed(); var first = f.Browser(); var second = f.Browser(); first.SetRefresh(a.Client.RefreshToken); second.SetRefresh(a.Client.RefreshToken);
        var results = await Task.WhenAll(first.Send(HttpMethod.Post, "/api/auth/refresh"), second.Send(HttpMethod.Post, "/api/auth/refresh"));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Unauthorized);
        await f.Db(async db => { Assert.Equal(2, await db.RefreshTokens.CountAsync(x => x.UserId == a.Id)); Assert.False(await db.RefreshTokens.AnyAsync(x => x.UserId == a.Id && x.RevokedAtUtc == null)); });
    }
    [SqlFact]
    public async Task Logout_and_logout_all_revoke_refresh_but_access_remains_until_expiry()
    {
        var a = await f.RegisterConfirmed(); var old = a.Client.RefreshToken;
        Assert.Equal(HttpStatusCode.NoContent, (await a.Client.Send(HttpMethod.Post, "/api/auth/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Get, "/api/auth/me")).StatusCode);
        a.Client.SetRefresh(old); Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
        var c = f.Browser(); await c.Send(HttpMethod.Post, "/api/auth/login", new LoginRequest(a.Email, a.Password));
        Assert.Equal(HttpStatusCode.NoContent, (await a.Client.Send(HttpMethod.Post, "/api/auth/logout-all")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
    }
    [SqlFact]
    public async Task Reset_is_single_use_and_revokes_existing_sessions()
    {
        var a = await f.RegisterConfirmed(); var c = f.Browser();
        Assert.Equal(HttpStatusCode.Accepted, (await c.Send(HttpMethod.Post, "/api/auth/forgot-password", new EmailRequest(a.Email))).StatusCode);
        var reset = new ResetPasswordRequest(a.Id, f.Email.Token(a.Email), "Different-password2!");
        Assert.Equal(HttpStatusCode.NoContent, (await c.Send(HttpMethod.Post, "/api/auth/reset-password", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/api/auth/reset-password", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/api/auth/login", new LoginRequest(a.Email, reset.Password))).StatusCode);
    }
    [SqlFact]
    public async Task Change_password_revokes_sessions_and_profile_cannot_overpost()
    {
        var a = await f.RegisterConfirmed(); var old = a.Client.RefreshToken;
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Put, "/api/auth/me", new { fullName = "Changed", role = "Owner", organizationId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Put, "/api/auth/me", new NameRequest("Changed"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.Client.Send(HttpMethod.Post, "/api/auth/change-password", new ChangePasswordRequest(a.Password, "Different-password2!"))).StatusCode);
        a.Client.SetRefresh(old); Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
    }
    [SqlFact]
    public async Task Cookie_csrf_and_cors_contracts_are_enforced()
    {
        var a = await f.RegisterConfirmed(); var c = f.Browser();
        var response = await c.Send(HttpMethod.Post, "/api/auth/login", new LoginRequest(a.Email, a.Password));
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), x => x.StartsWith("hireyar.refresh=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie.ToLowerInvariant()); Assert.Contains("path=/api/auth", cookie.ToLowerInvariant()); Assert.Contains("samesite=strict", cookie.ToLowerInvariant()); Assert.DoesNotContain("domain=", cookie.ToLowerInvariant());
        Assert.DoesNotContain("refreshToken", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/api/auth/refresh", csrf: false)).StatusCode);
        var allowed = await c.Send(HttpMethod.Get, "/api/auth/csrf", origin: "http://localhost:4200"); Assert.Equal("http://localhost:4200", Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
        var denied = await c.Send(HttpMethod.Get, "/api/auth/csrf", origin: "https://untrusted.example"); Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
        var logout = await c.Send(HttpMethod.Post, "/api/auth/logout"); Assert.Contains(logout.Headers.GetValues("Set-Cookie"), x => x.StartsWith("hireyar.refresh=", StringComparison.Ordinal) && x.Contains("path=/api/auth", StringComparison.OrdinalIgnoreCase));
    }
    [SqlFact]
    public async Task Openapi_and_health_are_available_and_panel_is_protected()
    {
        var c = f.Browser(); Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Get, "/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.Send(HttpMethod.Get, "/api/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.MovedPermanently, (await c.Send(HttpMethod.Get, "/swagger")).StatusCode);
        var ui = await c.Send(HttpMethod.Get, "/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        Assert.Contains("Swagger UI", await ui.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Get, "/swagger/swagger-ui.css")).StatusCode);
        var response = await c.Send(HttpMethod.Get, "/openapi/v1.json"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.True(doc.GetProperty("paths").TryGetProperty("/api/auth/refresh", out _));
    }
}


