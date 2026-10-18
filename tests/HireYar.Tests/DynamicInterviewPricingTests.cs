using System.Net;
using System.Net.Http.Json;
using HireYar.Api.Billing;
using HireYar.Application.Billing;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Application.InterviewTemplates;
using HireYar.Application.JobPositions;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using HireYar.Infrastructure.Persistence;
using Npgsql;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class DynamicInterviewPricingTests : IAsyncLifetime
{
    private readonly ApiFixture f = new();
    public Task InitializeAsync() => f.InitializeAsync();
    public Task DisposeAsync() => ((IAsyncLifetime)f).DisposeAsync();
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(BillingJson.Options))!;
    }
    private async Task<RialPackageDto> Package(Account admin, string code = "advanced")
        => (await Read<Page<RialPackageDto>>(await admin.Client.Send(HttpMethod.Get, "/api/admin/pricing/packages"))).Items.Single(x => x.Code == code);
    private async Task<InterviewTypeDto> Type(Account admin, string code = "leadership", string engine = "general")
        => await Read<InterviewTypeDto>(await admin.Client.Send(HttpMethod.Post, "/api/admin/pricing/interview-types",
            new InterviewTypeRequest(code, "Synthetic interview", engine, true)));
    private async Task<RialPackageDto> Price(Account admin, RialPackageDto package, string code, decimal price, bool active = true)
        => await Read<RialPackageDto>(await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/packages/{package.Id}/interview-types/{code}",
            new PackageInterviewTypeRequest(price, active, package.RowVersion)));
    private async Task<(Account Owner, Guid Job, Guid Template, Guid Org)> Setup()
    {
        var owner = await f.RegisterOwner(); await f.FundInterviewsAsync(owner);
        var job = await Read<JobDto>(await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/positions", new WizardPositionRequest("Synthetic", "Synthetic job")));
        var template = await Read<TemplateDto>(await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates",
            new WizardTemplateRequest(job.Id, "Synthetic", "general", Enumerable.Range(1, 5).Select(x => $"Question {x}").ToList(), "Welcome", "Goodbye", 5)));
        Guid org = default;
        await f.Db(async db => org = (await db.Users.SingleAsync(x => x.Id == owner.Id)).OrganizationId);
        return (owner, job.Id, template.Id, org);
    }
    private Task<HttpResponseMessage> Quote(Account owner, Guid template, string type, Guid? package = null)
        => owner.Client.Send(HttpMethod.Get, $"/api/interview-pricing/quote?templateId={template}&interviewType={type}" + (package is null ? "" : $"&packageId={package}"));
    private async Task<HttpResponseMessage> Invite(Account owner, Guid job, Guid template, string type)
    {
        using var client = f.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", owner.Client.AccessToken);
        using var form = new MultipartFormDataContent();
        foreach (var field in new Dictionary<string, string> { ["jobPositionId"] = job.ToString(), ["templateId"] = template.ToString(), ["interviewType"] = type,
            ["firstName"] = "Synthetic", ["lastName"] = "Candidate", ["email"] = "synthetic@example.test", ["phone"] = "09123456789", ["deadlineDays"] = "3", ["resumeText"] = "Synthetic work history" })
            form.Add(new StringContent(field.Value), field.Key);
        form.Add(new ByteArrayContent("%PDF-1.7\nSynthetic"u8.ToArray()), "resume", "resume.pdf");
        return await client.PostAsync("/api/panel-wizard/invitations", form);
    }

    [PostgresFact]
    public async Task Dynamic_types_have_independent_package_prices_and_live_rial_conversion()
    {
        var admin = await f.RegisterAdmin(); var (owner, _, template, _) = await Setup();
        var type = await Type(admin);
        var advanced = await Price(admin, await Package(admin), type.Code, .123456m);
        var basic = await Price(admin, await Package(admin, "basic"), type.Code, .456789m);
        f.DisplayRates.RialPerUsdt = 1234567m;
        var a = await Read<InvitationPriceDto>(await Quote(owner, template, type.Code));
        var b = await Read<InvitationPriceDto>(await Quote(owner, template, type.Code, basic.Id));
        Assert.Equal(.123456m, a.RatePerMinuteUsd); Assert.Equal(.456789m, b.RatePerMinuteUsd);
        Assert.Equal(decimal.Round(.123456m * 1234567m, 6, MidpointRounding.ToEven), a.RatePerMinuteRial);
        Assert.Equal("IRR", a.Currency); Assert.Equal(type.Code, a.InterviewType);
        var catalog = await Read<RialPackageCatalogDto>(await owner.Client.Send(HttpMethod.Get, "/api/pricing/packages"));
        Assert.Equal(a.RatePerMinuteRial, catalog.Packages.Single(x => x.Id == advanced.Id).InterviewTypes!.Single(x => x.Code == type.Code).PricePerMinuteRial);
        f.DisplayRates.RialPerUsdt = 2000000m;
        Assert.Equal(246912m, (await Read<InvitationPriceDto>(await Quote(owner, template, type.Code))).RatePerMinuteRial);
        await f.Db(async db => {
            Assert.Null(db.Model.FindEntityType(typeof(PricingPackage))!.FindProperty("AiPricePerMinuteUsd"));
            Assert.Null(db.Model.FindEntityType(typeof(PricingPackage))!.FindProperty("SpecializedAiPricePerMinuteUsd"));
            Assert.Equal(2, await db.PackageInterviewTypes.CountAsync(x => x.InterviewTypeCode == type.Code));
        });
    }

    [PostgresFact]
    public async Task Only_super_admin_can_manage_types_and_prices_with_validation_and_concurrency()
    {
        var admin = await f.RegisterAdmin(); var owner = await f.RegisterOwner();
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.Client.Send(HttpMethod.Post, "/api/admin/pricing/interview-types", new InterviewTypeRequest("custom", "Custom", "general", true))).StatusCode);
        var type = await Type(admin);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Client.Send(HttpMethod.Post, "/api/admin/pricing/interview-types", new InterviewTypeRequest(type.Code, "Duplicate", "general", true))).StatusCode);
        var package = await Package(admin);
        foreach (var bad in new[] { 0m, -1m, 1001m, .1234567m })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/packages/{package.Id}/interview-types/{type.Code}", new PackageInterviewTypeRequest(bad, true, package.RowVersion))).StatusCode);
        var changed = await Price(admin, package, type.Code, .1m);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/packages/{package.Id}/interview-types/{type.Code}", new PackageInterviewTypeRequest(.2m, true, package.RowVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/interview-types/{type.Code}", new InterviewTypeUpdateRequest(new(type.Code, type.Name, "specialized", true), type.RowVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/packages/{changed.Id}/interview-types/unknown", new PackageInterviewTypeRequest(.2m, true, changed.RowVersion))).StatusCode);
    }

    [PostgresFact]
    public async Task Activation_blocks_new_quotes_and_reservations_but_preserves_existing_billing()
    {
        var admin = await f.RegisterAdmin(); var (owner, job, template, _) = await Setup();
        var type = await Type(admin, "custom_technical", "specialized");
        var package = await Price(admin, await Package(admin), type.Code, .000001m);
        f.DisplayRates.RialPerUsdt = 1000000m;
        var invitation = (await Read<PanelInvitationResult>(await Invite(owner, job, template, type.Code))).Invitation;
        package = await Price(admin, package, type.Code, .5m, false);
        Assert.Equal(HttpStatusCode.Forbidden, (await Quote(owner, template, type.Code)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Invite(owner, job, template, type.Code)).StatusCode);
        var catalog = await Read<RialPackageCatalogDto>(await owner.Client.Send(HttpMethod.Get, "/api/pricing/packages"));
        Assert.DoesNotContain(catalog.Packages.Single(x => x.Id == package.Id).InterviewTypes!, x => x.Code == type.Code);
        await f.Db(async db => {
            var bill = await db.InterviewBillings.SingleAsync(x => x.InterviewSessionId == invitation.InterviewId);
            Assert.Equal(.000001m, bill.RatePerMinuteUsd); Assert.Equal(1m, bill.RatePerMinuteRial);
            var session = await db.InterviewSessions.SingleAsync(x => x.Id == invitation.InterviewId);
            Assert.Equal(type.Code, session.PricingInterviewTypeCode); Assert.Equal("technical_interview", session.RequiredCapabilityCode);
            Assert.Equal(1, await db.InterviewSessions.CountAsync(x => x.PricingInterviewTypeCode == type.Code));
        });
        package = await Price(admin, package, type.Code, .2m);
        var inactive = await Read<InterviewTypeDto>(await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/interview-types/{type.Code}", new InterviewTypeUpdateRequest(new(type.Code, type.Name, type.EngineType, false), type.RowVersion)));
        Assert.False(inactive.IsActive);
        Assert.Equal(HttpStatusCode.Conflict, (await Quote(owner, template, type.Code)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Invite(owner, job, template, type.Code)).StatusCode);
        var types = await Read<List<InterviewTypeDto>>(await owner.Client.Send(HttpMethod.Get, "/api/pricing/interview-types"));
        Assert.DoesNotContain(types, x => x.Code == type.Code);
        var list = await Read<Page<PanelInterviewDto>>(await owner.Client.Send(HttpMethod.Get, "/api/interviews"));
        Assert.Equal(type.Code, list.Items.Single(x => x.Id == invitation.InterviewId).InterviewType);
        // Existing reservations remain usable even after global deactivation and loss of the FX provider.
        f.DisplayRates.Unavailable = true;
        using var candidate = f.CreateClient(); candidate.DefaultRequestHeaders.Authorization = new("Bearer", invitation.Token);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{invitation.InterviewId}/start", null)).StatusCode);
    }

    [PostgresFact]
    public async Task Dynamic_template_codes_validate_against_database_and_package_with_no_prices_is_supported()
    {
        var admin = await f.RegisterAdmin(); var (owner, job, _, _) = await Setup();
        var type = await Type(admin);
        var dto = await Read<TemplateDto>(await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates", new WizardTemplateRequest(job, "Custom template", type.Code,
            Enumerable.Range(1, 5).Select(x => $"Question {x}").ToList(), "Welcome", "Goodbye", 5)));
        Assert.Equal(type.Code, dto.InterviewType);
        Assert.Equal(HttpStatusCode.Conflict, (await Quote(owner, dto.Id, type.Code)).StatusCode);
        var empty = await Read<RialPackageDto>(await admin.Client.Send(HttpMethod.Post, "/api/admin/pricing/packages", new {
            code = "custom_package", name = "Synthetic package", description = "", isActive = true, isPublic = true, sortOrder = 3, interviewRetentionDays = 30
        }));
        Assert.Empty(empty.InterviewTypes!);
        var updated = await Price(admin, empty, type.Code, .25m);
        Assert.Equal(.25m, Assert.Single(updated.InterviewTypes!).PricePerMinuteUsd);
    }

    [PostgresFact]
    public async Task Upgrade_initializes_approved_rates_preserves_availability_and_drops_old_columns()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("HIREYAR_TEST_POSTGRES"))
            { Database = "hireyar_type_upgrade_" + Guid.NewGuid().ToString("N") };
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261018132745_AddPackageSpecializedInterviewPrice");
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE "PricingPackages" SET "AiPricePerMinuteUsd" = 0.123456,
                    "SpecializedAiPricePerMinuteUsd" = 0.654321 WHERE "Code" = 'advanced';
                UPDATE "PackageFeatures" SET "IsEnabled" = false
                WHERE "FeatureId" = (SELECT "Id" FROM "FeatureDefinitions" WHERE "Code" = 'hr_interview')
                    AND "PackageId" = (SELECT "Id" FROM "PricingPackages" WHERE "Code" = 'advanced');
                """);
            await db.Database.MigrateAsync();
            var advanced = await db.PricingPackages.Include(x => x.InterviewTypes).SingleAsync(x => x.Code == "advanced");
            Assert.Equal(.03m, advanced.InterviewTypes.Single(x => x.InterviewTypeCode == "general").PricePerMinuteUsd);
            Assert.False(advanced.InterviewTypes.Single(x => x.InterviewTypeCode == "general").IsActive);
            Assert.Equal(.05m, advanced.InterviewTypes.Single(x => x.InterviewTypeCode == "specialized").PricePerMinuteUsd);
            Assert.True(advanced.InterviewTypes.Single(x => x.InterviewTypeCode == "specialized").IsActive);
            Assert.Equal(0, await db.Database.SqlQueryRaw<int>("""
                SELECT count(*)::int AS "Value" FROM information_schema.columns
                WHERE table_name = 'PricingPackages' AND column_name IN ('AiPricePerMinuteUsd', 'SpecializedAiPricePerMinuteUsd')
                """).SingleAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            await db.Database.MigrateAsync();
            Assert.Equal(3, await db.PackageInterviewTypes.CountAsync());
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("""
                UPDATE "PackageInterviewTypes" SET "PricePerMinuteUsd" = 0
                """));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "PackageInterviewTypes" ("PackageId", "InterviewTypeCode", "PricePerMinuteUsd", "IsActive")
                SELECT "Id", 'unknown', 1, true FROM "PricingPackages" LIMIT 1
                """));
            await db.GetService<IMigrator>().MigrateAsync("20261018132745_AddPackageSpecializedInterviewPrice");
            Assert.Equal(.03m, await db.Database.SqlQueryRaw<decimal>("""
                SELECT "AiPricePerMinuteUsd" AS "Value" FROM "PricingPackages" WHERE "Code" = 'advanced'
                """
            ).SingleAsync());
            Assert.Equal(.05m, await db.Database.SqlQueryRaw<decimal>("""
                SELECT "SpecializedAiPricePerMinuteUsd" AS "Value" FROM "PricingPackages" WHERE "Code" = 'advanced'
                """
            ).SingleAsync());
            await db.Database.MigrateAsync();
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "InterviewTypes" ("Code", "Name", "EngineType", "IsActive", "SortOrder", "RowVersion")
                VALUES ('custom', 'Synthetic', 'general', true, 3, decode(md5('synthetic'), 'hex'))
                """);
            var blocked = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261018132745_AddPackageSpecializedInterviewPrice"));
            Assert.Contains("without data loss", blocked.MessageText);
            Assert.Equal(3, await db.InterviewTypes.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
