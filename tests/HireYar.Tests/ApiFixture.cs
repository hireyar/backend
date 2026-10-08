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
    public bool UseFixedOtp { get; init; }
    public bool DisabledPayments { get; init; }
    public bool DiagnosticsEnabled { get; init; }
    public FakePaymentGateway PaymentGateway { get; } = new();
    public TestClock Clock { get; } = new();
    public TestPhoneOtp PhoneOtp { get; } = new();
    public FakeInvitationNotifier Invitations { get; } = new();
    public FakeQuestionSuggestionProvider QuestionAi { get; } = new();
    public FakeInterviewProvider InterviewAi { get; } = new();
    public FakeInterviewReportProvider ReportAi { get; } = new();
    private readonly string database = "hireyar_tests_" + Guid.NewGuid().ToString("N");
    private string? connection;
    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        b.ConfigureLogging(logging => { logging.ClearProviders(); logging.AddConsole(); });
        b.UseEnvironment(Production ? "Production" : "Development");
        b.UseSetting("SkipLocalSettings", "true");
        b.UseSetting("InterviewDiagnostics:Enabled", DiagnosticsEnabled.ToString());
        b.UseSetting("InterviewDiagnostics:CapturePrompts", "true");
        b.UseSetting("ConnectionStrings:Default", connection!);
        b.UseSetting("Security:SigningKey", Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        b.UseSetting("Kavenegar:UseFixedOtp", UseFixedOtp.ToString());
        b.UseSetting("Kavenegar:FixedOtpCode", "123456");
        b.UseSetting("Security:Issuer", "HireYar.Tests"); b.UseSetting("Security:Audience", "HireYar.Tests");
        b.UseSetting("Security:FrontendUrl", Production ? "https://localhost:4200" : "http://localhost:4200"); b.UseSetting("Security:AllowedOrigins:0", Production ? "https://localhost:4200" : "http://localhost:4200");
        b.UseSetting("Security:DataProtectionPath", Path.Combine(Path.GetTempPath(), database));
        b.UseSetting("AuthRateLimit:PermitLimit", "1000");
        b.UseSetting("CandidateRateLimit:PermitLimit", "10000");
        b.UseSetting("AI:Provider", "Disabled");
        b.UseSetting("InterviewAudio:BackgroundRecoveryEnabled", "false");
        b.UseSetting("AI:NetArz:ApiKey", "");
        b.UseSetting("InterviewStorage:RootPath", Path.Combine(Path.GetTempPath(), database, "audio"));
        b.ConfigureServices(s =>
        {
            if (!DisabledPayments) { s.RemoveAll<HireYar.Application.Billing.IPaymentGateway>(); s.AddSingleton<HireYar.Application.Billing.IPaymentGateway>(PaymentGateway); }
            s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(Clock);
            s.RemoveAll<IPhoneOtpSender>(); s.AddSingleton<IPhoneOtpSender>(PhoneOtp);
            s.RemoveAll<HireYar.Application.Interviews.IInterviewInvitationNotifier>(); s.AddSingleton<HireYar.Application.Interviews.IInterviewInvitationNotifier>(Invitations);
            s.RemoveAll<HireYar.Application.Interviews.IQuestionSuggestionProvider>(); s.AddSingleton<HireYar.Application.Interviews.IQuestionSuggestionProvider>(QuestionAi);
            s.RemoveAll<HireYar.Application.Interviews.ISpeechToTextService>(); s.AddSingleton<HireYar.Application.Interviews.ISpeechToTextService>(InterviewAi);
            s.RemoveAll<HireYar.Application.Interviews.IInterviewDecisionService>(); s.AddSingleton<HireYar.Application.Interviews.IInterviewDecisionService>(InterviewAi);
            s.RemoveAll<HireYar.Application.Interviews.IInterviewReportProvider>(); s.AddSingleton<HireYar.Application.Interviews.IInterviewReportProvider>(ReportAi);
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
    public async Task<HttpResponseMessage> SignInAsync(Browser browser, string mobile)
    {
        // Permit another synthetic OTP challenge without delaying the suite or changing the global clock.
        await Db(async db => { var challenge = await db.OtpChallenges.SingleOrDefaultAsync(x => x.Mobile == mobile); if (challenge is not null) { challenge.CreatedAtUtc = Clock.GetUtcNow().AddMinutes(-2); await db.SaveChangesAsync(); } });
        Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(mobile))).StatusCode);
        var response = await browser.Send(HttpMethod.Post, "/api/auth/otp/verify", new VerifyOtpRequest(mobile, UseFixedOtp ? "123456" : PhoneOtp.Codes[mobile]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        browser.AccessToken = (await response.Content.ReadFromJsonAsync<AccessResponse>())!.AccessToken;
        return response;
    }
    public async Task<Account> RegisterOwner()
    {
        var browser = Browser(); var mobile = "09" + Random.Shared.NextInt64(100_000_000, 1_000_000_000).ToString("D9");
        await SignInAsync(browser, mobile);
        var me = (await (await browser.Send(HttpMethod.Get, "/api/auth/me")).Content.ReadFromJsonAsync<CurrentUserDto>(BillingJson.Options))!;
        return new(browser, mobile, me.Id);
    }
    public async Task Db(Func<AppDbContext, Task> action) { await using var scope = Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
    public async Task FundInterviewsAsync(Account account)
    {
        var pricing = await (await account.Client.Send(HttpMethod.Get, "/api/pricing/packages")).Content.ReadFromJsonAsync<PricingResponse>(BillingJson.Options);
        var package = pricing!.Packages.Single(x => x.Code == "advanced");
        var pending = await (await account.Client.Send(HttpMethod.Post, "/api/billing/subscription/purchases", new HireYar.Application.Billing.SubscriptionPurchaseRequest(package.Periods.Single(x => x.Months == 1).Id))).Content.ReadFromJsonAsync<HireYar.Application.Billing.PurchaseResult>(BillingJson.Options);
        Assert.Equal(HttpStatusCode.OK, (await account.Client.Send(HttpMethod.Post, $"/api/billing/payments/{pending!.Payment.Id}/verify")).StatusCode);
        await using var scope = Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var org = await db.Users.Where(x => x.Id == account.Id).Select(x => x.OrganizationId).SingleAsync();
        await scope.ServiceProvider.GetRequiredService<HireYar.Application.Billing.WalletService>().AdjustAsync(org, new(Guid.NewGuid(), 100m, "Synthetic interview test credit"), default);
    }
}
public sealed record PricingResponse(IReadOnlyList<HireYar.Application.Billing.PackageDto> Packages);
public sealed record Account(Browser Client, string Mobile, Guid Id);
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
        var request = new HttpRequestMessage(method, url); if (body is not null) request.Content = JsonContent.Create(body, options: BillingJson.Options);
        if (AccessToken is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookies.Select(x => $"{x.Key}={x.Value}"))); return request;
    }
    public async Task<HttpResponseMessage> Send(HttpMethod method, string url, object? body = null, bool csrf = true, string? origin = null)
    {
        string? token = null;
        if (csrf && method != HttpMethod.Get && url.StartsWith("/api/auth", StringComparison.Ordinal))
        {
            using var csrfRequest = Request(HttpMethod.Get, "/api/auth/csrf"); using var response = await client.SendAsync(csrfRequest); Capture(response);
            var doc = await response.Content.ReadFromJsonAsync<JsonElement>(BillingJson.Options); token = doc.GetProperty("token").GetString();
        }
        using var request = Request(method, url, body); if (token is not null) request.Headers.Add("X-CSRF-TOKEN", token); if (origin is not null) request.Headers.Add("Origin", origin);
        var result = await client.SendAsync(request); Capture(result); return result;
    }
}
[CollectionDefinition("Postgres", DisableParallelization = true)] public sealed class PostgresCollection : ICollectionFixture<ApiFixture>;
