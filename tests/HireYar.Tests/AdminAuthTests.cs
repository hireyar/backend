using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using HireYar.Application.Auth;
using HireYar.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace HireYar.Tests;

// The test fixture alone replaces visual CAPTCHA with a known synthetic challenge.
public sealed class TestAdminCaptcha : IAdminCaptcha
{
    private readonly ConcurrentDictionary<Guid, bool> challenges = new();
    public AdminCaptchaDto Create() { var id = Guid.NewGuid(); challenges[id] = true; return new(id, "synthetic-image", DateTimeOffset.UtcNow.AddMinutes(2)); }
    public bool Consume(Guid id, string answer) => challenges.TryRemove(id, out _) && answer == "123456";
}
public sealed class AdminCaptchaTests
{
    [Theory]
    [InlineData(false, "", 15, true)]
    [InlineData(true, "short", 15, false)]
    [InlineData(true, "abcdefghijklmnopqrstuvwxyz012345", 15, true)]
    [InlineData(true, "abcdefghijklmnopqrstuvwxyz012345/", 15, false)]
    [InlineData(false, "", 31, false)]
    public void Entry_configuration_requires_a_url_safe_key_and_short_session(bool enabled, string key, int minutes, bool valid)
        => Assert.Equal(valid, new SuperAdminOptions { Enabled = enabled, RandomKey = key, AccessMinutes = minutes }.IsValid());
    [Fact]
    public void Captcha_is_bounded_expires_and_is_consumed_on_failure()
    {
        var clock = new TestClock(); var captcha = new AdminCaptcha(clock); var challenge = captcha.Create();
        Assert.StartsWith("data:image/png;base64,", challenge.ImageDataUrl);
        var png = Convert.FromBase64String(challenge.ImageDataUrl.Split(',')[1]);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.False(captcha.Consume(challenge.Id, "wrong")); Assert.False(captcha.Consume(challenge.Id, "123456"));
        var expired = captcha.Create(); clock.Offset = TimeSpan.FromMinutes(3); Assert.False(captcha.Consume(expired.Id, "123456"));
        for (var i = 0; i < 1024; i++) captcha.Create();
        Assert.Throws<HireYar.Application.Common.AppException>(() => captcha.Create());
        clock.Offset = TimeSpan.FromMinutes(6); captcha.Create();
    }
}
[Collection("Postgres")]
public sealed class AdminAuthTests(ApiFixture f)
{
    private const string GrantRoute = "/api/admin/users/super-admins";
    [PostgresFact]
    public async Task Only_admin_can_grant_existing_owner_and_generated_password_allows_admin_login()
    {
        var admin = await f.RegisterAdmin(); var target = await f.RegisterOwner();
        var request = new AdminGrantRequest(target.Mobile);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Browser().Send(HttpMethod.Post, GrantRoute, request)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await target.Client.Send(HttpMethod.Post, GrantRoute, request)).StatusCode);
        string? beforeStamp = null; Guid beforeOrganization = Guid.Empty;
        await f.Db(async db => { var u = await db.Users.AsNoTracking().SingleAsync(x => x.Id == target.Id); beforeStamp = u.SecurityStamp; beforeOrganization = u.OrganizationId; });
        var response = await admin.Client.Send(HttpMethod.Post, GrantRoute, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var granted = (await response.Content.ReadFromJsonAsync<AdminGrantDto>(BillingJson.Options))!;
        Assert.Equal(target.Id, granted.Id); Assert.Equal(target.Mobile, granted.Mobile);
        Assert.InRange(granted.Password.Length, 12, 128);
        await f.Db(async db => {
            var after = await db.Users.AsNoTracking().SingleAsync(x => x.Id == target.Id);
            Assert.True(after.IsSystemAdmin); Assert.NotEqual(granted.Password, after.PasswordHash);
            Assert.NotEqual(beforeStamp, after.SecurityStamp); Assert.Equal(beforeOrganization, after.OrganizationId);
        });
        var browser = f.Browser(); browser.AdminEntryKey = ApiFixture.AdminEntry;
        var login = await browser.Send(HttpMethod.Post, "/api/admin/auth/login", await Request(browser, target.Mobile, granted.Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        browser.AccessToken = (await login.Content.ReadFromJsonAsync<AdminSessionDto>(BillingJson.Options))!.AccessToken;
        Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Get, "/api/admin/pricing/packages")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Client.Send(HttpMethod.Post, GrantRoute, request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Get, "/api/admin/auth/me")).StatusCode);
    }

    [PostgresFact]
    public async Task Grant_rejects_missing_inactive_unverified_and_invalid_mobile_accounts_without_mutation()
    {
        var admin = await f.RegisterAdmin();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.Send(HttpMethod.Post, GrantRoute, new AdminGrantRequest("09000000000"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.Send(HttpMethod.Post, GrantRoute, new AdminGrantRequest("invalid"))).StatusCode);
        foreach (var inactive in new[] { true, false }) {
            var target = await f.RegisterOwner();
            await f.Db(async db => { var u = await db.Users.SingleAsync(x => x.Id == target.Id); if (inactive) u.IsActive = false; else u.PhoneNumberConfirmed = false; await db.SaveChangesAsync(); });
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.Send(HttpMethod.Post, GrantRoute, new AdminGrantRequest(target.Mobile))).StatusCode);
            await f.Db(async db => { var u = await db.Users.SingleAsync(x => x.Id == target.Id); Assert.False(u.IsSystemAdmin); Assert.Null(u.PasswordHash); });
        }
    }

    [PostgresFact]
    public async Task Concurrent_grants_have_one_winner_and_do_not_reset_its_password()
    {
        var admin = await f.RegisterAdmin(); var target = await f.RegisterOwner();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => admin.Client.Send(HttpMethod.Post, GrantRoute, new AdminGrantRequest(target.Mobile))));
        var success = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var granted = (await success.Content.ReadFromJsonAsync<AdminGrantDto>(BillingJson.Options))!;
        var browser = f.Browser(); browser.AdminEntryKey = ApiFixture.AdminEntry;
        Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Post, "/api/admin/auth/login", await Request(browser, target.Mobile, granted.Password))).StatusCode);
    }
    private async Task<AdminLoginRequest> Request(Browser browser, string mobile, string? password = null)
    {
        var challenge = (await (await browser.Send(HttpMethod.Get, "/api/admin/auth/captcha")).Content.ReadFromJsonAsync<AdminCaptchaDto>(BillingJson.Options))!;
        return new(mobile, password ?? ApiFixture.AdminPassword, challenge.Id, "123456");
    }
    [PostgresFact]
    public async Task Missing_wrong_and_disabled_entry_are_hidden()
    {
        var b = f.Browser(); Assert.Equal(HttpStatusCode.NotFound, (await b.Send(HttpMethod.Get, "/api/admin/auth/entry")).StatusCode);
        b.AdminEntryKey = "wrong"; Assert.Equal(HttpStatusCode.NotFound, (await b.Send(HttpMethod.Get, "/api/admin/auth/captcha")).StatusCode);
        b.AdminEntryKey = ApiFixture.AdminEntry; Assert.Equal(HttpStatusCode.OK, (await b.Send(HttpMethod.Get, "/api/admin/auth/entry")).StatusCode);
        await using var disabled = new ApiFixture { AdminEnabled = false }; await disabled.InitializeAsync();
        var d = disabled.Browser(); d.AdminEntryKey = ApiFixture.AdminEntry;
        Assert.Equal(HttpStatusCode.NotFound, (await d.Send(HttpMethod.Get, "/api/admin/auth/entry")).StatusCode);
        await ((IAsyncLifetime)disabled).DisposeAsync();
    }
    [PostgresFact]
    public async Task Captcha_is_mandatory_single_use_and_unknown_or_non_admin_users_cannot_login()
    {
        var admin = await f.RegisterAdmin(); var b = f.Browser(); b.AdminEntryKey = ApiFixture.AdminEntry;
        var request = await Request(b, admin.Mobile);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Send(HttpMethod.Post, "/api/admin/auth/login", request with { CaptchaAnswer = "654321" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Send(HttpMethod.Post, "/api/admin/auth/login", request)).StatusCode);
        var owner = await f.RegisterOwner();
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Post, "/api/admin/auth/login", await Request(b, owner.Mobile))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Post, "/api/admin/auth/login", await Request(b, "09000000000"))).StatusCode);
        var valid = await Request(b, admin.Mobile);
        Assert.Equal(HttpStatusCode.OK, (await b.Send(HttpMethod.Post, "/api/admin/auth/login", valid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Send(HttpMethod.Post, "/api/admin/auth/login", valid)).StatusCode);
    }
    [PostgresFact]
    public async Task Admin_and_hr_tokens_are_isolated_and_logout_revokes_admin_sessions()
    {
        var admin = await f.RegisterAdmin(); var b = admin.Client;
        Assert.Equal(HttpStatusCode.OK, (await b.Send(HttpMethod.Get, "/api/admin/pricing/packages")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.Send(HttpMethod.Get, "/api/admin/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Get, "/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.Send(HttpMethod.Post, "/api/admin/auth/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Post, GrantRoute, new AdminGrantRequest(admin.Mobile))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Get, "/api/admin/pricing/packages")).StatusCode);
        await f.SignInAsync(b, admin.Mobile);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Get, "/api/admin/pricing/packages")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Post, GrantRoute, new AdminGrantRequest(admin.Mobile))).StatusCode);
    }
    [PostgresFact]
    public async Task Concurrent_bad_passwords_lock_the_account_and_recovery_requires_unlock()
    {
        var admin = await f.RegisterAdmin(); var b = f.Browser(); b.AdminEntryKey = ApiFixture.AdminEntry;
        var requests = new List<AdminLoginRequest>();
        for (var i = 0; i < 5; i++) requests.Add(await Request(b, admin.Mobile, "Wrong-Synthetic-Password-42!"));
        var responses = await Task.WhenAll(requests.Select(r => b.Send(HttpMethod.Post, "/api/admin/auth/login", r)));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Post, "/api/admin/auth/login", await Request(b, admin.Mobile))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.Client.Send(HttpMethod.Get, "/api/admin/pricing/packages")).StatusCode);
        await f.Db(async db => { var u = await db.Users.SingleAsync(x => x.Id == admin.Id); u.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.OK, (await b.Send(HttpMethod.Post, "/api/admin/auth/login", await Request(b, admin.Mobile))).StatusCode);
    }
    [PostgresFact]
    public async Task Password_change_entry_rotation_and_account_disable_invalidate_existing_sessions()
    {
        var admin = await f.RegisterAdmin();
        var options = f.Services.GetRequiredService<IOptions<SuperAdminOptions>>().Value;
        var original = options.RandomKey;
        try
        {
            options.RandomKey = "synthetic-rotated-entry-key-0123456789";
            Assert.Equal(HttpStatusCode.Unauthorized, (await admin.Client.Send(HttpMethod.Get, "/api/admin/pricing/packages")).StatusCode);
        }
        finally { options.RandomKey = original; }
        await using (var scope = f.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AdminAccounts>().ProvisionAsync(admin.Mobile, "Changed-Synthetic-Password-42!", default);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.Client.Send(HttpMethod.Get, "/api/admin/pricing/packages")).StatusCode);
        var another = await f.RegisterAdmin();
        await f.Db(async db => { var u = await db.Users.SingleAsync(x => x.Id == another.Id); u.IsActive = false; await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Unauthorized, (await another.Client.Send(HttpMethod.Get, "/api/admin/pricing/packages")).StatusCode);
    }
    [PostgresFact]
    public async Task Entry_probing_is_rate_limited_in_production()
    {
        await using var limited = new ApiFixture { AdminRateLimit = 1, Production = true }; await limited.InitializeAsync();
        var b = limited.Browser(); b.AdminEntryKey = "wrong";
        Assert.Equal(HttpStatusCode.NotFound, (await b.Send(HttpMethod.Get, "/api/admin/auth/entry")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await b.Send(HttpMethod.Get, "/api/admin/auth/entry")).StatusCode);
        await ((IAsyncLifetime)limited).DisposeAsync();
    }
    [PostgresFact]
    public async Task Expired_or_tampered_tokens_are_rejected_and_login_has_no_refresh_cookie()
    {
        var admin = await f.RegisterAdmin(); var b = f.Browser(); b.AdminEntryKey = ApiFixture.AdminEntry;
        var response = await b.Send(HttpMethod.Post, "/api/admin/auth/login", await Request(b, admin.Mobile));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.False(response.Headers.Contains("Set-Cookie"));
        var original = (await response.Content.ReadFromJsonAsync<AdminSessionDto>(BillingJson.Options))!.AccessToken;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(original);
        Assert.DoesNotContain(jwt.Claims, c => c.Type is "org" or "role");
        var settings = f.Services.GetRequiredService<IOptions<SecurityOptions>>().Value;
        var expired = new JwtSecurityToken(settings.Issuer, settings.Audience + ":super-admin", jwt.Claims, DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddMinutes(-1),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256));
        b.AccessToken = new JwtSecurityTokenHandler().WriteToken(expired);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Get, "/api/admin/pricing/packages")).StatusCode);
        b.AccessToken = original[..^8] + "AAAAAAAA";
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Get, "/api/admin/pricing/packages")).StatusCode);
    }
}
