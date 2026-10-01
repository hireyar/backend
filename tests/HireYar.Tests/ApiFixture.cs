using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Auth;
using HireYar.Application.Common;
using HireYar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
namespace HireYar.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute() { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HIREYAR_TEST_POSTGRES"))) Skip = "Set HIREYAR_TEST_POSTGRES to a local PostgreSQL test connection; no database substitute is used."; }
}
public sealed class TestEmail : IEmailSender
{
    public ConcurrentDictionary<string, string> Messages { get; } = new();
    public bool Fail { get; set; }
    public Task SendAsync(string recipient, string subject, string body, CancellationToken ct)
    { if (Fail) throw new IOException("Simulated delivery failure."); Messages[recipient] = body; return Task.CompletedTask; }
    public string Token(string email)
    {
        var body = Messages[email]; var marker = "token="; var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        return Uri.UnescapeDataString(body[start..].Split('\n')[0].Trim());
    }
}
public sealed class TestPhoneOtp : IPhoneOtpSender
{
    public ConcurrentDictionary<string, string> Codes { get; } = new();
    public Task SendAsync(string mobile, string code, CancellationToken ct)
    { Codes[mobile] = code; return Task.CompletedTask; }
}
public sealed class TestClock : TimeProvider
{
    public TimeSpan Offset { get; set; }
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + Offset;
}
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public bool Production { get; init; }
    public bool AutoConfirmEmailOnRegistration { get; init; }
    public bool UseFixedOtp { get; init; }
    public TestEmail Email { get; } = new(); public TestClock Clock { get; } = new();
    public TestPhoneOtp PhoneOtp { get; } = new();
    public FakeInterviewProvider InterviewAi { get; } = new();
    private readonly string database = "hireyar_tests_" + Guid.NewGuid().ToString("N");
    private string? connection;
    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        b.ConfigureLogging(logging => { logging.ClearProviders(); logging.AddConsole(); });
        b.UseEnvironment(Production ? "Production" : "Development");
        b.UseSetting("SkipLocalSettings", "true");
        b.UseSetting("ConnectionStrings:Default", connection!);
        b.UseSetting("Security:SigningKey", Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        b.UseSetting("Security:AutoConfirmEmailOnRegistration", AutoConfirmEmailOnRegistration.ToString());
        b.UseSetting("Kavenegar:UseFixedOtp", UseFixedOtp.ToString());
        b.UseSetting("Kavenegar:FixedOtpCode", "123456");
        b.UseSetting("Security:Issuer", "HireYar.Tests"); b.UseSetting("Security:Audience", "HireYar.Tests");
        b.UseSetting("Security:FrontendUrl", Production ? "https://localhost:4200" : "http://localhost:4200"); b.UseSetting("Security:AllowedOrigins:0", Production ? "https://localhost:4200" : "http://localhost:4200");
        b.UseSetting("Smtp:Host", "localhost"); b.UseSetting("Smtp:Port", "1025"); b.UseSetting("Smtp:From", "no-reply@hireyar.test");
        b.UseSetting("Security:DataProtectionPath", Path.Combine(Path.GetTempPath(), database));
        b.UseSetting("AuthRateLimit:PermitLimit", "1000");
        b.UseSetting("AI:Provider", "Disabled");
        b.UseSetting("AI:NetArz:ApiKey", "");
        b.UseSetting("InterviewStorage:RootPath", Path.Combine(Path.GetTempPath(), database, "audio"));
        b.ConfigureServices(s =>
        {
            s.RemoveAll<IEmailSender>();
            s.AddSingleton<IEmailSender>(Email); s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(Clock);
            s.RemoveAll<IPhoneOtpSender>(); s.AddSingleton<IPhoneOtpSender>(PhoneOtp);
            s.RemoveAll<HireYar.Application.Interviews.ISpeechToTextService>(); s.AddSingleton<HireYar.Application.Interviews.ISpeechToTextService>(InterviewAi);
            s.RemoveAll<HireYar.Application.Interviews.IInterviewDecisionService>(); s.AddSingleton<HireYar.Application.Interviews.IInterviewDecisionService>(InterviewAi);
            s.RemoveAll<HireYar.Application.Interviews.ITextToSpeechService>(); s.AddSingleton<HireYar.Application.Interviews.ITextToSpeechService>(InterviewAi);
        });
    }
    public async Task InitializeAsync()
    {
        var raw = Environment.GetEnvironmentVariable("HIREYAR_TEST_POSTGRES"); if (string.IsNullOrEmpty(raw)) return;
        var parsed = new NpgsqlConnectionStringBuilder(raw);
        if (!new[] { "localhost", "127.0.0.1", "::1" }.Contains(parsed.Host, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Tests only accept a local PostgreSQL.");
        parsed.Database = database; connection = parsed.ConnectionString;
        await using var scope = Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }
    async Task IAsyncLifetime.DisposeAsync()
    {
        if (connection is not null) { await using var scope = Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.EnsureDeletedAsync(); }
        await DisposeAsync();
    }
    public Browser Browser() => new(CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false, BaseAddress = new Uri(Production ? "https://localhost" : "http://localhost") }));
    public async Task<Account> RegisterConfirmed()
    {
        var browser = Browser(); var address = $"{Guid.NewGuid():N}@example.test"; var password = "Test-" + Guid.NewGuid().ToString("N") + "!aA7";
        var response = await browser.Send(HttpMethod.Post, "/api/auth/register", new RegisterRequest("Test Person", "Test Organization", address, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var registration = (await response.Content.ReadFromJsonAsync<RegistrationResult>())!;
        var confirmation = await browser.Send(HttpMethod.Post, "/api/auth/confirm-email", new TokenRequest(registration.UserId, Email.Token(address))); Assert.Equal(HttpStatusCode.NoContent, confirmation.StatusCode);
        var login = await browser.Send(HttpMethod.Post, "/api/auth/login", new LoginRequest(address, password)); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<AccessResponse>())!; browser.AccessToken = session.AccessToken;
        return new(browser, address, password, registration.UserId);
    }
    public async Task Db(Func<AppDbContext, Task> action) { await using var scope = Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
}
public sealed record Account(Browser Client, string Email, string Password, Guid Id);
public sealed class Browser(HttpClient client)
{
    private readonly Dictionary<string, string> cookies = new();
    public string? AccessToken { get; set; }
    public string RefreshToken => cookies.GetValueOrDefault("hireyar.refresh", "");
    public void SetRefresh(string value) => cookies["hireyar.refresh"] = value;
    private void Capture(HttpResponseMessage response)
    { if (response.Headers.TryGetValues("Set-Cookie", out var values)) foreach (var value in values) { var pair = value.Split(';')[0].Split('=', 2); cookies[pair[0]] = pair[1]; } }
    private HttpRequestMessage Request(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url); if (body is not null) request.Content = JsonContent.Create(body);
        if (AccessToken is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookies.Select(x => $"{x.Key}={x.Value}"))); return request;
    }
    public async Task<HttpResponseMessage> Send(HttpMethod method, string url, object? body = null, bool csrf = true, string? origin = null)
    {
        string? token = null;
        if (csrf && method != HttpMethod.Get && url.StartsWith("/api/auth", StringComparison.Ordinal))
        {
            using var csrfRequest = Request(HttpMethod.Get, "/api/auth/csrf"); using var response = await client.SendAsync(csrfRequest); Capture(response);
            var doc = await response.Content.ReadFromJsonAsync<JsonElement>(); token = doc.GetProperty("token").GetString();
        }
        using var request = Request(method, url, body); if (token is not null) request.Headers.Add("X-CSRF-TOKEN", token); if (origin is not null) request.Headers.Add("Origin", origin);
        var result = await client.SendAsync(request); Capture(result); return result;
    }
}
[CollectionDefinition("Postgres", DisableParallelization = true)] public sealed class PostgresCollection : ICollectionFixture<ApiFixture>;
