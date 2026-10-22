using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Auth;
using HireYar.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class AuthTests(ApiFixture f)
{

    [PostgresFact]
    public async Task Expired_access_token_is_rejected()
    {
        var a = await f.RegisterOwner(); f.Clock.Offset = TimeSpan.FromHours(-1);
        try { await f.SignInAsync(a.Client, a.Mobile); }
        finally { f.Clock.Offset = TimeSpan.Zero; }
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Get, "/api/auth/me")).StatusCode);
    }
    [PostgresFact]
    public async Task Refresh_rotation_replay_revokes_only_its_session_family()
    {
        var a = await f.RegisterOwner(); var old = a.Client.RefreshToken; var other = f.Browser();
        Assert.Equal(HttpStatusCode.OK, (await f.SignInAsync(other, a.Mobile)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode); var next = a.Client.RefreshToken; Assert.NotEqual(old, next);
        a.Client.SetRefresh(old); Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
        a.Client.SetRefresh(next); Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
    }
    [PostgresFact]
    public async Task Concurrent_refresh_has_one_successor_and_replay_revokes_it()
    {
        var a = await f.RegisterOwner(); var first = f.Browser(); var second = f.Browser(); first.SetRefresh(a.Client.RefreshToken); second.SetRefresh(a.Client.RefreshToken);
        var results = await Task.WhenAll(first.Send(HttpMethod.Post, "/api/auth/refresh"), second.Send(HttpMethod.Post, "/api/auth/refresh"));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Unauthorized);
        await f.Db(async db => { Assert.Equal(2, await db.RefreshTokens.CountAsync(x => x.UserId == a.Id)); Assert.False(await db.RefreshTokens.AnyAsync(x => x.UserId == a.Id && x.RevokedAtUtc == null)); });
    }
    [PostgresFact]
    public async Task Logout_and_logout_all_revoke_refresh_but_access_remains_until_expiry()
    {
        var a = await f.RegisterOwner(); var old = a.Client.RefreshToken;
        Assert.Equal(HttpStatusCode.NoContent, (await a.Client.Send(HttpMethod.Post, "/api/auth/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Get, "/api/auth/me")).StatusCode);
        a.Client.SetRefresh(old); Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
        var c = f.Browser(); await f.SignInAsync(c, a.Mobile);
        Assert.Equal(HttpStatusCode.NoContent, (await a.Client.Send(HttpMethod.Post, "/api/auth/logout-all")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
    }

    [PostgresFact]
    public async Task Profile_cannot_overpost_and_logout_all_revokes_sessions()
    {
        var a = await f.RegisterOwner(); var old = a.Client.RefreshToken;
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Put, "/api/auth/me", new { fullName = "Changed", role = "Owner", organizationId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Put, "/api/auth/me", new NameRequest("Changed"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.Client.Send(HttpMethod.Post, "/api/auth/logout-all")).StatusCode);
        a.Client.SetRefresh(old); Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
    }
    [PostgresFact]
    public async Task Account_settings_save_organization_name_and_preserve_tenant_scope()
    {
        var a = await f.RegisterOwner(); var other = await f.RegisterOwner();
        var before = (await (await other.Client.Send(HttpMethod.Get, "/api/auth/me")).Content.ReadFromJsonAsync<CurrentUserDto>(TestJson.Options))!;
        var response = await a.Client.Send(HttpMethod.Put, "/api/auth/me", new NameRequest(" نام کاربر ", " شرکت نمونه "));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<CurrentUserDto>(TestJson.Options))!;
        Assert.Equal("نام کاربر", saved.FullName); Assert.Equal("شرکت نمونه", saved.OrganizationName);
        await f.Db(async db => {
            Assert.Equal("شرکت نمونه", (await db.Organizations.SingleAsync(x => x.Id == saved.OrganizationId)).Name);
            Assert.Equal("نام کاربر", (await db.Users.SingleAsync(x => x.Id == a.Id)).FullName);
        });
        var org = await (await a.Client.Send(HttpMethod.Get, "/api/organization")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("شرکت نمونه", org.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Put, "/api/auth/me", new NameRequest("نام تازه"))).StatusCode);
        var reloaded = (await (await a.Client.Send(HttpMethod.Get, "/api/auth/me")).Content.ReadFromJsonAsync<CurrentUserDto>(TestJson.Options))!;
        Assert.Equal("نام تازه", reloaded.FullName); Assert.Equal("شرکت نمونه", reloaded.OrganizationName);
        var unchanged = (await (await other.Client.Send(HttpMethod.Get, "/api/auth/me")).Content.ReadFromJsonAsync<CurrentUserDto>(TestJson.Options))!;
        Assert.Equivalent(before, unchanged, strict: true);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Put, "/api/auth/me",
            new { fullName = "Changed", organizationName = "Wrong", organizationId = before.OrganizationId })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Browser().Send(HttpMethod.Put, "/api/auth/me", new NameRequest("Name", "Company"))).StatusCode);
    }

    [PostgresFact]
    public async Task Invalid_organization_name_does_not_partially_save_account_settings()
    {
        var a = await f.RegisterOwner();
        var before = (await (await a.Client.Send(HttpMethod.Get, "/api/auth/me")).Content.ReadFromJsonAsync<CurrentUserDto>(TestJson.Options))!;
        foreach (var invalid in new[] { "", "   ", "\t\n", new string('x', 201) })
            Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Put, "/api/auth/me", new NameRequest("Changed", invalid))).StatusCode);
        var after = (await (await a.Client.Send(HttpMethod.Get, "/api/auth/me")).Content.ReadFromJsonAsync<CurrentUserDto>(TestJson.Options))!;
        Assert.Equivalent(before, after, strict: true);
        Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Put, "/api/auth/me", new NameRequest("Name", new string('x', 200)))).StatusCode);
    }
    [PostgresFact]
    public async Task Cookie_csrf_and_cors_contracts_are_enforced()
    {
        var a = await f.RegisterOwner(); var c = f.Browser();
        var response = await f.SignInAsync(c, a.Mobile);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), x => x.StartsWith("hireyar.refresh=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie.ToLowerInvariant()); Assert.Contains("path=/api/auth", cookie.ToLowerInvariant()); Assert.Contains("samesite=strict", cookie.ToLowerInvariant()); Assert.DoesNotContain("domain=", cookie.ToLowerInvariant());
        Assert.DoesNotContain("refreshToken", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/api/auth/refresh", csrf: false)).StatusCode);
        var allowed = await c.Send(HttpMethod.Get, "/api/auth/csrf", origin: "http://localhost:4200"); Assert.Equal("http://localhost:4200", Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
        var denied = await c.Send(HttpMethod.Get, "/api/auth/csrf", origin: "https://untrusted.example"); Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
        var logout = await c.Send(HttpMethod.Post, "/api/auth/logout"); Assert.Contains(logout.Headers.GetValues("Set-Cookie"), x => x.StartsWith("hireyar.refresh=", StringComparison.Ordinal) && x.Contains("path=/api/auth", StringComparison.OrdinalIgnoreCase));
    }
    [PostgresFact]
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
        var grant = doc.GetProperty("paths").GetProperty("/api/admin/users/super-admins").GetProperty("post");
        Assert.True(grant.GetProperty("security")[0].TryGetProperty("SuperAdmin", out _));
    }
}
