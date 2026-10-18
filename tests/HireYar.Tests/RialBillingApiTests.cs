using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Api.Billing;
using HireYar.Application.Billing;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class RialBillingApiTests : IAsyncLifetime
{
    private readonly ApiFixture f = new();
    public Task InitializeAsync() => f.InitializeAsync();
    public Task DisposeAsync() => ((IAsyncLifetime)f).DisposeAsync();

    [PostgresFact]
    public async Task Http_wallet_panel_history_usage_catalog_and_admin_are_rial_while_database_stays_rial()
    {
        f.DisplayRates.RialPerUsdt = 650005m;
        var owner = await f.RegisterOwner(); await f.FundInterviewsAsync(owner);
        var wallet = await Read<RialWalletDto>(owner.Client, "/api/billing/ai-credit");
        Assert.Equal("IRR", wallet.Currency); Assert.Equal(100m, wallet.Balance);
        var panel = await Read<RialBillingPanelDto>(owner.Client, "/api/billing/panel");
        Assert.Equal(wallet.Balance, panel.Wallet.Balance);
        Assert.Null(panel.AiRatePerMinuteRial);
        Assert.Null(panel.EstimatedAvailableMinutes);
        var subscription = await Read<RialSubscriptionOverviewDto>(owner.Client, "/api/billing/subscription");
        Assert.Equal("IRR", subscription.Current!.Currency);
        Assert.Equal(panel.AiRatePerMinuteRial, subscription.Current.AiRatePerMinuteRial);
        var transactions = await Read<HireYar.Application.Common.Page<RialTransactionDto>>(owner.Client, "/api/billing/ai-credit/transactions");
        Assert.Equal(100m, Assert.Single(transactions.Items).Amount);
        Assert.Equal("IRR", transactions.Items[0].Currency);
        var history = await Read<RialFinancialHistoryDto>(owner.Client, "/api/billing/financial-history");
        Assert.All(history.Transactions.Items, x => Assert.Equal("IRR", x.Currency));
        Assert.Equal(100m, Assert.Single(history.Transactions.Items, x => x.TransactionType == "adjustment").Amount);
        var usage = await Read<HireYar.Application.Common.Page<RialUsageDto>>(owner.Client, "/api/billing/ai-usage");
        Assert.Empty(usage.Items);
        var catalog = await Read<RialPackageCatalogDto>(f.Browser(), "/api/pricing/packages");
        var basic = catalog.Packages.Single(x => x.Code == "basic");
        Assert.Equal(19500.15m, basic.AiPricePerMinuteRial);
        Assert.Equal(30000000m, basic.Periods.Single(x => x.Months == 1).Price);
        var admin = await f.RegisterAdmin();
        Guid org = default;
        await f.Db(async db => {
            org = (await db.Users.SingleAsync(x => x.Id == owner.Id)).OrganizationId;
            Assert.Equal(100m, (await db.OrganizationAiWallets.SingleAsync(x => x.OrganizationId == org)).Balance);
        });
        var adminWallet = await Read<RialWalletDto>(admin.Client, $"/api/admin/pricing/organizations/{org}/ai-credit");
        Assert.Equal(wallet.Balance, adminWallet.Balance);
        f.DisplayRates.RialPerUsdt = 700000m;
        Assert.Equal(100m, (await Read<RialWalletDto>(owner.Client, "/api/billing/ai-credit")).Balance);
        await f.Db(async db => Assert.Equal(100m, (await db.OrganizationAiWallets.SingleAsync(x => x.OrganizationId == org)).Balance));
    }

    [PostgresFact]
    public async Task Paid_cash_and_invoice_are_rial_and_fixed_when_exchange_rate_changes()
    {
        f.DisplayRates.RialPerUsdt = 650005m;
        var owner = await f.RegisterOwner(); var admin = await f.RegisterAdmin();
        var created = await admin.Client.Send(HttpMethod.Post, "/api/admin/pricing/ai-credit-packages", new CreditPackageRequest("Rial test", 10, 500000, "IRT", true, true, 0));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var product = (await created.Content.ReadFromJsonAsync<RialCreditPackageDto>(BillingJson.Options))!;
        Assert.Equal(10m, product.CreditAmountRial); Assert.Equal(5000000m, product.SellPriceAmount);
        var buy = await owner.Client.Send(HttpMethod.Post, "/api/billing/ai-credit/purchases", new CreditPurchaseRequest(product.Id));
        Assert.Equal(HttpStatusCode.OK, buy.StatusCode);
        var checkout = (await buy.Content.ReadFromJsonAsync<RialPurchaseResult>(BillingJson.Options))!;
        Assert.Equal("IRR", checkout.Payment.Currency); Assert.Equal(5000000m, checkout.Payment.Amount);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Post, $"/api/billing/payments/{checkout.Payment.Id}/verify")).StatusCode);
        var invoice = await Read<RialInvoiceDto>(owner.Client, $"/api/billing/payments/{checkout.Payment.Id}/invoice");
        Assert.Equal(5000000m, invoice.TotalAmount); Assert.Equal(10m, invoice.CreditAmountRial);
        f.DisplayRates.RialPerUsdt = 700000m;
        var updated = await Read<RialInvoiceDto>(owner.Client, $"/api/billing/payments/{checkout.Payment.Id}/invoice");
        Assert.Equal(invoice.TotalAmount, updated.TotalAmount); Assert.Equal(10m, updated.CreditAmountRial);
        await f.Db(async db => {
            var pay = await db.Payments.SingleAsync(x => x.Id == checkout.Payment.Id);
            Assert.Equal("IRT", pay.Currency); Assert.Equal(500000m, pay.Amount); Assert.Equal(10m, pay.CreditAmountRialSnapshot);
        });
        f.DisplayRates.Unavailable = true;
        Assert.Equal(5000000m, (await Read<RialPaymentDto>(owner.Client, $"/api/billing/payments/{checkout.Payment.Id}")).Amount);
    }

    [PostgresFact]
    public async Task Rial_adjustments_work_without_exchange_quote_and_auth_guards_remain()
    {
        var owner = await f.RegisterOwner(); var admin = await f.RegisterAdmin();
        Guid org = default;
        await f.Db(async db => org = (await db.Users.SingleAsync(x => x.Id == owner.Id)).OrganizationId);
        f.DisplayRates.Unavailable = true;
        var response = await admin.Client.Send(HttpMethod.Post, $"/api/admin/pricing/organizations/{org}/ai-credit/adjustments", new AdjustmentRequest(Guid.NewGuid(), 2, "test"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2m, (await response.Content.ReadFromJsonAsync<RialWalletDto>(BillingJson.Options))!.Balance);
        await f.Db(async db => Assert.Equal(2m, (await db.AiCreditTransactions.SingleAsync(x => x.OrganizationId == org)).Amount));
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Browser().Send(HttpMethod.Get, "/api/billing/ai-credit")).StatusCode);
    }

    private static async Task<T> Read<T>(Browser browser, string url)
    {
        var response = await browser.Send(HttpMethod.Get, url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        return (await response.Content.ReadFromJsonAsync<T>(BillingJson.Options))!;
    }
}
