using HireYar.Api.Billing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Billing;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Domain.Billing;
using HireYar.Domain.Interviews;
using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace HireYar.Tests;

public sealed class BillingMathTests
{
    [Theory]
    [InlineData(1800, "0.03", "0.9")]
    [InlineData(3600, "0.03", "1.8")]
    [InlineData(1800, "0.05", "1.5")]
    [InlineData(2520, "0.03", "1.26")]
    [InlineData(2520, "0.05", "2.1")]
    [InlineData(2540, "0.03", "1.27")]
    [InlineData(2540, "0.05", "2.116667")]
    [InlineData(1, "0.00003", "0.000000")]
    [InlineData(3, "0.00003", "0.000002")]
    public void Seconds_cost_uses_decimal_and_final_bankers_rounding(int seconds, string rate, string expected)
    { var culture = System.Globalization.CultureInfo.InvariantCulture; Assert.Equal(decimal.Parse(expected, culture), BillingMath.Cost(seconds, decimal.Parse(rate, culture))); }

    [Fact] public void Invalid_duration_or_rate_is_rejected()
    { Assert.Throws<ArgumentOutOfRangeException>(() => BillingMath.Cost(-1, 0.03m)); Assert.Throws<ArgumentOutOfRangeException>(() => BillingMath.Cost(1, -1m)); }
}

[Collection("Postgres")]
public sealed class BillingTests(ApiFixture f)
{
    private async Task<Guid> Org(Account a) { Guid org = default; await f.Db(async db => org = await db.Users.Where(x => x.Id == a.Id).Select(x => x.OrganizationId).SingleAsync()); return org; }
    private async Task<Account> Admin()
    {
        return await f.RegisterAdmin();
    }
    private async Task<RialPackageDto> SeedPackage(string code) => (await (await f.Browser().Send(HttpMethod.Get, "/api/pricing/packages")).Content.ReadFromJsonAsync<PricingResponse>(BillingJson.Options))!.Packages.Single(x => x.Code == code);
    private static PackageRequest Request(RialPackageDto p, decimal? rate = null, bool? active = null) => new(p.Code, p.Name, p.Description, active ?? p.IsActive, p.IsPublic, p.SortOrder, rate ?? p.AiPricePerMinuteUsd, p.RetentionDays);
    private async Task<RialPurchaseResult> Buy(Account a, Guid period) => (await (await a.Client.Send(HttpMethod.Post, "/api/billing/subscription/purchases", new SubscriptionPurchaseRequest(period))).Content.ReadFromJsonAsync<RialPurchaseResult>(BillingJson.Options))!;
    private async Task Verify(Account a, Guid payment) => Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Post, $"/api/billing/payments/{payment}/verify")).StatusCode);
    private async Task<RialCreditPackageDto> CreditProduct(Account admin)
    {
        var response = await admin.Client.Send(HttpMethod.Post, "/api/admin/pricing/ai-credit-packages", new CreditPackageRequest("Synthetic credit", 10m, 123456m, "IRT", true, true, 0));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<RialCreditPackageDto>(BillingJson.Options))!;
    }
    private async Task<RialPurchaseResult> BuyCredit(Account a, Guid product) => (await (await a.Client.Send(HttpMethod.Post, "/api/billing/ai-credit/purchases", new CreditPurchaseRequest(product))).Content.ReadFromJsonAsync<RialPurchaseResult>(BillingJson.Options))!;
    private async Task<RialWalletDto> Wallet(Account a) => (await (await a.Client.Send(HttpMethod.Get, "/api/billing/ai-credit")).Content.ReadFromJsonAsync<RialWalletDto>(BillingJson.Options))!;
    private async Task<InterviewInvitation> Invite(Account a, string capability = "technical_interview", bool insufficient = false)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var job = await (await a.Client.Send(HttpMethod.Post, "/api/job-positions", new { title = "Synthetic role " + suffix, description = "Synthetic job", seniority = "Senior", status = "Active" })).Content.ReadFromJsonAsync<JsonElement>(BillingJson.Options);
        var jobId = job.GetProperty("id").GetGuid();
        var template = await (await a.Client.Send(HttpMethod.Post, "/api/interview-templates", new { jobPositionId = jobId, title = "Synthetic interview " + suffix, durationMinutes = 60, maxFollowUpQuestionsPerQuestion = 0, language = "fa-IR", isArchived = false,
            questions = new[] { new { text = "Synthetic", order = 1, competency = "HR", evaluationGuidance = "Observable evidence" } } })).Content.ReadFromJsonAsync<JsonElement>(BillingJson.Options);
        var skill = await (await a.Client.Send(HttpMethod.Post, "/api/skills", new { name = "skill-" + Guid.NewGuid() })).Content.ReadFromJsonAsync<JsonElement>(BillingJson.Options);
        var skillId = skill.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await a.Client.Send(HttpMethod.Put, $"/api/job-positions/{jobId}/skills/{skillId}", new { isRequired = true, priority = 10, targetDifficulty = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Post, $"/api/job-positions/{jobId}/question-bank", new { skillId, text = "Synthetic question", topic = "Synthetic topic", difficulty = 1, isActive = true })).StatusCode);
        var application = await (await a.Client.Send(HttpMethod.Post, $"/api/job-positions/{jobId}/applications", new { firstName = "Synthetic", lastName = "Candidate", email = "candidate@example.test" })).Content.ReadFromJsonAsync<JsonElement>(BillingJson.Options);
        var result = await a.Client.Send(HttpMethod.Post, $"/api/job-positions/{jobId}/applications/{application.GetProperty("id").GetGuid()}/interviews",
            new CreateInterviewRequest(template.GetProperty("id").GetGuid(), 1, 72, capability));
        if (insufficient) { Assert.Equal(HttpStatusCode.Conflict, result.StatusCode); Assert.Contains("insufficient_ai_credit", await result.Content.ReadAsStringAsync()); return null!; }
        Assert.Equal(HttpStatusCode.OK, result.StatusCode); return (await result.Content.ReadFromJsonAsync<InterviewInvitation>(BillingJson.Options))!;
    }
    private HttpClient Candidate(InterviewInvitation i)
    { var c = f.CreateClient(); c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", i.Token); return c; }

    [PostgresFact]
    public async Task Catalog_seed_prices_and_features_are_exact()
    {
        var basic = await SeedPackage("basic"); var advanced = await SeedPackage("advanced");
        Assert.Equal(new decimal[] { 3000000, 8550000, 16200000, 28800000 }, basic.Periods.Select(x => x.Price / 10m));
        Assert.Equal(new decimal[] { 4500000, 12825000, 24300000, 43200000 }, advanced.Periods.Select(x => x.Price / 10m));
        Assert.Equal(0.03m, basic.AiPricePerMinuteUsd); Assert.Equal(0.05m, advanced.AiPricePerMinuteUsd);
        Assert.Equal(30, basic.RetentionDays); Assert.Equal(60, advanced.RetentionDays);
        Assert.DoesNotContain(basic.Features, x => x.Code == "technical_interview"); Assert.Contains(advanced.Features, x => x.Code == "technical_interview");
    }
    [PostgresFact]
    public async Task Admin_can_create_dynamic_periods_archive_and_enforce_stale_versions()
    {
        var admin = await Admin(); var response = await admin.Client.Send(HttpMethod.Post, "/api/admin/pricing/packages", new PackageRequest("test_" + Guid.NewGuid().ToString("N"), "Custom", "Synthetic", true, true, 0, 0.07m, 90));
        var p = (await response.Content.ReadFromJsonAsync<RialPackageDto>(BillingJson.Options))!; Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var change = await admin.Client.Send(HttpMethod.Post, $"/api/admin/pricing/packages/{p.Id}/periods", new PeriodWriteRequest(new(9, 123456m, "IRT", 7m, true, 0), p.RowVersion));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode); var latest = (await change.Content.ReadFromJsonAsync<RialPackageDto>(BillingJson.Options))!;
        Assert.Contains((await (await f.Browser().Send(HttpMethod.Get, "/api/pricing/packages")).Content.ReadFromJsonAsync<PricingResponse>(BillingJson.Options))!.Packages, x => x.Id == p.Id && x.Periods.Any(y => y.Months == 9));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/packages/{p.Id}", new PackageUpdateRequest(Request(p), p.RowVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/packages/{p.Id}", new PackageUpdateRequest(Request(latest, active: false), latest.RowVersion))).StatusCode);
        Assert.DoesNotContain((await (await f.Browser().Send(HttpMethod.Get, "/api/pricing/packages")).Content.ReadFromJsonAsync<PricingResponse>(BillingJson.Options))!.Packages, x => x.Id == p.Id);
    }
    [PostgresFact]
    public async Task Normal_hr_cannot_administer_pricing_and_admin_revocation_is_immediate()
    {
        var a = await f.RegisterOwner(); Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.Send(HttpMethod.Get, "/api/admin/pricing/packages")).StatusCode);
        var admin = await Admin(); Assert.Equal(HttpStatusCode.OK, (await admin.Client.Send(HttpMethod.Get, "/api/admin/pricing/packages")).StatusCode);
        await f.Db(async db => { var u = await db.Users.SingleAsync(x => x.Id == admin.Id); u.IsSystemAdmin = false; await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.Client.Send(HttpMethod.Get, "/api/admin/pricing/packages")).StatusCode);
    }
    [PostgresFact]
    public async Task Duplicate_callbacks_and_concurrent_credit_verifications_fulfill_once()
    {
        var admin = await Admin(); var product = await CreditProduct(admin); var a = await f.RegisterOwner(); var pending = await BuyCredit(a, product.Id);
        Assert.Equal(PaymentStatus.Pending, pending.Payment.Status); Assert.Equal(1234560m, pending.Payment.Amount);
        using var c = f.CreateClient(); var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => c.PostAsync($"/api/payment-callbacks/{pending.Payment.Id}", null)));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode)); await Verify(a, pending.Payment.Id);
        Assert.Equal(10m, (await Wallet(a)).Balance);
        await f.Db(async db => { var ledger = await db.AiCreditTransactions.SingleAsync(x => x.ReferenceId == pending.Payment.Id); Assert.Equal(0m, ledger.BalanceBefore); Assert.Equal(10m, ledger.BalanceAfter); });
    }
    [PostgresFact]
    public async Task Duplicate_provider_transaction_rolls_back_second_wallet_fulfillment()
    {
        var admin = await Admin(); var product = await CreditProduct(admin); var a = await f.RegisterOwner(); var b = await f.RegisterOwner();
        var first = await BuyCredit(a, product.Id); var second = await BuyCredit(b, product.Id); var txn = "shared-test-" + Guid.NewGuid();
        f.PaymentGateway.Results[first.Payment.Id] = new(true, txn, first.Payment.Amount / 10m, "IRT"); f.PaymentGateway.Results[second.Payment.Id] = new(true, txn, second.Payment.Amount / 10m, "IRT");
        await Verify(a, first.Payment.Id); Assert.Equal(HttpStatusCode.Conflict, (await b.Client.Send(HttpMethod.Post, $"/api/billing/payments/{second.Payment.Id}/verify")).StatusCode);
        Assert.Equal(0m, (await Wallet(b)).Balance);
        await f.Db(async db => Assert.Equal(PaymentStatus.Pending, (await db.Payments.SingleAsync(x => x.Id == second.Payment.Id)).Status));
    }
    [PostgresFact]
    public async Task Failed_or_mismatched_verification_never_credits_wallet()
    {
        var admin = await Admin(); var product = await CreditProduct(admin); var a = await f.RegisterOwner();
        foreach (var failure in new[] { new PaymentVerificationResult(false, null, 123456m, "IRT"), new(true, "invalid-amount", 1m, "IRT"), new(true, "invalid-currency", 123456m, "USD") })
        {
            var pending = await BuyCredit(a, product.Id); f.PaymentGateway.Results[pending.Payment.Id] = failure; await Verify(a, pending.Payment.Id);
            await f.Db(async db => Assert.Equal(PaymentStatus.Failed, (await db.Payments.SingleAsync(x => x.Id == pending.Payment.Id)).Status));
        }
        Assert.Equal(0m, (await Wallet(a)).Balance);
    }
    [PostgresFact]
    public async Task Client_cannot_supply_authoritative_price_or_access_other_org_payment()
    {
        var a = await f.RegisterOwner(); var b = await f.RegisterOwner(); var p = await SeedPackage("basic"); var period = p.Periods[0].Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Post, "/api/billing/subscription/purchases", new { packagePeriodId = period, price = 1 })).StatusCode);
        var pending = await Buy(a, period);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.Send(HttpMethod.Get, $"/api/billing/payments/{pending.Payment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.Send(HttpMethod.Post, $"/api/billing/payments/{pending.Payment.Id}/verify")).StatusCode);
        Assert.Equal(0m, (await Wallet(b)).Balance); Assert.Null((await (await b.Client.Send(HttpMethod.Get, "/api/billing/subscription")).Content.ReadFromJsonAsync<JsonElement>(BillingJson.Options)).GetProperty("current").GetString());
    }
    [PostgresFact]
    public async Task Subscription_renewals_schedule_and_credit_purchase_does_not_extend_subscription()
    {
        var a = await f.RegisterOwner(); var package = await SeedPackage("basic"); var p1 = await Buy(a, package.Periods[0].Id); await Verify(a, p1.Payment.Id);
        var p2 = await Buy(a, package.Periods[0].Id); await Task.WhenAll(Verify(a, p2.Payment.Id), Verify(a, p2.Payment.Id));
        var org = await Org(a); await f.Db(async db => {
            var s = await db.OrganizationSubscriptions.Where(x => x.OrganizationId == org).OrderBy(x => x.StartedAtUtc).ToListAsync();
            Assert.Equal(2, s.Count); Assert.Equal(s[0].ExpiresAtUtc, s[1].StartedAtUtc); Assert.Equal(SubscriptionStatus.Scheduled, s[1].Status);
        });
        var admin = await Admin(); var credit = await BuyCredit(a, (await CreditProduct(admin)).Id); await Verify(a, credit.Payment.Id);
        await f.Db(async db => Assert.Equal(2, await db.OrganizationSubscriptions.CountAsync(x => x.OrganizationId == org)));
    }
    [PostgresFact]
    public async Task Snapshot_survives_catalog_price_rate_and_feature_changes_before_verification()
    {
        var admin = await Admin(); var a = await f.RegisterOwner();
        var create = await admin.Client.Send(HttpMethod.Post, "/api/admin/pricing/packages", new PackageRequest("contract_" + Guid.NewGuid().ToString("N"), "Original", "", true, true, 0, 0.123456m, 75));
        var p = (await create.Content.ReadFromJsonAsync<RialPackageDto>(BillingJson.Options))!;
        var period = await admin.Client.Send(HttpMethod.Post, $"/api/admin/pricing/packages/{p.Id}/periods", new PeriodWriteRequest(new(2, 123m, "IRT", 12m, true, 0), p.RowVersion));
        p = (await period.Content.ReadFromJsonAsync<RialPackageDto>(BillingJson.Options))!;
        var features = (await (await admin.Client.Send(HttpMethod.Get, "/api/admin/pricing/features")).Content.ReadFromJsonAsync<List<FeatureDto>>(BillingJson.Options))!;
        var feature = features.Single(x => x.Code == "hr_interview");
        p = (await (await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/packages/{p.Id}/features", new FeatureWriteRequest(feature.Id, true, null, p.RowVersion))).Content.ReadFromJsonAsync<RialPackageDto>(BillingJson.Options))!;
        var pending = await Buy(a, p.Periods[0].Id);
        p = (await (await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/packages/{p.Id}", new PackageUpdateRequest(Request(p, rate: 0.9m), p.RowVersion))).Content.ReadFromJsonAsync<RialPackageDto>(BillingJson.Options))!;
        Assert.Equal(HttpStatusCode.OK, (await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/packages/{p.Id}/features", new FeatureWriteRequest(feature.Id, false, null, p.RowVersion))).StatusCode);
        await Verify(a, pending.Payment.Id); await using var scope = f.Services.CreateAsyncScope();
        var e = scope.ServiceProvider.GetRequiredService<ISubscriptionEntitlementService>(); var snapshot = await e.RequireAsync(await Org(a), ["hr_interview"], default);
        Assert.Equal(0.123456m, snapshot.AiRatePerMinuteUsd); Assert.Equal(75, snapshot.RetentionDays); Assert.Equal("Original", snapshot.PackageName);
        await f.Db(async db => { var purchase = await db.SubscriptionPurchases.SingleAsync(x => x.PaymentId == pending.Payment.Id); Assert.Equal(123m, purchase.PriceAmountSnapshot); Assert.Equal(2, purchase.PeriodMonthsSnapshot); });
    }
    [PostgresFact]
    public async Task Ledger_adjustment_is_idempotent_and_history_is_immutable_in_application_and_postgres()
    {
        var a = await f.RegisterOwner(); var org = await Org(a); var key = Guid.NewGuid();
        async Task Adjust() { await using var scope = f.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<WalletService>().AdjustAsync(org, new(key, 1.234567m, "Synthetic adjustment"), default); }
        await Task.WhenAll(Adjust(), Adjust()); Assert.Equal(1.234567m, (await Wallet(a)).Balance);
        await f.Db(async db => { var tx = await db.AiCreditTransactions.SingleAsync(x => x.ReferenceId == key); tx.Amount = 2; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); });
        await f.Db(async db => await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"AiCreditTransactions\" WHERE \"ReferenceId\" = {key}")));
    }
    [PostgresFact]
    public async Task Concurrent_reservations_cannot_overspend_and_cancel_releases_full_maximum()
    {
        var a = await f.RegisterOwner(); var package = await SeedPackage("advanced"); var purchase = await Buy(a, package.Periods[0].Id); await Verify(a, purchase.Payment.Id);
        var org = await Org(a); await using (var scope = f.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<WalletService>().AdjustAsync(org, new(Guid.NewGuid(), 5m, "Synthetic credit"), default);
        var winning = await Invite(a); await Invite(a, insufficient: true);
        using var candidate = Candidate(winning);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{winning.InterviewId}/start", null)).StatusCode);
        var balance = await Wallet(a); Assert.Equal(3m, balance.ReservedCredit); Assert.Equal(2m, balance.AvailableCredit); Assert.Equal(5m, balance.Balance);
        Assert.Equal(HttpStatusCode.NoContent, (await a.Client.Send(HttpMethod.Post, $"/api/interviews/{winning.InterviewId}/revoke")).StatusCode);
        balance = await Wallet(a); Assert.Equal(0m, balance.ReservedCredit); Assert.Equal(5m, balance.AvailableCredit);
    }
    [PostgresFact]
    public async Task Completion_bills_server_window_once_and_releases_unused_reservation()
    {
        var a = await f.RegisterOwner(); await f.FundInterviewsAsync(a); var invite = await Invite(a); using var c = Candidate(invite);
        var start = await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/start", null); var current = (await start.Content.ReadFromJsonAsync<CandidateInterviewDto>(BillingJson.Options))!;
        f.Clock.Offset = TimeSpan.FromSeconds(42);
        try
        {
            using var form = new MultipartFormDataContent(); var bytes = new ByteArrayContent("ID3-synthetic-billing-answer"u8.ToArray()); bytes.Headers.ContentType = new("audio/mpeg");
            form.Add(bytes, "audio", "answer.mp3"); form.Add(new StringContent(current.Question!.Id.ToString()), "questionId");
            form.Add(new StringContent("1"), "durationSeconds");
            var answer = await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/answers", form); Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
            Assert.Equal(HttpStatusCode.Gone, (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Gone, (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null)).StatusCode);
            await f.Db(async db => { var bill = await db.InterviewBillings.SingleAsync(x => x.InterviewSessionId == invite.InterviewId);
                Assert.InRange(bill.BillableSeconds, 42, 45); Assert.Equal(BillingMath.Cost(bill.BillableSeconds, 0.05m), bill.BilledAmountRial);
                Assert.Equal(1, await db.AiCreditTransactions.CountAsync(x => x.ReferenceId == invite.InterviewId)); });
            Assert.Equal(0m, (await Wallet(a)).ReservedCredit);
        }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
    [PostgresFact]
    public async Task Long_disconnected_window_and_provider_retry_do_not_add_billable_time()
    {
        var a = await f.RegisterOwner(); await f.FundInterviewsAsync(a); var invite = await Invite(a); using var c = Candidate(invite);
        var current = (await (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/start", null)).Content.ReadFromJsonAsync<CandidateInterviewDto>(BillingJson.Options))!;
        f.Clock.Offset = TimeSpan.FromSeconds(200); f.InterviewAi.FailStt = true;
        try
        {
            using var form = new MultipartFormDataContent(); var bytes = new ByteArrayContent("ID3-synthetic-billing-answer"u8.ToArray()); bytes.Headers.ContentType = new("audio/mpeg"); form.Add(bytes, "audio", "answer.mp3"); form.Add(new StringContent(current.Question!.Id.ToString()), "questionId");
            var failed = (await (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/answers", form)).Content.ReadFromJsonAsync<CandidateInterviewDto>(BillingJson.Options))!;
            f.InterviewAi.FailStt = false; f.Clock.Offset = TimeSpan.FromSeconds(250);
            Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/answers/{failed.Answer!.Id}/retry", null)).StatusCode);
            await f.Db(async db => { var bill = await db.InterviewBillings.SingleAsync(x => x.InterviewSessionId == invite.InterviewId); Assert.Equal(120, bill.BillableSeconds); Assert.Equal(0.1m, bill.BilledAmountRial); });
            Assert.Equal(99.9m, (await Wallet(a)).Balance);
        }
        finally { f.Clock.Offset = TimeSpan.Zero; f.InterviewAi.Reset(); }
    }
    [PostgresFact]
    public async Task Expiry_during_started_interview_keeps_rate_but_blocks_new_start()
    {
        var a = await f.RegisterOwner(); await f.FundInterviewsAsync(a); var org = await Org(a);
        DateTimeOffset expiry = default; await f.Db(async db => expiry = await db.OrganizationSubscriptions.Where(x => x.OrganizationId == org).Select(x => x.ExpiresAtUtc).SingleAsync());
        f.Clock.Offset = expiry - DateTimeOffset.UtcNow - TimeSpan.FromSeconds(30);
        try
        {
            var first = await Invite(a); var second = await Invite(a); using var c = Candidate(first); using var c2 = Candidate(second);
            Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/candidate/interviews/{first.InterviewId}/start", null)).StatusCode);
            f.Clock.Offset += TimeSpan.FromSeconds(45);
            Assert.Equal(HttpStatusCode.Forbidden, (await c2.PostAsync($"/api/candidate/interviews/{second.InterviewId}/start", null)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/candidate/interviews/{first.InterviewId}/complete", null)).StatusCode);
            await f.Db(async db => Assert.Equal(0.05m, (await db.InterviewBillings.SingleAsync(x => x.InterviewSessionId == first.InterviewId)).RatePerMinuteUsd));
        }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
    [PostgresFact]
    public async Task Expired_reservation_releases_available_credit_and_no_answer_cost()
    {
        var a = await f.RegisterOwner(); await f.FundInterviewsAsync(a); var invite = await Invite(a); using var c = Candidate(invite);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/start", null)).StatusCode);
        f.Clock.Offset = TimeSpan.FromHours(2);
        try { var w = await Wallet(a); Assert.Equal(0m, w.ReservedCredit); Assert.Equal(100m, w.AvailableCredit); await f.Db(async db => Assert.Equal(ReservationStatus.Expired, (await db.AiCreditReservations.SingleAsync(x => x.InterviewSessionId == invite.InterviewId)).Status)); }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
    [PostgresFact]
    public async Task Basic_entitlement_blocks_technical_invite_but_allows_hr_and_candidate_cannot_access_finance()
    {
        var a = await f.RegisterOwner(); var p = await SeedPackage("basic"); var purchase = await Buy(a, p.Periods[0].Id); await Verify(a, purchase.Payment.Id);
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var e = scope.ServiceProvider.GetRequiredService<ISubscriptionEntitlementService>(); var org = await Org(a);
            await e.RequireAsync(org, ["hr_interview"], default); var error = await Assert.ThrowsAsync<AppException>(() => e.RequireAsync(org, ["technical_interview"], default)); Assert.Equal("subscription_feature_required", error.Code);
        }
        await Invite(a, "hr_interview", insufficient: true);
        await using (var scope = f.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<WalletService>().AdjustAsync(await Org(a), new(Guid.NewGuid(), 3m, "Synthetic credit"), default);
        var invite = await Invite(a, "hr_interview"); using var c = Candidate(invite);
        foreach (var route in new[] { "/api/billing/ai-credit", "/api/billing/subscription", "/api/billing/payments", "/api/admin/pricing/packages" }) Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/billing/subscription/purchases", new SubscriptionPurchaseRequest(p.Periods[0].Id))).StatusCode);
        var start = await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/start", null); Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        await f.Db(async db => Assert.Single(await db.AiCreditReservations.Where(x => x.InterviewSessionId == invite.InterviewId).ToListAsync()));
    }
    [PostgresFact]
    public async Task Disabled_gateway_keeps_payment_pending_without_fake_credit()
    {
        var disabled = new ApiFixture { DisabledPayments = true }; await disabled.InitializeAsync();
        try
        {
            var a = await disabled.RegisterOwner(); var pricing = (await (await a.Client.Send(HttpMethod.Get, "/api/pricing/packages")).Content.ReadFromJsonAsync<PricingResponse>(BillingJson.Options))!;
            var pending = (await (await a.Client.Send(HttpMethod.Post, "/api/billing/subscription/purchases", new SubscriptionPurchaseRequest(pricing.Packages[0].Periods[0].Id))).Content.ReadFromJsonAsync<RialPurchaseResult>(BillingJson.Options))!;
            Assert.False(pending.GatewayEnabled); Assert.Null(pending.RedirectUrl); Assert.Equal(PaymentStatus.Pending, pending.Payment.Status);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await a.Client.Send(HttpMethod.Post, $"/api/billing/payments/{pending.Payment.Id}/verify")).StatusCode);
            await disabled.Db(async db => { Assert.Equal(0, await db.OrganizationSubscriptions.CountAsync()); Assert.Equal(0, await db.AiCreditTransactions.CountAsync()); });
        }
        finally { await ((IAsyncLifetime)disabled).DisposeAsync(); }
    }
    [PostgresFact]
    public async Task Database_rejects_unledgered_wallet_mutation_and_cross_org_financial_relation()
    {
        var a = await f.RegisterOwner(); var b = await f.RegisterOwner(); var orgA = await Org(a); var orgB = await Org(b); await Wallet(a);
        await f.Db(async db => await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"OrganizationAiWallets\" SET \"Balance\" = 99 WHERE \"OrganizationId\" = {orgA}")));
        Assert.Equal(0m, (await Wallet(a)).Balance);
        await f.Db(async db => {
            var walletId = await db.OrganizationAiWallets.Where(x => x.OrganizationId == orgA).Select(x => x.Id).SingleAsync();
            db.AiCreditTransactions.Add(new() { OrganizationId = orgB, WalletId = walletId, Amount = 0, BalanceBefore = 0, BalanceAfter = 0, Type = CreditTransactionType.AdminAdjustment,
                ReferenceType = "Synthetic", ReferenceId = Guid.NewGuid(), CreatedAtUtc = f.Clock.GetUtcNow() });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }
    [PostgresFact]
    public async Task Credit_contract_and_conversion_ratio_survive_product_edit()
    {
        var admin = await Admin(); var product = await CreditProduct(admin); var a = await f.RegisterOwner(); var pending = await BuyCredit(a, product.Id);
        Assert.Equal(HttpStatusCode.OK, (await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/ai-credit-packages/{product.Id}",
            new CreditPackageUpdateRequest(new("Changed synthetic product", 100m, 987654m, "IRT", false, false, 0), product.RowVersion))).StatusCode);
        await Verify(a, pending.Payment.Id); Assert.Equal(10m, (await Wallet(a)).Balance);
        await f.Db(async db => { var pay = await db.Payments.SingleAsync(x => x.Id == pending.Payment.Id); Assert.Equal(123456m, pay.Amount); Assert.Equal(12345.6m, pay.EffectiveCreditRialToIrtSnapshot); });
    }
    [PostgresFact]
    public async Task Concurrent_start_of_same_interview_reserves_only_once()
    {
        var a = await f.RegisterOwner(); await f.FundInterviewsAsync(a); var invite = await Invite(a); using var first = Candidate(invite); using var second = Candidate(invite);
        // Both starts use the same snapshot; the later UI response may race on the session version after generating audio.
        var responses = await Task.WhenAll(first.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/start", null), second.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/start", null));
        Assert.Contains(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.All(responses, x => Assert.Contains(x.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        await f.Db(async db => { Assert.Equal(1, await db.AiCreditReservations.CountAsync(x => x.InterviewSessionId == invite.InterviewId)); Assert.Equal(1, await db.InterviewBillings.CountAsync(x => x.InterviewSessionId == invite.InterviewId)); });
        Assert.Equal(3m, (await Wallet(a)).ReservedCredit);
    }
    [PostgresFact]
    public async Task Bill_rate_is_preserved_after_package_edit_and_ledger_equals_wallet()
    {
        var a = await f.RegisterOwner(); await f.FundInterviewsAsync(a); var invite = await Invite(a); using var c = Candidate(invite);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/start", null)).StatusCode);
        var admin = await Admin(); var package = await SeedPackage("advanced");
        // Edit and restore the catalog without changing any existing purchase snapshot.
        var changed = (await (await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/packages/{package.Id}", new PackageUpdateRequest(Request(package, 0.9m), package.RowVersion))).Content.ReadFromJsonAsync<RialPackageDto>(BillingJson.Options))!;
        try
        {
            await f.Db(async db => Assert.Equal(0.05m, (await db.InterviewBillings.SingleAsync(x => x.InterviewSessionId == invite.InterviewId)).RatePerMinuteUsd));
            Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null)).StatusCode);
            var org = await Org(a); await f.Db(async db => { var w = await db.OrganizationAiWallets.SingleAsync(x => x.OrganizationId == org); Assert.Equal(w.Balance, await db.AiCreditTransactions.Where(x => x.OrganizationId == org).SumAsync(x => x.Amount)); });
        }
        finally { Assert.Equal(HttpStatusCode.OK, (await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/packages/{package.Id}", new PackageUpdateRequest(Request(package), changed.RowVersion))).StatusCode); }
    }
    [PostgresFact]
    public async Task Basic_hr_interview_does_not_use_advanced_analysis_or_adaptive_llm()
    {
        var a = await f.RegisterOwner(); var p = await SeedPackage("basic"); var purchase = await Buy(a, p.Periods[0].Id); await Verify(a, purchase.Payment.Id);
        await using (var scope = f.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<WalletService>().AdjustAsync(await Org(a), new(Guid.NewGuid(), 5m, "Synthetic HR credit"), default);
        var invite = await Invite(a, "hr_interview"); using var c = Candidate(invite); var calls = f.InterviewAi.LlmCalls;
        var started = (await (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/start", null)).Content.ReadFromJsonAsync<CandidateInterviewDto>(BillingJson.Options))!;
        using var form = new MultipartFormDataContent(); var audio = new ByteArrayContent("ID3-synthetic-hr-answer"u8.ToArray()); audio.Headers.ContentType = new("audio/mpeg");
        form.Add(audio, "audio", "answer.mp3"); form.Add(new StringContent(started.Question!.Id.ToString()), "questionId");
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/answers", form)).StatusCode);
        Assert.Equal(calls, f.InterviewAi.LlmCalls);
        await f.Db(async db => { var s = await db.InterviewSessions.SingleAsync(x => x.Id == invite.InterviewId); Assert.Empty(s.Evidence); Assert.Empty(s.CoveredTopics); });
    }
    [PostgresFact]
    public async Task Media_access_expires_using_purchased_retention_snapshot()
    {
        var a = await f.RegisterOwner(); await f.FundInterviewsAsync(a); var invite = await Invite(a); using var c = Candidate(invite);
        var started = (await (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/start", null)).Content.ReadFromJsonAsync<CandidateInterviewDto>(BillingJson.Options))!;
        Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/questions/{started.Question!.Id}/audio")).StatusCode);
        f.Clock.Offset = TimeSpan.FromDays(61);
        try
        {
            Assert.Equal(HttpStatusCode.NotFound, (await a.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/questions/{started.Question!.Id}/audio")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await a.Client.Send(HttpMethod.Post, $"/api/interviews/{invite.InterviewId}/revoke")).StatusCode);
            // Late cancellation must not restore media whose retention window already elapsed.
            Assert.Equal(HttpStatusCode.NotFound, (await a.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/questions/{started.Question!.Id}/audio")).StatusCode);
        }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
}


