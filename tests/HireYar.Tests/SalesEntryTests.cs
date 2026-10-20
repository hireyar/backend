using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Sales;
using HireYar.Infrastructure.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace HireYar.Tests;
public sealed class SalesEntryOptionTests
{
    [Theory]
    [InlineData(false,"",15,true)]
    [InlineData(true,"short",15,false)]
    [InlineData(true,"synthetic-sales-entry-key-0123456789",15,true)]
    [InlineData(true,"synthetic-sales-entry-key-0123456789/",15,false)]
    [InlineData(false,"",31,false)]
    public void Entry_options_are_validated(bool enabled,string key,int minutes,bool valid)=>Assert.Equal(valid,new SalesAdminOptions {Enabled=enabled,RandomKey=key,AccessMinutes=minutes}.IsValid());
}
[Collection("Postgres")]
public sealed class SalesEntryTests : IAsyncLifetime
{
    private readonly ApiFixture f=new();
    public Task InitializeAsync()=>f.InitializeAsync();
    public Task DisposeAsync()=>((IAsyncLifetime)f).DisposeAsync();
    [PostgresFact]
    public async Task Sales_entry_is_private_without_captcha_and_rotation_revokes_all_sales_sessions()
    {
        var admin=await f.RegisterAdmin();var mobile="09"+Random.Shared.NextInt64(100_000_000,1_000_000_000);
        var response=await admin.Client.Send(HttpMethod.Post,"/api/admin/sales/agents",new SalesAgentRequest("Synthetic sales",mobile));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var agent=(await response.Content.ReadFromJsonAsync<SalesCredentialsDto>(TestJson.Options))!;
        var b=f.Browser();var request=new SalesLoginRequest(mobile,agent.Password);
        Assert.Equal(HttpStatusCode.NotFound,(await b.Send(HttpMethod.Get,"/api/sales/auth/entry")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await b.Send(HttpMethod.Post,"/api/sales/auth/login",request)).StatusCode);
        b.SalesEntryKey=ApiFixture.AdminEntry;Assert.Equal(HttpStatusCode.NotFound,(await b.Send(HttpMethod.Get,"/api/sales/auth/entry")).StatusCode);
        b.SalesEntryKey=ApiFixture.SalesEntry;Assert.Equal(HttpStatusCode.OK,(await b.Send(HttpMethod.Get,"/api/sales/auth/entry")).StatusCode);
        var doc=(await (await b.Send(HttpMethod.Get,"/openapi/v1.json")).Content.ReadFromJsonAsync<JsonElement>())!;
        Assert.False(doc.GetProperty("paths").TryGetProperty("/api/sales/auth/captcha",out _));
        response=await b.Send(HttpMethod.Post,"/api/sales/auth/login",request);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        b.AccessToken=(await response.Content.ReadFromJsonAsync<SalesSessionDto>(TestJson.Options))!.AccessToken;
        Assert.Equal(HttpStatusCode.OK,(await b.Send(HttpMethod.Get,"/api/sales/auth/me")).StatusCode);
        var options=f.Services.GetRequiredService<IOptions<SalesAdminOptions>>().Value;options.RandomKey="synthetic-sales-rotated-entry-key-0123456789";
        Assert.Equal(HttpStatusCode.Unauthorized,(await b.Send(HttpMethod.Get,"/api/sales/leads")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await b.Send(HttpMethod.Post,"/api/sales/auth/login",request)).StatusCode);
        b.SalesEntryKey=options.RandomKey;response=await b.Send(HttpMethod.Post,"/api/sales/auth/login",request);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        b.AccessToken=(await response.Content.ReadFromJsonAsync<SalesSessionDto>(TestJson.Options))!.AccessToken;
        options.Enabled=false;Assert.Equal(HttpStatusCode.NotFound,(await b.Send(HttpMethod.Get,"/api/sales/auth/entry")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await b.Send(HttpMethod.Get,"/api/sales/leads")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await admin.Client.Send(HttpMethod.Get,"/api/admin/auth/me")).StatusCode);
    }
    [PostgresFact]
    public async Task Hidden_sales_entry_is_rate_limited_independently()
    {
        await using var limited=new ApiFixture {SalesRateLimit=2};await limited.InitializeAsync();var b=limited.Browser();
        Assert.Equal(HttpStatusCode.NotFound,(await b.Send(HttpMethod.Get,"/api/sales/auth/entry")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await b.Send(HttpMethod.Get,"/api/sales/auth/entry")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await b.Send(HttpMethod.Get,"/api/sales/auth/entry")).StatusCode);
        await ((IAsyncLifetime)limited).DisposeAsync();
    }
}
