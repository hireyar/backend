using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Auth;
using HireYar.Domain.InterviewTemplates;
using HireYar.Domain.JobPositions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Tests;

[Collection("Sql")]
public sealed class SecurityTests(ApiFixture f)
{
    [SqlFact]
    public async Task Concurrent_registration_enforces_database_email_uniqueness()
    {
        var email = $"{Guid.NewGuid():N}@example.test"; var request = new RegisterRequest("Name", "Org", email, "Valid-test-password1!");
        var results = await Task.WhenAll(f.Browser().Send(HttpMethod.Post, "/api/auth/register", request), f.Browser().Send(HttpMethod.Post, "/api/auth/register", request));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest);
        await f.Db(async db => Assert.Equal(1, await db.Users.CountAsync(x => x.Email == email)));
    }
    [SqlFact]
    public async Task Expired_refresh_token_is_rejected()
    {
        var a = await f.RegisterConfirmed(); a.Client.AccessToken = null; f.Clock.Offset = TimeSpan.FromDays(8);
        try { Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode); }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
    [SqlFact]
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
    [SqlFact]
    public async Task Production_sets_secure_cookies_and_hides_openapi()
    {
        var production = new ApiFixture { Production = true }; await production.InitializeAsync();
        try
        {
            var account = await production.RegisterConfirmed(); var browser = production.Browser();
            var response = await browser.Send(HttpMethod.Post, "/api/auth/login", new LoginRequest(account.Email, account.Password));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains(response.Headers.GetValues("Set-Cookie"), x => x.StartsWith("hireyar.refresh=", StringComparison.Ordinal) && x.Contains("secure", StringComparison.OrdinalIgnoreCase));
            Assert.NotEqual(HttpStatusCode.OK, (await browser.Send(HttpMethod.Get, "/openapi/v1.json")).StatusCode);
        }
        finally { await ((IAsyncLifetime)production).DisposeAsync(); }
    }
    [SqlFact]
    public void Missing_signing_key_fails_startup()
    {
        using var invalid = f.WithWebHostBuilder(b => b.UseSetting("Security:SigningKey", ""));
        Assert.Throws<InvalidOperationException>(() => invalid.CreateClient());
    }
}
