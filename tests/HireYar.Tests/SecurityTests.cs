using System.Collections.Concurrent;
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
    private sealed class ErrorLogProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new ErrorLogger(Entries);
        public void Dispose() { }

        private sealed class ErrorLogger(ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel)) entries.Enqueue(formatter(state, exception));
            }
        }
    }

    private sealed class ProxyRequestStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, continuation) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
                return continuation(context);
            });
            next(app);
        };
    }

    [Fact]
    public async Task Production_csrf_uses_forwarded_https_without_proxy_ip_configuration()
    {
        using var logs = new ErrorLogProvider();
        using var production = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => { logging.ClearProviders(); logging.AddProvider(logs); });
            builder.UseEnvironment("Production");
            builder.UseSetting("InterviewStorage:VideoProvider", "Local");
            builder.UseSetting("ConnectionStrings:Default", "Host=localhost;Database=hireyar_test;Username=test;Password=test");
            builder.UseSetting("Security:SigningKey", new string('x', 32));
            builder.UseSetting("Security:Issuer", "HireYar.Tests");
            builder.UseSetting("Security:Audience", "HireYar.Tests");
            builder.UseSetting("Security:FrontendUrl", "https://localhost:4200");
            builder.UseSetting("Security:AllowedOrigins:0", "https://localhost:4200");
            builder.UseSetting("Security:DataProtectionPath", Path.Combine(Path.GetTempPath(), "hireyar-csrf-" + Guid.NewGuid().ToString("N")));
            builder.ConfigureServices(services => services.AddTransient<IStartupFilter, ProxyRequestStartupFilter>());
        });
        using var client = production.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("hireyar.csrf=", StringComparison.Ordinal) && cookie.Contains("secure", StringComparison.OrdinalIgnoreCase));

        using var plainRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        using var plainResponse = await client.SendAsync(plainRequest);
        Assert.Equal(HttpStatusCode.InternalServerError, plainResponse.StatusCode);
        var problem = await plainResponse.Content.ReadFromJsonAsync<JsonElement>();
        var traceId = problem.GetProperty("traceId").GetString();
        Assert.Contains(logs.Entries, entry => entry.Contains($"TraceId={traceId}", StringComparison.Ordinal));

        using var ambiguousRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        ambiguousRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https,http");
        using var ambiguousResponse = await client.SendAsync(ambiguousRequest);
        Assert.Equal(HttpStatusCode.InternalServerError, ambiguousResponse.StatusCode);
    }

    [Fact]
    public async Task Production_serves_swagger_anonymously_but_protects_account_api()
    {
        using var production = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("InterviewStorage:VideoProvider", "Local");
            builder.UseSetting("ConnectionStrings:Default", "Host=localhost;Database=hireyar_test;Username=test;Password=test");
            builder.UseSetting("Security:SigningKey", new string('x', 32));
            builder.UseSetting("Security:Issuer", "HireYar.Tests");
            builder.UseSetting("Security:Audience", "HireYar.Tests");
            builder.UseSetting("Security:FrontendUrl", "https://localhost:4200");
            builder.UseSetting("Security:AllowedOrigins:0", "https://localhost:4200");
            builder.UseSetting("Security:DataProtectionPath", Path.Combine(Path.GetTempPath(), "hireyar-swagger-" + Guid.NewGuid().ToString("N")));
        });
        using var client = production.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        var openApiResponse = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
        var openApi = await openApiResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("ورود با موبایل", openApi.GetProperty("info").GetProperty("description").GetString());
        Assert.Contains("ورود", openApi.GetProperty("paths").GetProperty("/api/auth/otp/verify").GetProperty("post").GetProperty("summary").GetString());
        var registerOperation = openApi.GetProperty("paths").GetProperty("/api/auth/register").GetProperty("post");
        Assert.Contains("ثبت‌نام", registerOperation.GetProperty("summary").GetString());
        var registrationSchema = registerOperation.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        if (registrationSchema.TryGetProperty("$ref", out var registrationReference))
            registrationSchema = openApi.GetProperty("components").GetProperty("schemas").GetProperty(registrationReference.GetString()!.Split('/').Last());
        var requiredRegistrationFields = registrationSchema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        foreach (var field in new[] { "mobile", "code", "email", "firstName", "lastName" }) Assert.Contains(field, requiredRegistrationFields);
        Assert.DoesNotContain("referralCode", requiredRegistrationFields);
        var adminLogin = openApi.GetProperty("paths").GetProperty("/api/admin/auth/login").GetProperty("post");
        Assert.Contains(adminLogin.GetProperty("parameters").EnumerateArray(), x => x.GetProperty("name").GetString() == "X-Admin-Entry" && x.GetProperty("required").GetBoolean());
        var adminPricing = openApi.GetProperty("paths").GetProperty("/api/admin/pricing/packages").GetProperty("get");
        Assert.True(adminPricing.GetProperty("security")[0].TryGetProperty("SuperAdmin", out _));
        var uploadContent = openApi.GetProperty("paths").GetProperty("/api/candidate/interviews/{interviewId}/answers").GetProperty("post").GetProperty("requestBody").GetProperty("content");
        Assert.False(uploadContent.TryGetProperty("application/x-www-form-urlencoded", out _));
        var uploadSchema = uploadContent.GetProperty("multipart/form-data").GetProperty("schema");
        if (uploadSchema.TryGetProperty("$ref", out var reference))
            uploadSchema = openApi.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/').Last());
        foreach (var field in new[] { "audio", "video" })
        {
            var file = uploadSchema.GetProperty("properties").GetProperty(field);
            Assert.Equal("string", file.GetProperty("type").GetString());
            Assert.Equal("binary", file.GetProperty("format").GetString());
        }
        foreach (var path in openApi.GetProperty("paths").EnumerateObject())
        {
            if (path.Name == "/health") continue;
            foreach (var method in path.Value.EnumerateObject())
                Assert.True(method.Value.TryGetProperty("summary", out var summary) && !string.IsNullOrWhiteSpace(summary.GetString()), $"Missing Persian summary: {method.Name} {path.Name}");
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [PostgresFact]
    public async Task Expired_refresh_token_is_rejected()
    {
        var a = await f.RegisterOwner(); a.Client.AccessToken = null; f.Clock.Offset = TimeSpan.FromDays(8);
        try { Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode); }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
    [PostgresFact]
    public async Task Database_rejects_cross_tenant_template_even_without_service_checks()
    {
        var a = await f.RegisterOwner(); var b = await f.RegisterOwner();
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
            var account = await production.RegisterOwner(); var browser = production.Browser();
            var response = await production.SignInAsync(browser, account.Mobile);
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
