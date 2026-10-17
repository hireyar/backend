using HireYar.Api.Billing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Billing;
using HireYar.Application.Interviews;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class BillingPanelTests(ApiFixture f)
{
    private async Task<RialPackageDto> Package(string code) => (await (await f.Browser().Send(HttpMethod.Get, "/api/pricing/packages")).Content.ReadFromJsonAsync<PricingResponse>(BillingJson.Options))!.Packages.Single(x => x.Code == code);
    private async Task<RialPanelCheckoutResult> Checkout(Account a, Guid period, string operation = "purchase")
    {
        var preview = await a.Client.Send(HttpMethod.Post, "/api/billing/subscription/quote", new SubscriptionQuoteRequest(period, operation));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode); var summary = (await preview.Content.ReadFromJsonAsync<RialCheckoutSummaryDto>(BillingJson.Options))!;
        var response = await a.Client.Send(HttpMethod.Post, "/api/billing/subscription/checkout", new SubscriptionQuoteRequest(period, operation, summary.QuoteFingerprint));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<RialPanelCheckoutResult>(BillingJson.Options))!;
    }
    private async Task Verify(Account a, Guid id) => Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Post, $"/api/billing/payments/{id}/verify")).StatusCode);
    private async Task<RialBillingPanelDto> Overview(Account a)
    {
        var response = await a.Client.Send(HttpMethod.Get, "/api/billing/panel"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RialBillingPanelDto>(BillingJson.Options))!;
    }
    private async Task<RialFinancialHistoryDto> History(Account a, string filter = "")
    {
        var response = await a.Client.Send(HttpMethod.Get, "/api/billing/financial-history" + filter); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RialFinancialHistoryDto>(BillingJson.Options))!;
    }
    [PostgresFact]
    public async Task Panel_quote_tax_discount_invoice_and_renewal_use_server_snapshots()
    {
        var a = await f.RegisterOwner(); var none = await Overview(a); Assert.Equal("none", none.SubscriptionState); Assert.Null(none.EstimatedAvailableMinutes);
        var basic = await Package("basic"); var period = basic.Periods.Single(x => x.Months == 3);
        var quote = await a.Client.Send(HttpMethod.Post, "/api/billing/subscription/quote", new SubscriptionQuoteRequest(period.Id));
        Assert.Equal(HttpStatusCode.OK, quote.StatusCode); var summary = (await quote.Content.ReadFromJsonAsync<RialCheckoutSummaryDto>(BillingJson.Options))!;
        Assert.Equal(9000000m * 10m, summary.OriginalAmount); Assert.Equal(450000m * 10m, summary.DiscountAmount);
        Assert.Equal(HttpStatusCode.Conflict, (await a.Client.Send(HttpMethod.Post, "/api/billing/subscription/checkout", new SubscriptionQuoteRequest(period.Id, "purchase", new string('A', 64)))).StatusCode);
        Assert.Equal(8550000m * 10m, summary.SubtotalAmount); Assert.Equal(855000m * 10m, summary.TaxAmount); Assert.Equal(9405000m * 10m, summary.TotalAmount);
        var checkout = await Checkout(a, period.Id); Assert.Equal(summary.TotalAmount, checkout.Checkout.Payment.Amount);
        Assert.Empty((await History(a)).Transactions.Items);
        var stranger = await f.RegisterOwner();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Get, $"/api/billing/payments/{checkout.Checkout.Payment.Id}/invoice")).StatusCode);
        await Verify(a, checkout.Checkout.Payment.Id);
        var invoice = await (await a.Client.Send(HttpMethod.Get, $"/api/billing/payments/{checkout.Checkout.Payment.Id}/invoice")).Content.ReadFromJsonAsync<RialInvoiceDto>(BillingJson.Options);
        Assert.Equal(9405000m * 10m, invoice!.TotalAmount); Assert.Equal(855000m * 10m, invoice.TaxAmount); Assert.StartsWith("HY-", invoice.InvoiceNumber);
        var overview = await Overview(a); Assert.Equal("active", overview.SubscriptionState); Assert.Equal(0.03m, overview.AiRatePerMinuteRial); Assert.Equal(0m, overview.EstimatedAvailableMinutes);
        var renewal = await Checkout(a, period.Id, "renewal"); await Verify(a, renewal.Checkout.Payment.Id);
        var after = await Overview(a); Assert.Equal(overview.Current!.ExpiresAtUtc, Assert.Single(after.Scheduled).StartedAtUtc);
        var history = await History(a, "?creditType=subscription&transactionType=payment&pageSize=1");
        Assert.Single(history.Transactions.Items); Assert.Equal(2, history.Transactions.Total); Assert.Equal(18810000m * 10m, history.Totals.SubscriptionPaidRial);
        Assert.Empty((await History(stranger)).Transactions.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Get, "/api/billing/financial-history?creditType=other")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Get, "/api/billing/financial-history?fromUtc=2026-10-08T00:00:00Z&toUtc=2026-10-01T00:00:00Z")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Post, "/api/billing/subscription/checkout", new { packagePeriodId = period.Id, operation = "purchase", totalAmount = 1 })).StatusCode);
    }
    [PostgresFact]
    public async Task Upgrade_prorates_and_replaces_once_without_mutating_original_contract()
    {
        var a = await f.RegisterOwner(); var basic = await Package("basic"); var advanced = await Package("advanced");
        var oldPurchase = await Checkout(a, basic.Periods.Single(x => x.Months == 1).Id); await Verify(a, oldPurchase.Checkout.Payment.Id);
        var original = (await Overview(a)).Current!;
        f.Clock.Offset = TimeSpan.FromDays(10);
        try
        {
            var request = new SubscriptionQuoteRequest(advanced.Periods.Single(x => x.Months == 1).Id, "upgrade");
            var upgrade = await Checkout(a, request.PackagePeriodId, "upgrade");
            Assert.Equal(original.Id, upgrade.Summary.ReplacesSubscriptionId); Assert.True(upgrade.Summary.ProratedCreditAmount > 0);
            var totalDays = (decimal)Math.Ceiling((original.ExpiresAtUtc - original.StartedAtUtc).TotalDays);
            var remaining = (decimal)Math.Floor((original.ExpiresAtUtc - upgrade.Checkout.Payment.CreatedAtUtc).TotalDays);
            Assert.Equal(decimal.Floor(3000000m * remaining / totalDays) * 10m, upgrade.Summary.ProratedCreditAmount);
            var taxable = 4500000m - upgrade.Summary.ProratedCreditAmount / 10m;
            Assert.Equal((taxable + decimal.Round(taxable * 0.1m, 0, MidpointRounding.AwayFromZero)) * 10m, upgrade.Checkout.Payment.Amount);
            Assert.Equal(HttpStatusCode.Conflict, (await a.Client.Send(HttpMethod.Post, "/api/billing/subscription/purchases", new SubscriptionPurchaseRequest(basic.Periods[0].Id))).StatusCode);
            await Task.WhenAll(Verify(a, upgrade.Checkout.Payment.Id), Verify(a, upgrade.Checkout.Payment.Id));
            var after = await Overview(a); Assert.Equal("advanced", after.Current!.PackageCode); Assert.Equal(0.05m, after.AiRatePerMinuteRial);
            Assert.Empty(after.Scheduled); Assert.True(after.Current.StartedAtUtc < original.ExpiresAtUtc);
            await f.Db(async db =>
            {
                Assert.Equal(original.ExpiresAtUtc, (await db.OrganizationSubscriptions.SingleAsync(x => x.Id == original.Id)).ExpiresAtUtc);
                var replaced = await db.SubscriptionReplacements.SingleAsync(x => x.SubscriptionId == original.Id);
                Assert.Equal(upgrade.Checkout.Payment.Id, replaced.PaymentId);
                await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"SubscriptionReplacements\" WHERE \"Id\" = {replaced.Id}"));
            });
            var invoice = await (await a.Client.Send(HttpMethod.Get, $"/api/billing/payments/{upgrade.Checkout.Payment.Id}/invoice")).Content.ReadFromJsonAsync<RialInvoiceDto>(BillingJson.Options);
            Assert.Equal(4500000m * 10m, invoice!.SubtotalAmount); Assert.Equal(upgrade.Summary.ProratedCreditAmount, invoice.ProratedCreditAmount);
        }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
    [PostgresFact]
    public async Task Failed_upgrade_keeps_original_and_scheduled_queue_is_not_lost()
    {
        var a = await f.RegisterOwner(); var basic = await Package("basic"); var advanced = await Package("advanced");
        var old = await Checkout(a, basic.Periods[0].Id); await Verify(a, old.Checkout.Payment.Id);
        var upgrade = await Checkout(a, advanced.Periods[0].Id, "upgrade");
        f.PaymentGateway.Results[upgrade.Checkout.Payment.Id] = new(false, null, upgrade.Checkout.Payment.Amount / 10m, "IRT");
        await Verify(a, upgrade.Checkout.Payment.Id); Assert.Equal("basic", (await Overview(a)).Current!.PackageCode);
        var renewal = await Checkout(a, basic.Periods[0].Id, "renewal"); await Verify(a, renewal.Checkout.Payment.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await a.Client.Send(HttpMethod.Post, "/api/billing/subscription/quote", new SubscriptionQuoteRequest(advanced.Periods[0].Id, "upgrade"))).StatusCode);
        Assert.Single((await Overview(a)).Scheduled);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Post, "/api/billing/subscription/quote", new SubscriptionQuoteRequest(advanced.Periods[0].Id, "renewal"))).StatusCode);
    }
    [PostgresFact]
    public async Task Credit_purchase_history_is_once_and_uses_paid_time_without_changing_subscription()
    {
        var a = await f.RegisterOwner(); var admin = await f.RegisterAdmin();
        var product = (await (await admin.Client.Send(HttpMethod.Post, "/api/admin/pricing/ai-credit-packages", new CreditPackageRequest("Synthetic fifty dollars", 50m, 5000000m, "IRT", true, true, 0))).Content.ReadFromJsonAsync<RialCreditPackageDto>(BillingJson.Options))!;
        var credit = (await (await a.Client.Send(HttpMethod.Post, "/api/billing/ai-credit/purchases", new CreditPurchaseRequest(product.Id))).Content.ReadFromJsonAsync<RialPurchaseResult>(BillingJson.Options))!;
        Assert.Empty((await History(a)).Transactions.Items); var since = DateTimeOffset.UtcNow.AddMinutes(30);
        f.Clock.Offset = TimeSpan.FromHours(1);
        try
        {
            await Task.WhenAll(Verify(a, credit.Payment.Id), Verify(a, credit.Payment.Id));
            var history = await History(a, "?creditType=aiCredit&transactionType=payment&fromUtc=" + Uri.EscapeDataString(since.ToString("O")));
            var item = Assert.Single(history.Transactions.Items); Assert.Equal(50m, item.Amount); Assert.Equal("IRR", item.Currency); Assert.Equal(5000000m * 10m, item.CashPaidRial);
            Assert.Equal(50m, history.Totals.CreditPurchasedRial); Assert.Equal(0m * 10m, history.Totals.SubscriptionPaidRial);
            var overview = await Overview(a); Assert.Equal("none", overview.SubscriptionState); Assert.Equal(50m, overview.Wallet.Balance); Assert.Null(overview.EstimatedAvailableMinutes);
        }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
    [PostgresFact]
    public async Task Panel_estimate_excludes_reservations_and_history_contains_billed_duration_and_candidate()
    {
        f.InterviewAi.Reset(); var a = await f.RegisterOwner(); await f.FundInterviewsAsync(a);
        var job = await (await a.Client.Send(HttpMethod.Post, "/api/panel-wizard/positions", new { title = "Synthetic", description = "Synthetic job" })).Content.ReadFromJsonAsync<JsonElement>(BillingJson.Options);
        var template = await (await a.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates", new { jobPositionId = job.GetProperty("id").GetGuid(), title = "Synthetic", interviewType = "general", welcomeMessage = "Welcome", goodbyeMessage = "Goodbye", questions = Enumerable.Range(1, 5).Select(x => "Synthetic question " + x).ToArray() })).Content.ReadFromJsonAsync<JsonElement>(BillingJson.Options);
        using var hr = f.CreateClient(); hr.DefaultRequestHeaders.Authorization = new("Bearer", a.Client.AccessToken);
        using var form = new MultipartFormDataContent();
        foreach (var field in new Dictionary<string, string> { ["jobPositionId"] = job.GetProperty("id").GetString()!, ["templateId"] = template.GetProperty("id").GetString()!, ["firstName"] = "Synthetic", ["lastName"] = "Candidate", ["email"] = "synthetic@example.test", ["phone"] = "09123456789" }) form.Add(new StringContent(field.Value), field.Key);
        form.Add(new StringContent("Synthetic candidate work history"), "resumeText");
        form.Add(new ByteArrayContent("%PDF-1.7\nSynthetic resume"u8.ToArray()), "resume", "resume.pdf");
        var invitation = (await (await hr.PostAsync("/api/panel-wizard/invitations", form)).Content.ReadFromJsonAsync<PanelInvitationResult>(BillingJson.Options))!;
        using var candidate = f.CreateClient(); candidate.DefaultRequestHeaders.Authorization = new("Bearer", invitation.Invitation.Token);
        var current = (await (await candidate.PostAsync($"/api/candidate/interviews/{invitation.Invitation.InterviewId}/start", null)).Content.ReadFromJsonAsync<CandidateInterviewDto>(BillingJson.Options))!;
        var reserved = await Overview(a); Assert.Equal(3m, reserved.Wallet.ReservedCredit); Assert.Equal(1940m, reserved.EstimatedAvailableMinutes);
        f.Clock.Offset = TimeSpan.FromSeconds(20);
        try
        {
            using var answer = new MultipartFormDataContent(); answer.Add(new StringContent(current.Question!.Id.ToString()), "questionId");
            var audio = new ByteArrayContent(new byte[] { 0x1a, 0x45, 0xdf, 0xa3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }); audio.Headers.ContentType = new("audio/webm"); answer.Add(audio, "audio", "answer.webm");
            Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{invitation.Invitation.InterviewId}/answers", answer)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{invitation.Invitation.InterviewId}/complete", null)).StatusCode);
            var history = await History(a, "?transactionType=usage"); var item = Assert.Single(history.Transactions.Items);
            Assert.Equal("Synthetic Candidate", item.CandidateName); Assert.Equal(invitation.Invitation.InterviewId, item.InterviewId); Assert.True(item.BillableSeconds >= 20);
            Assert.Equal(BillingMath.Cost(item.BillableSeconds!.Value, 0.05m), -item.Amount); Assert.Equal(-item.Amount, history.Totals.CreditConsumedRial);
            Assert.Equal((long)item.BillableSeconds!.Value, history.Totals.BillableSeconds);
        }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
    [PostgresFact]
    public async Task Disabled_gateway_allows_preview_and_read_but_not_paid_checkout()
    {
        var disabled = new ApiFixture { DisabledPayments = true }; await disabled.InitializeAsync();
        try
        {
            var a = await disabled.RegisterOwner(); var basic = await Package("basic");
            var read = await a.Client.Send(HttpMethod.Get, "/api/billing/panel"); Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            var dto = (await read.Content.ReadFromJsonAsync<RialBillingPanelDto>(BillingJson.Options))!; Assert.False(dto.CanPurchase); Assert.False(dto.GatewayEnabled);
            Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Post, "/api/billing/subscription/quote", new SubscriptionQuoteRequest(basic.Periods[0].Id))).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await a.Client.Send(HttpMethod.Post, "/api/billing/subscription/checkout", new SubscriptionQuoteRequest(basic.Periods[0].Id))).StatusCode);
        }
        finally { await ((IAsyncLifetime)disabled).DisposeAsync(); }
    }
}
