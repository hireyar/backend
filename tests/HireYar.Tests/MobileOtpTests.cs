using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Auth;
using HireYar.Application.Organizations;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class MobileOtpTests(ApiFixture fixture)
{
    [PostgresFact]
    public async Task Fixed_otp_mode_skips_sms_and_accepts_configured_code_once()
    {
        var test = new ApiFixture { UseFixedOtp = true };
        await test.InitializeAsync();
        try
        {
            var browser = test.Browser();
            var mobile = "09" + Random.Shared.NextInt64(100_000_000, 1_000_000_000).ToString("D9");
            Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(mobile))).StatusCode);
            Assert.False(test.PhoneOtp.Codes.ContainsKey(mobile));
            Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Send(HttpMethod.Post, "/api/auth/otp/verify", new VerifyOtpRequest(mobile, "654321"))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Post, "/api/auth/otp/verify", new VerifyOtpRequest(mobile, "123456"))).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Send(HttpMethod.Post, "/api/auth/otp/verify", new VerifyOtpRequest(mobile, "123456"))).StatusCode);
        }
        finally { await ((IAsyncLifetime)test).DisposeAsync(); }
    }

    [PostgresFact]
    public async Task Verified_mobile_creates_owner_then_logs_into_same_account()
    {
        var browser = fixture.Browser();
        var mobile = "09" + Random.Shared.NextInt64(100_000_000, 1_000_000_000).ToString("D9");
        var request = new RequestOtpRequest(mobile);
        Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Post, "/api/auth/otp/request", request)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await browser.Send(HttpMethod.Post, "/api/auth/otp/request", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Send(HttpMethod.Post, "/api/auth/otp/verify", new VerifyOtpRequest(mobile, "000000" == fixture.PhoneOtp.Codes[mobile] ? "000001" : "000000"))).StatusCode);
        var verify = new VerifyOtpRequest(mobile, fixture.PhoneOtp.Codes[mobile]);
        var first = await browser.Send(HttpMethod.Post, "/api/auth/otp/verify", verify);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        browser.AccessToken = (await first.Content.ReadFromJsonAsync<AccessResponse>())!.AccessToken;
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Send(HttpMethod.Post, "/api/auth/otp/verify", verify)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Put, "/api/organization", new OrganizationRequest("Acme"))).StatusCode);
        Guid firstId = default;
        await fixture.Db(async db => { var u = await db.Users.SingleAsync(x => x.PhoneNumber == mobile); firstId = u.Id; Assert.True(u.PhoneNumberConfirmed); Assert.True(await db.Organizations.AnyAsync(x => x.Id == u.OrganizationId && x.Name == "Acme")); });
        fixture.Clock.Offset = TimeSpan.FromMinutes(2);
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Post, "/api/auth/otp/request", request)).StatusCode);
            var second = await browser.Send(HttpMethod.Post, "/api/auth/otp/verify", new VerifyOtpRequest(mobile, fixture.PhoneOtp.Codes[mobile]));
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            await fixture.Db(async db => Assert.Equal(firstId, (await db.Users.SingleAsync(x => x.PhoneNumber == mobile)).Id));
        }
        finally { fixture.Clock.Offset = TimeSpan.Zero; }
    }
}
