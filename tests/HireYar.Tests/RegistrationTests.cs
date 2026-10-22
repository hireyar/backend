using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Auth;
using HireYar.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class RegistrationTests : IAsyncLifetime
{
    private readonly ApiFixture f = new();
    public Task InitializeAsync() => f.InitializeAsync();
    public Task DisposeAsync() => ((IAsyncLifetime)f).DisposeAsync();
    private static string Mobile() => "09" + Random.Shared.NextInt64(100_000_000, 1_000_000_000).ToString("D9");
    private async Task<RegisterRequest> Request(Browser browser, string mobile)
    {
        await f.Db(async db => {
            var challenge = await db.OtpChallenges.SingleOrDefaultAsync(x => x.Mobile == mobile);
            if (challenge is not null) { challenge.CreatedAtUtc = f.Clock.GetUtcNow().AddMinutes(-2); await db.SaveChangesAsync(); }
        });
        Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(mobile))).StatusCode);
        return ApiFixture.Registration(mobile, f.PhoneOtp.Codes[mobile]);
    }
    private static async Task<string?> Code(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

    [PostgresFact]
    public async Task Login_does_not_register_and_explicit_registration_persists_profile_referral()
    {
        var b = f.Browser(); var r = await Request(b, Mobile());
        var login = await b.Send(HttpMethod.Post, "/api/auth/otp/verify", new VerifyOtpRequest(r.Mobile, r.Code));
        Assert.Equal(HttpStatusCode.Conflict, login.StatusCode); Assert.Equal("registration_required", await Code(login));
        await f.Db(async db => {
            Assert.False(await db.Users.AnyAsync()); Assert.False(await db.Organizations.AnyAsync());
            Assert.False(await db.RefreshTokens.AnyAsync());
            Assert.Null((await db.OtpChallenges.SingleAsync()).ConsumedAtUtc);
        });
        r = r with { FirstName = "  نام  ", LastName = "  نمونه  ", ReferralCode = "  FUTURE-42  " };
        var signup = await b.Send(HttpMethod.Post, "/api/auth/register", r);
        Assert.Equal(HttpStatusCode.OK, signup.StatusCode);
        Assert.False(string.IsNullOrEmpty(b.RefreshToken));
        var body = await signup.Content.ReadFromJsonAsync<JsonElement>(); Assert.False(body.TryGetProperty("refreshToken", out _));
        b.AccessToken = body.GetProperty("accessToken").GetString();
        var me = (await (await b.Send(HttpMethod.Get, "/api/auth/me")).Content.ReadFromJsonAsync<CurrentUserDto>(TestJson.Options))!;
        Assert.Equal("نام نمونه", me.FullName); Assert.Equal(r.Email, me.Email); Assert.Equal(OrganizationRole.Owner, me.Role);
        await f.Db(async db => {
            var u = await db.Users.SingleAsync(); Assert.Equal(me.Id, u.Id); Assert.True(u.PhoneNumberConfirmed); Assert.False(u.EmailConfirmed);
            Assert.Equal("FUTURE-42", u.ReferralCode); Assert.Equal(r.Email.ToUpperInvariant(), u.NormalizedEmail);
            });
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Post, "/api/auth/register", r)).StatusCode);
        await f.SignInAsync(b, r.Mobile);
        var later = (await (await b.Send(HttpMethod.Get, "/api/auth/me")).Content.ReadFromJsonAsync<CurrentUserDto>(TestJson.Options))!;
        Assert.Equivalent(me, later, strict: true);
        Assert.Equal(HttpStatusCode.OK, (await b.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
    }

    [PostgresFact]
    public async Task Registering_existing_mobile_never_logs_in_or_changes_profile()
    {
        var owner = await f.RegisterOwner(explicitSignup: true); var b = f.Browser(); var r = await Request(b, owner.Mobile);
        var response = await b.Send(HttpMethod.Post, "/api/auth/register", r with { Email = "changed@example.test", FirstName = "Changed", ReferralCode = "OTHER" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Equal("mobile_already_registered", await Code(response));
        Assert.Empty(b.RefreshToken);
        await f.Db(async db => {
            var u = await db.Users.SingleAsync(); Assert.Equal(owner.Id, u.Id); Assert.Equal(r.Email, u.Email); Assert.Equal("نام آزمایشی", u.FullName); Assert.Null(u.ReferralCode);
            Assert.Single(await db.Organizations.ToListAsync()); });
        Assert.Equal(HttpStatusCode.OK, (await b.Send(HttpMethod.Post, "/api/auth/otp/verify", new VerifyOtpRequest(r.Mobile, r.Code))).StatusCode);
    }

    [PostgresFact]
    public async Task Duplicate_normalized_email_rolls_back_and_same_otp_can_register_with_another_email()
    {
        var owner = await f.RegisterOwner(explicitSignup: true); var b = f.Browser(); var r = await Request(b, Mobile());
        var response = await b.Send(HttpMethod.Post, "/api/auth/register", r with { Email = owner.Mobile + "@EXAMPLE.TEST" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Equal("email_unavailable", await Code(response));
        await f.Db(async db => {
            Assert.Single(await db.Users.ToListAsync()); Assert.Single(await db.Organizations.ToListAsync()); Assert.Single(await db.SignupTrials.ToListAsync()); Assert.Null((await db.OtpChallenges.SingleAsync(x => x.Mobile == r.Mobile)).ConsumedAtUtc);
        });
        Assert.Equal(HttpStatusCode.OK, (await b.Send(HttpMethod.Post, "/api/auth/register", r with { ReferralCode = "  " })).StatusCode);
        await f.Db(async db => Assert.Null((await db.Users.SingleAsync(x => x.PhoneNumber == r.Mobile)).ReferralCode));
    }

    [PostgresFact]
    public async Task Required_fields_limits_csrf_and_overposting_are_enforced_before_any_account_write()
    {
        var b = f.Browser(); var r = await Request(b, Mobile());
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Send(HttpMethod.Post, "/api/auth/register", r, csrf: false)).StatusCode);
        object[] invalid = [r with { FirstName = null! }, r with { LastName = "   " }, r with { Email = "bad-email" }, r with { Email = null! },
            r with { FirstName = new string('a', 101) }, r with { FirstName = new string('a', 100), LastName = new string('b', 100) },
            r with { ReferralCode = new string('a', 65) }, r with { Mobile = "09123" }, r with { Code = "123" },
            new { r.Mobile, r.Code, r.Email, r.FirstName, r.LastName, role = "Owner", organizationId = Guid.NewGuid() }];
        foreach (var payload in invalid)
            Assert.Equal(HttpStatusCode.BadRequest, (await b.Send(HttpMethod.Post, "/api/auth/register", payload)).StatusCode);
        await f.Db(async db => { Assert.False(await db.Users.AnyAsync()); Assert.Null((await db.OtpChallenges.SingleAsync()).ConsumedAtUtc); });
        Assert.Equal(HttpStatusCode.OK, (await b.Send(HttpMethod.Post, "/api/auth/register", r with { FirstName = new string('a',100), LastName = new string('b',99), ReferralCode = new string('r',64) })).StatusCode);
    }

    [PostgresFact]
    public async Task Registration_obeys_expiry_attempt_limit_and_shared_otp_lockout()
    {
        var b = f.Browser(); var r = await Request(b, Mobile()); var wrong = r.Code == "000000" ? "111111" : "000000";
        for (var i=0; i<5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Post, "/api/auth/register", r with { Code = wrong })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Post, "/api/auth/otp/verify", new VerifyOtpRequest(r.Mobile,r.Code))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Post, "/api/auth/register", r)).StatusCode);
        var fresh = await Request(b, Mobile()); f.Clock.Offset = TimeSpan.FromMinutes(6);
        try { Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Post, "/api/auth/register", fresh)).StatusCode); }
        finally { f.Clock.Offset = TimeSpan.Zero; }
        await f.Db(async db => Assert.False(await db.Users.AnyAsync()));
    }

    [PostgresFact]
    public async Task Pending_invitation_cannot_be_bypassed_by_explicit_registration()
    {
        var owner = await f.RegisterOwner(); var mobile = Mobile();
        Assert.Equal(HttpStatusCode.Created, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new { mobile, firstName="نام", lastName="عضو", email="invite@example.test" })).StatusCode);
        var b = f.Browser(); var r = await Request(b,mobile);
        var response = await b.Send(HttpMethod.Post, "/api/auth/register",r);
        Assert.Equal(HttpStatusCode.Conflict,response.StatusCode); Assert.Equal("team_invitation_pending",await Code(response));
        await f.Db(async db => { Assert.Single(await db.Users.ToListAsync()); Assert.Null((await db.OtpChallenges.SingleAsync(x=>x.Mobile==mobile)).ConsumedAtUtc); });
    }

    [PostgresFact]
    public async Task Concurrent_registrations_with_same_email_create_only_one_organization_and_grant()
    {
        var first = f.Browser(); var second = f.Browser();
        var a = await Request(first,Mobile()); var b = (await Request(second,Mobile())) with { Email = a.Email.ToUpperInvariant() };
        var results = await Task.WhenAll(first.Send(HttpMethod.Post,"/api/auth/register",a),second.Send(HttpMethod.Post,"/api/auth/register",b));
        Assert.Single(results,x=>x.StatusCode==HttpStatusCode.OK); Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);
        await f.Db(async db=> {
            Assert.Single(await db.Users.ToListAsync()); Assert.Single(await db.Organizations.ToListAsync());
            Assert.Single(await db.RefreshTokens.ToListAsync());
            Assert.Single(await db.SignupTrials.ToListAsync());
            Assert.Single(await db.OtpChallenges.Where(x=>x.ConsumedAtUtc!=null).ToListAsync());
        });
        var loser = results[0].StatusCode == HttpStatusCode.Conflict ? a : b;
        Assert.Equal(HttpStatusCode.OK,(await f.Browser().Send(HttpMethod.Post,"/api/auth/register",loser with { Email=loser.Mobile+"-retry@example.test" })).StatusCode);
    }

    [PostgresFact]
    public async Task Legacy_accounts_remain_readable_migrations_are_idempotent_and_rollback_is_blocked()
    {
        var legacy = await f.RegisterOwner();
        await f.Db(async db => {
            var migrations = db.Database.GetMigrations().ToArray();
            var index = Array.FindIndex(migrations,x=>x.EndsWith("_SeparateRegistrationFromLogin"));
            var migrator = db.GetService<IMigrator>();
            var sql = migrator.GenerateScript(migrations[index-1],migrations[index],MigrationsSqlGenerationOptions.Idempotent);
            await db.Database.ExecuteSqlRawAsync(sql); await db.Database.ExecuteSqlRawAsync(sql);
            Assert.False(db.Database.HasPendingModelChanges());
            var u = await db.Users.AsNoTracking().SingleAsync(x=>x.Id==legacy.Id); Assert.Null(u.ReferralCode); Assert.Null(u.Email);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"AspNetUsers\" SET \"ReferralCode\" = {"FUTURE"} WHERE \"Id\" = {legacy.Id}");
            var error = await Assert.ThrowsAsync<PostgresException>(()=>migrator.MigrateAsync(migrations[index-1]));
            Assert.Equal("P0001",error.SqlState);
        });
    }
}
