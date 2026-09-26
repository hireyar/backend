using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Auth;
using HireYar.Domain.InterviewTemplates;
using HireYar.Domain.JobPositions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class SecurityTests(ApiFixture f)
{
    private sealed class ProxyRequestStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, continuation) =>
            {
                context.Connection.RemoteIpAddress = context.Request.Headers.TryGetValue("X-Test-Remote-IP", out var address)
                    ? IPAddress.Parse(address.ToString()) : IPAddress.Loopback;
                return continuation(context);
            });
            next(app);
        };
    }

    [Fact]
    public async Task Production_csrf_uses_https_forwarded_by_trusted_proxy()
    {
        using var production = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Default", "Host=localhost;Database=hireyar_test;Username=test;Password=test");
            builder.UseSetting("Security:SigningKey", new string('x', 32));
            builder.UseSetting("Security:Issuer", "HireYar.Tests");
            builder.UseSetting("Security:Audience", "HireYar.Tests");
            builder.UseSetting("Security:FrontendUrl", "https://localhost:4200");
            builder.UseSetting("Security:AllowedOrigins:0", "https://localhost:4200");
            builder.UseSetting("Security:DataProtectionPath", Path.Combine(Path.GetTempPath(), "hireyar-csrf-" + Guid.NewGuid().ToString("N")));
            builder.UseSetting("ReverseProxy:KnownProxies:0", "127.0.0.1");
            builder.UseSetting("Smtp:Host", "localhost");
            builder.UseSetting("Smtp:Port", "1025");
            builder.UseSetting("Smtp:From", "no-reply@hireyar.test");
            builder.ConfigureServices(services => services.AddTransient<IStartupFilter, ProxyRequestStartupFilter>());
        });
        using var client = production.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("hireyar.csrf=", StringComparison.Ordinal) && cookie.Contains("secure", StringComparison.OrdinalIgnoreCase));

        using var untrustedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        untrustedRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        untrustedRequest.Headers.TryAddWithoutValidation("X-Test-Remote-IP", "192.0.2.10");
        using var untrustedResponse = await client.SendAsync(untrustedRequest);
        Assert.Equal(HttpStatusCode.InternalServerError, untrustedResponse.StatusCode);
    }

    [Fact]
    public async Task Production_serves_swagger_anonymously_but_protects_account_api()
    {
        using var production = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Default", "Host=localhost;Database=hireyar_test;Username=test;Password=test");
            builder.UseSetting("Security:SigningKey", new string('x', 32));
            builder.UseSetting("Security:Issuer", "HireYar.Tests");
            builder.UseSetting("Security:Audience", "HireYar.Tests");
            builder.UseSetting("Security:FrontendUrl", "https://localhost:4200");
            builder.UseSetting("Security:AllowedOrigins:0", "https://localhost:4200");
            builder.UseSetting("Security:DataProtectionPath", Path.Combine(Path.GetTempPath(), "hireyar-swagger-" + Guid.NewGuid().ToString("N")));
            builder.UseSetting("Smtp:Host", "localhost");
            builder.UseSetting("Smtp:Port", "1025");
            builder.UseSetting("Smtp:From", "no-reply@hireyar.test");
        });
        using var client = production.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        var openApiResponse = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
        var openApi = await openApiResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("شروع با حساب جدید", openApi.GetProperty("info").GetProperty("description").GetString());
        Assert.Contains("ثبت‌نام", openApi.GetProperty("paths").GetProperty("/api/auth/register").GetProperty("post").GetProperty("summary").GetString());
        foreach (var path in openApi.GetProperty("paths").EnumerateObject())
        {
            if (path.Name == "/health") continue;
            foreach (var method in path.Value.EnumerateObject())
                Assert.False(string.IsNullOrWhiteSpace(method.Value.GetProperty("summary").GetString()), $"Missing Persian summary: {method.Name} {path.Name}");
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
    [PostgresFact]
    public async Task Concurrent_registration_enforces_database_email_uniqueness()
    {
        var email = $"{Guid.NewGuid():N}@example.test"; var request = new RegisterRequest("Name", "Org", email, "Valid-test-password1!");
        var results = await Task.WhenAll(f.Browser().Send(HttpMethod.Post, "/api/auth/register", request), f.Browser().Send(HttpMethod.Post, "/api/auth/register", request));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest);
        await f.Db(async db => Assert.Equal(1, await db.Users.CountAsync(x => x.Email == email)));
    }
    [PostgresFact]
    public async Task Expired_refresh_token_is_rejected()
    {
        var a = await f.RegisterConfirmed(); a.Client.AccessToken = null; f.Clock.Offset = TimeSpan.FromDays(8);
        try { Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode); }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
    [PostgresFact]
    public async Task Database_rejects_cross_tenant_template_even_without_service_checks()
    {
        var a = await f.RegisterConfirmed(); var b = await f.RegisterConfirmed();
        await f.Db(async db =>
        {
            var first = await db.Users.FindAsync(a.Id); var second = await db.Users.FindAsync(b.Id);
            var job = new JobPosition { OrganizationId = first!.OrganizationId, Title = "Job", Description = "Text", Seniority = "Senior", CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow };
            db.JobPositions.Add(job); await db.SaveChangesAsync();
            db.InterviewTemplates.Add(new InterviewTemplate { OrganizationId = second!.OrganizationId, JobPositionId = job.Id, Title = "Forbidden", DurationMinutes = 30, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }
    [PostgresFact]
    public async Task Production_sets_secure_cookies_and_serves_openapi()
    {
        var production = new ApiFixture { Production = true }; await production.InitializeAsync();
        try
        {
            var account = await production.RegisterConfirmed(); var browser = production.Browser();
            var response = await browser.Send(HttpMethod.Post, "/api/auth/login", new LoginRequest(account.Email, account.Password));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains(response.Headers.GetValues("Set-Cookie"), x => x.StartsWith("hireyar.refresh=", StringComparison.Ordinal) && x.Contains("secure", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Get, "/openapi/v1.json")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Get, "/swagger/index.html")).StatusCode);
        }
        finally { await ((IAsyncLifetime)production).DisposeAsync(); }
    }
    [PostgresFact]
    public void Missing_signing_key_fails_startup()
    {
        using var invalid = f.WithWebHostBuilder(b => b.UseSetting("Security:SigningKey", ""));
        Assert.Throws<InvalidOperationException>(() => invalid.CreateClient());
    }
}
