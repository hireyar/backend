using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using HireYar.Application.Auth;
using HireYar.Application.Billing;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class RegistrationTrialTests : IAsyncLifetime
{
    private readonly ApiFixture f = new();
    public Task InitializeAsync() => f.InitializeAsync();
    public Task DisposeAsync() => ((IAsyncLifetime)f).DisposeAsync();
    [PostgresFact]
    public async Task Configured_half_dollar_and_two_days_are_persisted_and_not_repeated()
    {
        var test = new ApiFixture { TrialDurationDays = 2, TrialCreditAmountUsd = 0.5m };
        await test.InitializeAsync();
        try
        {
            var owner = await test.RegisterOwner(signupTrial: true);
            await test.SignInAsync(owner.Client, owner.Mobile);
            await test.Db(async db => {
                var grant = await db.RegistrationTrials.SingleAsync();
                var sub = await db.OrganizationSubscriptions.SingleAsync(x => x.Id == grant.Id);
                Assert.Equal(TimeSpan.FromDays(2), sub.ExpiresAtUtc - sub.StartedAtUtc);
                Assert.Equal(0.5m, grant.CreditAmountUsd);
                Assert.Equal(0.5m, (await db.OrganizationAiWallets.SingleAsync()).Balance);
                var entry = await db.AiCreditTransactions.SingleAsync();
                Assert.Equal(0.5m, entry.Amount); Assert.Equal(0m, entry.BalanceBefore); Assert.Equal(0.5m, entry.BalanceAfter);
                var error = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261010132215_AddRegistrationTrial"));
                Assert.Equal("23514", error.SqlState);
                // The newer team snapshot guard now prevents reaching the older trial downgrade.
                Assert.Contains("Cannot remove registration trial history", error.MessageText);
                Assert.False(db.Database.HasPendingModelChanges());
            });
            var wallet = await (await owner.Client.Send(HttpMethod.Get, "/api/billing/ai-credit")).Content.ReadFromJsonAsync<WalletDto>(BillingJson.Options);
            Assert.Equal(0.5m, wallet!.Balance);
            test.Clock.Offset = TimeSpan.FromDays(2) + TimeSpan.FromMinutes(1);
            using var scope = test.Services.CreateScope();
            var org = (await scope.ServiceProvider.GetRequiredService<HireYar.Infrastructure.Persistence.AppDbContext>().RegistrationTrials.SingleAsync()).OrganizationId;
            Assert.Null(await scope.ServiceProvider.GetRequiredService<ISubscriptionEntitlementService>().CurrentAsync(org, default));
            Assert.Equal(0.5m, (await scope.ServiceProvider.GetRequiredService<WalletService>().GetAsync(org, default)).Balance);
        }
        finally { await ((IAsyncLifetime)test).DisposeAsync(); }
    }

    [PostgresFact]
    public async Task Database_rejects_ledger_that_disagrees_with_configured_credit()
    {
        var test = new ApiFixture(); await test.InitializeAsync();
        try
        {
            var owner = await test.RegisterOwner();
            await test.Db(async db => {
                var org = (await db.Users.SingleAsync(x => x.Id == owner.Id)).OrganizationId;
                await using var transaction = await db.Database.BeginTransactionAsync();
                var service = new RegistrationTrialService(db, Microsoft.Extensions.Options.Options.Create(new RegistrationTrialOptions { DurationDays = 2, CreditAmountUsd = 0.5m }));
                await service.CreateForNewOrganizationAsync(org, test.Clock.GetUtcNow(), default);
                var ledger = db.AiCreditTransactions.Local.Single();
                ledger.Amount = 0.25m; ledger.BalanceAfter = 0.25m;
                db.OrganizationAiWallets.Local.Single().Balance = 0.25m;
                await db.SaveChangesAsync();
                var error = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
                Assert.Equal("23514", error.SqlState); Assert.Contains("Incomplete registration trial", error.MessageText);
            });
            await test.Db(async db => {
                Assert.False(await db.RegistrationTrials.AnyAsync());
                Assert.False(await db.OrganizationAiWallets.AnyAsync());
                Assert.False(await db.AiCreditTransactions.AnyAsync());
            });
        }
        finally { await ((IAsyncLifetime)test).DisposeAsync(); }
    }

    [PostgresFact]
    public async Task Signup_grants_all_package_features_and_one_dollar_exactly_once()
    {
        var a = await f.RegisterOwner(signupTrial: true);
        var overview = await (await a.Client.Send(HttpMethod.Get, "/api/billing/subscription")).Content.ReadFromJsonAsync<SubscriptionOverviewDto>(BillingJson.Options);
        var trial = Assert.IsType<SubscriptionDto>(overview!.Current);
        Assert.True(trial.IsTrial); Assert.Equal("registration_trial", trial.PackageCode);
        Assert.Equal(TimeSpan.FromDays(3), trial.ExpiresAtUtc - trial.StartedAtUtc);
        var wallet = await (await a.Client.Send(HttpMethod.Get, "/api/billing/ai-credit")).Content.ReadFromJsonAsync<WalletDto>(BillingJson.Options);
        Assert.Equal(1m, wallet!.Balance); Assert.Equal(1m, wallet.AvailableCredit);
        await f.Db(async db => {
            var org = await db.Users.Where(x => x.Id == a.Id).Select(x => x.OrganizationId).SingleAsync();
            var grant = await db.RegistrationTrials.Include(x => x.Entitlements).SingleAsync(x => x.OrganizationId == org);
            var codes = await db.PackageFeatures.Where(x => x.IsEnabled && db.PricingPackages.Any(p => p.Id == x.PackageId && p.IsActive && p.IsPublic)).Select(x => x.Feature.Code).Distinct().ToListAsync();
            Assert.Equal(codes.Order(), grant.Entitlements.Select(x => x.Code).Order());
            Assert.Contains("hr_interview", codes); Assert.Contains("technical_interview", codes); Assert.Contains("advanced_ai_workflow", codes);
            Assert.Equal(1m, grant.CreditAmountUsd); Assert.Equal(trial.Id, grant.Id);
            var entry = await db.AiCreditTransactions.SingleAsync(x => x.OrganizationId == org);
            Assert.Equal(CreditTransactionType.PromotionalCredit, entry.Type); Assert.Equal("RegistrationTrial", entry.ReferenceType);
            Assert.Equal(grant.Id, entry.ReferenceId); Assert.Equal(0m, entry.BalanceBefore); Assert.Equal(1m, entry.BalanceAfter);
            Assert.False(await db.Payments.AnyAsync(x => x.OrganizationId == org));
        });
        await f.SignInAsync(a.Client, a.Mobile);
        await f.Db(async db => {
            var org = await db.Users.Where(x => x.Id == a.Id).Select(x => x.OrganizationId).SingleAsync();
            Assert.Single(await db.RegistrationTrials.Where(x => x.OrganizationId == org).ToListAsync());
            Assert.Single(await db.AiCreditTransactions.Where(x => x.OrganizationId == org).ToListAsync());
            Assert.Equal(1m, (await db.OrganizationAiWallets.SingleAsync(x => x.OrganizationId == org)).Balance);
        });
    }

    [PostgresFact]
    public async Task Expiry_closes_new_access_keeps_credit_and_schedules_paid_package_after_trial()
    {
        var a = await f.RegisterOwner(signupTrial: true); Guid org = default;
        await f.Db(async db => org = await db.Users.Where(x => x.Id == a.Id).Select(x => x.OrganizationId).SingleAsync());
        using var scope = f.Services.CreateScope(); var entitlement = scope.ServiceProvider.GetRequiredService<ISubscriptionEntitlementService>();
        var trial = (await entitlement.RequireAsync(org, ["technical_interview", "hr_interview", "ai_interview"], default));
        var catalog = await (await a.Client.Send(HttpMethod.Get, "/api/pricing/packages")).Content.ReadFromJsonAsync<PricingResponse>(BillingJson.Options);
        var period = catalog!.Packages.Single(x => x.Code == "basic").Periods.Single(x => x.Months == 1);
        var pending = await (await a.Client.Send(HttpMethod.Post, "/api/billing/subscription/purchases", new SubscriptionPurchaseRequest(period.Id))).Content.ReadFromJsonAsync<PurchaseResult>(BillingJson.Options);
        Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Post, $"/api/billing/payments/{pending!.Payment.Id}/verify")).StatusCode);
        var overview = await (await a.Client.Send(HttpMethod.Get, "/api/billing/subscription")).Content.ReadFromJsonAsync<SubscriptionOverviewDto>(BillingJson.Options);
        Assert.True(overview!.Current!.IsTrial); Assert.Equal(trial.ExpiresAtUtc, Assert.Single(overview.Scheduled).StartedAtUtc);
        f.Clock.Offset = TimeSpan.FromDays(3) + TimeSpan.FromMinutes(1);
        try
        {
            var current = (await entitlement.CurrentAsync(org, default))!; Assert.False(current.IsTrial); Assert.Equal("basic", current.PackageCode);
            var error = await Assert.ThrowsAsync<AppException>(() => entitlement.RequireAsync(org, ["technical_interview"], default));
            Assert.Equal("subscription_feature_required", error.Code);
            var wallet = await scope.ServiceProvider.GetRequiredService<WalletService>().GetAsync(org, default);
            Assert.Equal(1m, wallet.Balance);
        }
        finally { f.Clock.Offset = TimeSpan.Zero; }
        var b = await f.RegisterOwner(signupTrial: true); Guid other = default;
        await f.Db(async db => other = await db.Users.Where(x => x.Id == b.Id).Select(x => x.OrganizationId).SingleAsync());
        f.Clock.Offset = TimeSpan.FromDays(3) + TimeSpan.FromMinutes(1);
        try
        {
            Assert.Null(await entitlement.CurrentAsync(other, default));
            Assert.Equal("active_subscription_required", (await Assert.ThrowsAsync<AppException>(() => entitlement.RequireAsync(other, ["ai_interview"], default))).Code);
            using var panelScope = f.Services.CreateScope();
            var panel = await panelScope.ServiceProvider.GetRequiredService<BillingPanelService>().OverviewAsync(other, true, default);
            // Expired trials are still readable in the normal panel contract.
            Assert.Equal("expired", panel.SubscriptionState); Assert.True(panel.LastExpired!.IsTrial);
        }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }

    [PostgresFact]
    public async Task Existing_accounts_do_not_receive_a_retroactive_trial()
    {
        var a = await f.RegisterOwner();
        await f.Db(async db => {
            var org = await db.Users.Where(x => x.Id == a.Id).Select(x => x.OrganizationId).SingleAsync();
            Assert.False(await db.RegistrationTrials.AnyAsync(x => x.OrganizationId == org));
            Assert.False(await db.AiCreditTransactions.AnyAsync(x => x.OrganizationId == org));
        });
    }

    [PostgresFact]
    public async Task Concurrent_signup_consumes_otp_and_grants_only_once()
    {
        var first = f.Browser(); var second = f.Browser();
        var mobile = "09" + Random.Shared.NextInt64(100_000_000, 1_000_000_000).ToString("D9");
        Assert.Equal(HttpStatusCode.OK, (await first.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(mobile))).StatusCode);
        var verify = ApiFixture.Registration(mobile, f.PhoneOtp.Codes[mobile]);
        var responses = await Task.WhenAll(first.Send(HttpMethod.Post, "/api/auth/register", verify), second.Send(HttpMethod.Post, "/api/auth/register", verify));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Unauthorized);
        await f.Db(async db => {
            var org = (await db.Users.SingleAsync(x => x.PhoneNumber == mobile)).OrganizationId;
            Assert.Single(await db.RegistrationTrials.Where(x => x.OrganizationId == org).ToListAsync());
            Assert.Single(await db.AiCreditTransactions.Where(x => x.OrganizationId == org).ToListAsync());
        });
    }

    [PostgresFact]
    public async Task Trial_can_start_paid_processing_and_preserves_its_contract()
    {
        var test = new ApiFixture(); await test.InitializeAsync();
        try
        {
            var hr = await test.RegisterOwner(signupTrial: true);
            var job = await (await hr.Client.Send(HttpMethod.Post, "/api/job-positions", new { title = "Synthetic role", description = "Synthetic", seniority = "Senior", status = "Active" })).Content.ReadFromJsonAsync<JsonElement>();
            var id = job.GetProperty("id").GetGuid();
            var template = await (await hr.Client.Send(HttpMethod.Post, "/api/interview-templates", new { jobPositionId = id, title = "Trial interview", durationMinutes = 5, maxFollowUpQuestionsPerQuestion = 0, language = "fa-IR", isArchived = false,
                questions = new[] { new { text = "Synthetic question", order = 1, competency = "SQL", evaluationGuidance = "Observable evidence" } } })).Content.ReadFromJsonAsync<JsonElement>();
            var skill = await (await hr.Client.Send(HttpMethod.Post, "/api/skills", new { name = "SQL" })).Content.ReadFromJsonAsync<JsonElement>();
            var skillId = skill.GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.NoContent, (await hr.Client.Send(HttpMethod.Put, $"/api/job-positions/{id}/skills/{skillId}", new { isRequired = true, priority = 100, targetDifficulty = 3 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Post, $"/api/job-positions/{id}/question-bank", new { skillId, text = "Synthetic question", topic = "SQL", difficulty = 3, isActive = true })).StatusCode);
            var application = await (await hr.Client.Send(HttpMethod.Post, $"/api/job-positions/{id}/applications", new { firstName = "Synthetic", lastName = "Candidate", email = "candidate@example.test" })).Content.ReadFromJsonAsync<JsonElement>();
            var response = await hr.Client.Send(HttpMethod.Post, $"/api/job-positions/{id}/applications/{application.GetProperty("id").GetGuid()}/interviews", new { templateId = template.GetProperty("id").GetGuid(), maxQuestionCount = 1, expiresInHours = 72 });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var invite = (await response.Content.ReadFromJsonAsync<InterviewInvitation>())!;
            using var candidate = test.CreateClient(); candidate.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invite.Token);
            Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/start", null)).StatusCode);
            await test.Db(async db => {
                var bill = await db.InterviewBillings.SingleAsync(x => x.InterviewSessionId == invite.InterviewId);
                Assert.True((await db.OrganizationSubscriptions.SingleAsync(x => x.Id == bill.SubscriptionId)).IsTrial);
                Assert.Equal(0.05m, bill.RatePerMinuteUsd);
                var reserve = await db.AiCreditReservations.SingleAsync(x => x.InterviewSessionId == invite.InterviewId);
                Assert.Equal(0.25m, reserve.ReservedAmountUsd);
                Assert.Equal(1m, (await db.OrganizationAiWallets.SingleAsync(x => x.OrganizationId == bill.OrganizationId)).Balance);
                await db.PackageFeatures.ExecuteUpdateAsync(s => s.SetProperty(x => x.IsEnabled, false));
            });
            test.Clock.Offset = TimeSpan.FromDays(4);
            await test.Db(async db => {
                var session = await db.InterviewSessions.SingleAsync(x => x.Id == invite.InterviewId);
                using var scope = test.Services.CreateScope();
                var codes = await scope.ServiceProvider.GetRequiredService<InterviewBillingService>().CapabilitiesAsync(session, default);
                Assert.Contains("adaptive_questions", codes); Assert.Contains("technical_interview", codes); Assert.Contains("hr_interview", codes);
            });
        }
        finally { test.Clock.Offset = TimeSpan.Zero; await ((IAsyncLifetime)test).DisposeAsync(); }
    }

    [PostgresFact]
    public async Task Unavailable_catalog_rolls_back_signup_and_retry_grants_once()
    {
        var test = new ApiFixture(); await test.InitializeAsync();
        try
        {
            await test.Db(db => db.PricingPackages.ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false)));
            var browser = test.Browser(); var mobile = "09" + Random.Shared.NextInt64(100_000_000, 1_000_000_000).ToString("D9");
            Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(mobile))).StatusCode);
            var verify = ApiFixture.Registration(mobile, test.PhoneOtp.Codes[mobile]);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await browser.Send(HttpMethod.Post, "/api/auth/register", verify)).StatusCode);
            await test.Db(async db => {
                Assert.False(await db.Users.AnyAsync()); Assert.False(await db.Organizations.AnyAsync());
                Assert.False(await db.RegistrationTrials.AnyAsync()); Assert.False(await db.AiCreditTransactions.AnyAsync());
                Assert.Null((await db.OtpChallenges.SingleAsync(x => x.Mobile == mobile)).ConsumedAtUtc);
                await db.PricingPackages.ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true));
            });
            Assert.Equal(HttpStatusCode.OK, (await browser.Send(HttpMethod.Post, "/api/auth/register", verify)).StatusCode);
            await test.Db(async db => { Assert.Single(await db.RegistrationTrials.ToListAsync()); Assert.Single(await db.AiCreditTransactions.ToListAsync()); });
        }
        finally { await ((IAsyncLifetime)test).DisposeAsync(); }
    }

    [PostgresFact]
    public async Task Database_enforces_trial_scope_completeness_immutability_and_safe_downgrade()
    {
        var test = new ApiFixture(); await test.InitializeAsync();
        try
        {
            await test.Db(async db => {
                await db.GetService<IMigrator>().MigrateAsync("20261010085424_AddMediumFirstTopicQuestions");
                await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
            });
            var a = await test.RegisterOwner(signupTrial: true); var b = await test.RegisterOwner();
            await test.Db(async db => {
                var grant = await db.RegistrationTrials.SingleAsync();
                var other = (await db.Users.SingleAsync(x => x.Id == b.Id)).OrganizationId;
                var sub = await db.OrganizationSubscriptions.SingleAsync(x => x.Id == grant.Id);
                async Task Reject(FormattableString sql, string state)
                { Assert.Equal(state, (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(sql))).SqlState); }
                await Reject($"UPDATE \"RegistrationTrials\" SET \"CreditAmountUsd\" = 2 WHERE \"Id\" = {grant.Id}", "23514");
                await Reject($"DELETE FROM \"RegistrationTrialEntitlement\" WHERE \"TrialId\" = {grant.Id}", "23514");
                await Reject($"INSERT INTO \"RegistrationTrials\" (\"Id\",\"OrganizationId\",\"CreatedAtUtc\",\"CreditAmountUsd\",\"AiPricePerMinuteUsd\",\"RetentionDays\") VALUES ({grant.Id},{other},{grant.CreatedAtUtc},1,0.05,60)", "23514");
                var fresh = Guid.NewGuid();
                await Reject($"INSERT INTO \"OrganizationSubscriptions\" (\"Id\",\"OrganizationId\",\"PaymentId\",\"IsTrial\",\"PackageId\",\"PackagePeriodId\",\"Status\",\"StartedAtUtc\",\"ExpiresAtUtc\",\"CreatedAtUtc\") VALUES ({fresh},{other},NULL,true,{sub.PackageId},{sub.PackagePeriodId},'Active',{sub.StartedAtUtc},{sub.ExpiresAtUtc},{sub.CreatedAtUtc})", "23514");
                var error = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261010085424_AddMediumFirstTopicQuestions"));
                Assert.Equal("23514", error.SqlState); Assert.Contains("Cannot remove registration trial history", error.MessageText);
                Assert.Single(await db.RegistrationTrials.ToListAsync()); Assert.False(db.Database.HasPendingModelChanges());
            });
        }
        finally { await ((IAsyncLifetime)test).DisposeAsync(); }
    }
}
