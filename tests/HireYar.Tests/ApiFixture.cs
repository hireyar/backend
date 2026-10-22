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
using Microsoft.Extensions.Hosting;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Interviews;
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
    public Action<TimerCallback, object?, TimeSpan>? TimerCreated { get; set; }
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        TimerCreated?.Invoke(callback, state, dueTime);
        return timer;
    }
}
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SalesEntry = "synthetic-sales-entry-key-for-tests-only";
    public bool SalesEnabled { get; init; } = true;
    public int SalesRateLimit { get; init; } = 1000;
    public const string AdminEntry = "synthetic-admin-entry-key-for-tests-only";
    public const string AdminPassword = "Synthetic-Admin-Password-42!";
    public bool AdminEnabled { get; init; } = true;
    public int AdminRateLimit { get; init; } = 1000;
    public TestAdminCaptcha AdminCaptcha { get; } = new();
    public string? MigrationTarget { get; init; }
    public bool Production { get; init; }
    public bool UseS3VideoStorage { get; init; }
    public FakeVideoS3 VideoS3 { get; } = new();
    public FakeTicketS3 TicketS3 { get; } = new();
    public bool UseFixedOtp { get; init; }
    public bool DiagnosticsEnabled { get; init; }
    public bool AssessmentWorkerEnabled { get; init; }
    public TestClock Clock { get; } = new();
    public TestPhoneOtp PhoneOtp { get; } = new();
    public FakeInvitationNotifier Invitations { get; } = new();
    public FakeQuestionSuggestionProvider QuestionAi { get; } = new();
    public FakeInterviewProvider InterviewAi { get; } = new();
    public FakeReportNarrativeProvider NarrativeAi { get; } = new();
    public FakeAnswerAssessmentProvider AssessmentAi { get; } = new();
    private readonly string database = "hireyar_tests_" + Guid.NewGuid().ToString("N");
    private string? connection;
    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        b.ConfigureLogging(logging => { logging.ClearProviders(); logging.AddConsole(); });
        b.UseEnvironment(Production ? "Production" : "Development");
        b.UseSetting("SkipLocalSettings", "true");
        b.UseSetting("Notifications:Enabled", "false");
        b.UseSetting("InterviewNotifications:Email:Enabled", "false");
        b.UseSetting("TicketStorage:AccessKey", "synthetic");
        b.UseSetting("TicketStorage:SecretKey", "synthetic");
        b.UseSetting("TicketStorage:Bucket", "hireyar-ticketing");
        b.UseSetting("SalesAdmin:Enabled", SalesEnabled.ToString());
        b.UseSetting("SalesAdmin:RandomKey", SalesEntry);
        b.UseSetting("SalesAdmin:AuthRequestsPerMinute", SalesRateLimit.ToString());
        b.UseSetting("SuperAdmin:Enabled", AdminEnabled.ToString());
        b.UseSetting("SuperAdmin:RandomKey", AdminEntry);
        b.UseSetting("SuperAdmin:AuthRequestsPerMinute", AdminRateLimit.ToString());
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
        foreach (var capability in HireYar.Infrastructure.Interviews.AiRoutingOptions.Capabilities)
            b.UseSetting($"AI:{capability}:Provider", "Disabled");
        b.UseSetting("AnswerAssessments:WorkerEnabled", AssessmentWorkerEnabled.ToString());
        b.UseSetting("AnswerAssessments:AttemptTimeoutSeconds", "1");
        b.UseSetting("InterviewAudio:BackgroundRecoveryEnabled", "false");
        b.UseSetting("AI:NetArz:ApiKey", "");
        b.UseSetting("InterviewStorage:VideoProvider", UseS3VideoStorage ? "ArvanS3" : "Local");
        if (UseS3VideoStorage) {
            b.UseSetting("InterviewStorage:S3:AccessKey", "synthetic-access");
            b.UseSetting("InterviewStorage:S3:SecretKey", "synthetic-secret");
        }
        b.UseSetting("InterviewStorage:RootPath", Path.Combine(Path.GetTempPath(), database, "audio"));
        b.ConfigureServices(s =>
        {
            s.RemoveAll<IReportNarrativeProvider>(); s.AddSingleton<IReportNarrativeProvider>(NarrativeAi);
            s.RemoveAll<IAnswerAssessmentProvider>(); s.AddSingleton<IAnswerAssessmentProvider>(AssessmentAi);
            s.RemoveAll<IAdminCaptcha>(); s.AddSingleton<IAdminCaptcha>(AdminCaptcha);
            if (UseS3VideoStorage) { s.RemoveAll<Amazon.S3.IAmazonS3>(); s.AddSingleton<Amazon.S3.IAmazonS3>(VideoS3); }
            s.AddKeyedSingleton<Amazon.S3.IAmazonS3>("ticketing", TicketS3);
            s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(Clock);
            s.RemoveAll<IPhoneOtpSender>(); s.AddSingleton<IPhoneOtpSender>(PhoneOtp);
            s.RemoveAll<HireYar.Application.Interviews.IInterviewInvitationNotifier>(); s.AddSingleton<HireYar.Application.Interviews.IInterviewInvitationNotifier>(Invitations);
            s.RemoveAll<HireYar.Application.Interviews.IQuestionSuggestionProvider>(); s.AddSingleton<HireYar.Application.Interviews.IQuestionSuggestionProvider>(QuestionAi);
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
        await using var scope = Services.CreateAsyncScope(); var testDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(testDb).MigrateAsync(MigrationTarget);
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
    public static RegisterRequest Registration(string mobile, string code) => new(mobile, code, mobile + "@example.test", "نام", "آزمایشی");
    public async Task<Account> RegisterOwner(bool explicitSignup = false, bool provisionMembership = true)
    {
        var browser = Browser(); var mobile = "09" + Random.Shared.NextInt64(100_000_000, 1_000_000_000).ToString("D9");
        // Exercise either an existing account or the explicit OTP registration flow.
        if (!explicitSignup)
        {
            await using var scope = Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = Clock.GetUtcNow(); var org = new HireYar.Domain.Organizations.Organization { Name = "سازمان من", CreatedAtUtc = now, UpdatedAtUtc = now };
            db.Organizations.Add(org); await db.SaveChangesAsync();
            var result = await scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<HireYar.Infrastructure.Auth.ApplicationUser>>()
                .CreateAsync(new() { Id = Guid.NewGuid(), UserName = mobile, PhoneNumber = mobile, PhoneNumberConfirmed = true,
                    FullName = "کاربر", OrganizationId = org.Id, Role = HireYar.Domain.Organizations.OrganizationRole.Owner, CreatedAtUtc = now, UpdatedAtUtc = now });
            Assert.True(result.Succeeded);
        }
        if (explicitSignup)
        {
            Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(mobile))).StatusCode);
            var response = await browser.Send(HttpMethod.Post, "/api/auth/register", Registration(mobile, UseFixedOtp ? "123456" : PhoneOtp.Codes[mobile]));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            browser.AccessToken = (await response.Content.ReadFromJsonAsync<AccessResponse>())!.AccessToken;
        }
        else await SignInAsync(browser, mobile);
        var me = (await (await browser.Send(HttpMethod.Get, "/api/auth/me")).Content.ReadFromJsonAsync<CurrentUserDto>(TestJson.Options))!;
        if (provisionMembership)
        {
            await using var setupScope = Services.CreateAsyncScope();
            var setupDb = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = Clock.GetUtcNow(); var request = Guid.NewGuid();
            var subscriptionOrder = new HireYar.Domain.Subscriptions.SubscriptionOrder { OrganizationId = me.OrganizationId,
                RequestId = request, RequestFingerprint = new string('a',64), Product = "subscription", ProductName = "Synthetic baseline",
                Status = "Confirmed", Months = 12, TeamUsers = 30, UnlimitedTeam = true, UnitPriceRial = 90_000_000,
                OriginalAmountRial = 1_080_000_000, AmountRial = 1_080_000_000, RetentionDays = 60,
                CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(1), ConfirmedAtUtc = now, ConfirmedByUserId = me.Id, PaymentReference = "synthetic-" + request };
            var bundleOrder = new HireYar.Domain.Subscriptions.SubscriptionOrder { OrganizationId = me.OrganizationId,
                RequestId = Guid.NewGuid(), RequestFingerprint = new string('b',64), Product = "interviews", ProductName = "Synthetic quota",
                BundleCode = "custom", Status = "Confirmed", Quantity = 1000, UnitPriceRial = 4_000_000,
                OriginalAmountRial = 4_000_000_000, AmountRial = 4_000_000_000, RetentionDays = 60,
                CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(1), ConfirmedAtUtc = now, ConfirmedByUserId = me.Id, PaymentReference = "synthetic-quota-" + request };
            setupDb.SubscriptionOrders.AddRange(subscriptionOrder, bundleOrder);
            setupDb.OrganizationMemberships.Add(new() { OrganizationId = me.OrganizationId, OrderId = subscriptionOrder.Id,
                StartedAtUtc = now.AddDays(-1), ExpiresAtUtc = now.AddDays(-1).AddMonths(12), TeamUsers = 30, UnlimitedTeam = true, RetentionDays = 60 });
            setupDb.InterviewQuotaGrants.Add(new() { OrganizationId = me.OrganizationId, OrderId = bundleOrder.Id, Quantity = 1000, CreatedAtUtc = now });
            await setupDb.SaveChangesAsync();
        }
        return new(browser, mobile, me.Id);
    }
    public async Task Db(Func<AppDbContext, Task> action) { await using var scope = Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
    public async Task<Account> RegisterAdmin()
    {
        var a = await RegisterOwner();
        await using (var scope = Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<HireYar.Infrastructure.Auth.AdminAccounts>().ProvisionAsync(a.Mobile, AdminPassword, default);
        a.Client.AdminEntryKey = AdminEntry;
        var challenge = (await (await a.Client.Send(HttpMethod.Get, "/api/admin/auth/captcha")).Content.ReadFromJsonAsync<AdminCaptchaDto>(TestJson.Options))!;
        var login = await a.Client.Send(HttpMethod.Post, "/api/admin/auth/login", new AdminLoginRequest(a.Mobile, AdminPassword, challenge.Id, "123456"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        a.Client.AccessToken = (await login.Content.ReadFromJsonAsync<AdminSessionDto>(TestJson.Options))!.AccessToken;
        return a;
    }
}
public sealed record Account(Browser Client, string Mobile, Guid Id);
public sealed class Browser(HttpClient client)
{
    private readonly Dictionary<string, string> cookies = new();
    public string? AccessToken { get; set; }
    public string? AdminEntryKey { get; set; }
    public string? SalesEntryKey { get; set; }
    public string RefreshToken => cookies.GetValueOrDefault("hireyar.refresh", "");
    public void SetRefresh(string value) => cookies["hireyar.refresh"] = value;
    private void Capture(HttpResponseMessage response)
    { if (response.Headers.TryGetValues("Set-Cookie", out var values)) foreach (var value in values) { var pair = value.Split(';')[0].Split('=', 2); cookies[pair[0]] = pair[1]; } }
    private HttpRequestMessage Request(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url); if (body is not null) request.Content = JsonContent.Create(body, options: TestJson.Options);
        if (SalesEntryKey is not null) request.Headers.Add("X-Sales-Entry", SalesEntryKey);
        if (AdminEntryKey is not null) request.Headers.Add("X-Admin-Entry", AdminEntryKey);
        if (AccessToken is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookies.Select(x => $"{x.Key}={x.Value}"))); return request;
    }
    public async Task<HttpResponseMessage> Send(HttpMethod method, string url, object? body = null, bool csrf = true, string? origin = null)
    {
        string? token = null;
        if (csrf && method != HttpMethod.Get && url.StartsWith("/api/auth", StringComparison.Ordinal))
        {
            using var csrfRequest = Request(HttpMethod.Get, "/api/auth/csrf"); using var response = await client.SendAsync(csrfRequest); Capture(response);
            var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options); token = doc.GetProperty("token").GetString();
        }
        using var request = Request(method, url, body); if (token is not null) request.Headers.Add("X-CSRF-TOKEN", token); if (origin is not null) request.Headers.Add("Origin", origin);
        var result = await client.SendAsync(request); Capture(result); return result;
    }
}
[CollectionDefinition("Postgres", DisableParallelization = true)] public sealed class PostgresCollection : ICollectionFixture<ApiFixture>;
