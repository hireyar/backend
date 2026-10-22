using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Organizations;
using HireYar.Domain.Interviews;
using HireYar.Domain.InterviewTemplates;
using HireYar.Domain.JobPositions;
using HireYar.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class PackagingRemovalTests(ApiFixture f)
{
    private static readonly string[] RetiredTables = [
        "PricingPackages", "PackagePeriods", "FeatureDefinitions", "PackageFeatures", "InterviewTypes", "PackageInterviewTypes",
        "AiCreditPackages", "Payments", "TeamSeatPurchases", "SubscriptionPurchases", "SubscriptionEntitlement",
        "OrganizationSubscriptions", "SubscriptionReplacements", "OrganizationAiWallets", "AiCreditTransactions",
        "AiCreditReservations", "InterviewBillings", "RegistrationTrials", "RegistrationTrialEntitlement"
    ];

    [PostgresFact]
    public async Task Legacy_routes_and_schema_stay_removed_with_the_new_subscription_model()
    {
        var owner = await f.RegisterOwner(explicitSignup: true);
        foreach (var route in new[] { "/api/pricing/packages", "/api/pricing/ai-credit-packages", "/api/pricing/interview-types",
            "/api/pricing/ai-display-rate", "/api/billing/panel", "/api/billing/subscription", "/api/billing/ai-credit",
            "/api/interview-pricing/quote", "/api/organization/features" })
            Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.Send(HttpMethod.Get, route)).StatusCode);
        for (var i = 0; i < 7; i++)
        {
            var mobile = "09" + Random.Shared.NextInt64(100_000_000, 1_000_000_000).ToString("D9");
            Assert.Equal(HttpStatusCode.Created, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations",
                new InviteTeamMemberRequest(mobile, "Synthetic", "Member", Email: "invite@example.test"))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.Send(HttpMethod.Get, "/api/admin/pricing/packages")).StatusCode);
        var api = await f.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.DoesNotContain(api.GetProperty("paths").EnumerateObject(), p => p.Name.StartsWith("/api/billing") ||
            p.Name.StartsWith("/api/pricing") || p.Name.StartsWith("/api/admin/pricing") || p.Name.StartsWith("/api/interview-pricing"));
        await f.Db(async db =>
        {
            Assert.False(db.Database.HasPendingModelChanges());
            var tables = await db.Database.SqlQueryRaw<string>("SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = 'public'").ToArrayAsync();
            Assert.DoesNotContain(tables, name => RetiredTables.Contains(name));
        });
    }

    [PostgresFact]
    public async Task Upgrade_drops_populated_financial_schema_preserves_interviews_and_normalizes_custom_template_types()
    {
        var legacy = new ApiFixture { MigrationTarget = "20261018182652_NormalizePackageInterviewTypes" };
        await legacy.InitializeAsync();
        try
        {
            var org = new Organization { Name = "Synthetic migration organization" };
            var job = new JobPosition { OrganizationId = org.Id, Title = "Synthetic role" };
            var template = new InterviewTemplate { OrganizationId = org.Id, JobPositionId = job.Id, Title = "Synthetic template",
                InterviewType = "custom_general", DurationMinutes = 5 };
            var application = new CandidateApplication { OrganizationId = org.Id, JobPositionId = job.Id,
                FirstName = "Synthetic", LastName = "Candidate", Email = "migration@example.test" };
            var skill = Guid.NewGuid(); var question = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
            var session = new InterviewSession(org.Id, job.Id, application.Id, template.Id,
                [new(question, skill, "Synthetic question", "Synthetic topic", 3, true)], [new(skill, "Synthetic skill", true, 100, 3)],
                1, 300, now, now.AddDays(1));
            session.SetAccessTokenHash(new string('a', 64));
            await legacy.Db(async db =>
            {
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO "InterviewTypes" ("Code","Name","EngineType","IsActive","SortOrder","RowVersion")
                    VALUES ('custom_general','Synthetic custom type','general',true,99,decode('aa','hex'));
                    """);
                db.Organizations.Add(org); db.JobPositions.Add(job); db.InterviewTemplates.Add(template); db.CandidateApplications.Add(application);
                await db.SaveChangesAsync();
                var row = db.Model.FindEntityType(typeof(InterviewSession))!.GetProperties()
                    .Where(p => p.Name != nameof(InterviewSession.InterviewType))
                    .ToDictionary(p => p.Name, p => p.PropertyInfo!.GetValue(session));
                foreach (var key in row.Keys.ToArray())
                {
                    if (row[key] is Enum e) row[key] = e.ToString();
                    if (row[key] is byte[] bytes) row[key] = "\\x" + Convert.ToHexString(bytes);
                }
                row["RequiredCapabilityCode"] = "hr_interview"; row["PricingInterviewTypeCode"] = "custom_general";
                var json = JsonSerializer.Serialize(row);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "InterviewSessions" SELECT * FROM jsonb_populate_record(NULL::"InterviewSessions", {json}::jsonb);
                    INSERT INTO "OrganizationAiWallets" ("Id","OrganizationId","Currency","Balance","CreatedAtUtc","UpdatedAtUtc")
                        VALUES ({Guid.NewGuid()},{org.Id},'IRR',0,{now},{now});
                    INSERT INTO "Payments" ("Id","OrganizationId","Type","Amount","Currency","Status","Provider","CreatedAtUtc","ExpiresAtUtc")
                        VALUES ({Guid.NewGuid()},{org.Id},'Subscription',100,'IRT','Pending','Synthetic',{now},{now.AddHours(1)});
                    """);
                var script = db.GetService<IMigrator>().GenerateScript(legacy.MigrationTarget, "20261019065637_RemoveLegacyPackagingAndBilling", MigrationsSqlGenerationOptions.Idempotent);
                await db.Database.ExecuteSqlRawAsync(script);
                await db.Database.ExecuteSqlRawAsync(script);
                Assert.False(db.Database.HasPendingModelChanges());
                db.ChangeTracker.Clear();
                var saved = await db.InterviewSessions.IgnoreAutoIncludes().SingleAsync(s => s.Id == session.Id);
                Assert.Equal("general", saved.InterviewType); Assert.Equal(application.Id, saved.CandidateApplicationId);
                Assert.Equal("specialized", session.InterviewType); // Migration uses the historical execution strategy.
                Assert.Equal("general", (await db.InterviewTemplates.SingleAsync(t => t.Id == template.Id)).InterviewType);
                Assert.Equal("Synthetic Candidate", (await db.CandidateApplications.SingleAsync()).FirstName + " " + (await db.CandidateApplications.SingleAsync()).LastName);
                var tables = await db.Database.SqlQueryRaw<string>("SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname='public'").ToArrayAsync();
                Assert.DoesNotContain(tables, t => RetiredTables.Contains(t));
                var functions = await db.Database.SqlQueryRaw<string>("SELECT proname::text AS \"Value\" FROM pg_proc JOIN pg_namespace n ON n.oid=pronamespace WHERE n.nspname='public'").ToArrayAsync();
                Assert.DoesNotContain(functions, n => n.StartsWith("hireyar_") && (n.Contains("billing") || n.Contains("wallet") ||
                    n.Contains("payment") || n.Contains("subscription") || n.Contains("reservation") || n.Contains("replacement") || n.Contains("trial") || n.Contains("team_seat")));
                await db.GetService<IMigrator>().MigrateAsync(); // Reapplying is a no-op.
                var rollback = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(legacy.MigrationTarget));
                Assert.Equal(PostgresErrorCodes.RaiseException, rollback.SqlState);
                Assert.Single(await db.InterviewSessions.IgnoreAutoIncludes().ToListAsync());
            });
        }
        finally { await ((IAsyncLifetime)legacy).DisposeAsync(); }
    }
}
