using System.Reflection;
using System.Text.Json;
using HireYar.Api.Billing;
using HireYar.Api.Controllers;
using HireYar.Application.Billing;
using HireYar.Application.Common;
using HireYar.Domain.Billing;

namespace HireYar.Tests;

public sealed class RialBillingOutputTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-17T10:00:00Z");
    private static AiDisplayRateDto Rate(decimal value = 650005m) => new("IRR", value, Now, Now.AddSeconds(5), 5);

    [Fact]
    public void Wallet_stays_rial_when_market_rate_changes()
    {
        var source = new WalletDto("IRR", 20, 3, 17);
        var first = RialBillingOutput.Map(source, Rate());
        var second = RialBillingOutput.Map(source, Rate(700000));
        Assert.Equal("IRR", first.Currency);
        Assert.Equal(20m, first.Balance);
        Assert.Equal(3m, first.ReservedCredit);
        Assert.Equal(17m, first.AvailableCredit);
        Assert.Equal(first.Balance - first.ReservedCredit, first.AvailableCredit);
        Assert.Equal(20m, second.Balance);
        Assert.Equal(20, source.Balance);
        Assert.Equal("IRR", source.Currency);
        var json = JsonSerializer.SerializeToElement(first, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(650005m, json.GetProperty("displayRate").GetProperty("rialPerUsdt").GetDecimal());
    }

    [Fact]
    public void Usage_and_ledger_keep_sign_seconds_and_references()
    {
        var id = Guid.NewGuid();
        var usage = RialBillingOutput.Map(new UsageDto(id, .1m, 60, .1m, InterviewBillingStatus.Settled, Now), Rate());
        var tx = RialBillingOutput.Map(new TransactionDto(Guid.NewGuid(), CreditTransactionType.InterviewUsage, -.1m, 1, .9m, "interview", id, "usage", Now), Rate());
        Assert.Equal(.1m, usage.RatePerMinuteRial);
        Assert.Equal(.1m, usage.BilledAmountRial);
        Assert.Equal(60, usage.BillableSeconds);
        Assert.Equal(-usage.BilledAmountRial, tx.Amount);
        Assert.Equal(1m, tx.BalanceBefore);
        Assert.Equal(.9m, tx.BalanceAfter);
        Assert.Equal(id, tx.ReferenceId);
        Assert.Equal("IRR", tx.Currency);
    }

    [Fact]
    public void Catalog_converts_both_cash_and_credit_and_keeps_small_dollar_reference()
    {
        var source = Sample<PackageDto>();
        var result = RialBillingOutput.Map(source, Rate());
        Assert.Equal(1300010m, result.AiPricePerMinuteRial);
        Assert.Equal(2, result.AiPricePerMinuteUsd);
        Assert.Equal(20, result.TeamUserMonthlyPriceRial);
        var period = RialBillingOutput.Map(new PeriodDto(Guid.NewGuid(), 3, 100000, "IRT", 5, true, 0), Rate());
        Assert.Equal(1000000, period.Price);
        Assert.Equal(5, period.DiscountPercent);
        Assert.Equal("IRR", period.Currency);
        var credit = RialBillingOutput.Map(new CreditPackageDto(Guid.NewGuid(), "credit", 50, 5000000, "IRT", true, true, 0, []), Rate());
        Assert.Equal(50, credit.CreditAmountRial);
        Assert.Equal(50000000, credit.SellPriceAmount);
        Assert.Equal("IRR", credit.SellPriceCurrency);
        Assert.Equal(50, credit.CreditAmountRial);
    }

    [Fact]
    public void Panel_and_scheduled_subscriptions_share_one_rate_and_preserve_minutes()
    {
        var sub = Sample<SubscriptionDto>();
        var source = new BillingPanelDto(Now, "active", sub, sub, [sub], 3, new("IRR", 20, 3, 17), .1m, 170, true, true);
        var rate = Rate();
        var result = RialBillingOutput.Map(source, rate);
        Assert.Same(rate, result.DisplayRate);
        Assert.Same(rate, result.Wallet.DisplayRate);
        Assert.Same(rate, result.Current!.DisplayRate);
        Assert.Equal(result.Current.AiRatePerMinuteRial, Assert.Single(result.Scheduled).AiRatePerMinuteRial);
        Assert.Equal(result.Current.AiRatePerMinuteRial, result.LastExpired!.AiRatePerMinuteRial);
        Assert.Equal(65000.5m, result.AiRatePerMinuteRial);
        Assert.Equal(170, result.EstimatedAvailableMinutes);
        var overview = RialBillingOutput.Map(new SubscriptionOverviewDto(sub, [sub]), rate);
        Assert.Equal(1300010, overview.Current!.AiRatePerMinuteRial);
    }

    [Fact]
    public void History_handles_mixed_units_totals_and_pagination_without_rounding()
    {
        var usd = new FinancialHistoryItemDto(Guid.NewGuid(), Now, "usage", "aiCredit", "usage", -.1m, "IRR", null, 60, Guid.NewGuid(), "candidate", "tx1", null);
        var cash = usd with { Id = Guid.NewGuid(), Amount = -100000, Currency = "IRT", CashPaidIrt = 100000, TransactionType = "payment" };
        var source = new FinancialHistoryDto(Now, new([usd, cash], 12, 2, 2), new(100000, 50, .1m, 60, 5000000, 200000));
        var result = RialBillingOutput.Map(source, Rate());
        Assert.Equal(-.1m, result.Transactions.Items[0].Amount);
        Assert.Null(result.Transactions.Items[0].CashPaidRial);
        Assert.Equal(-1000000, result.Transactions.Items[1].Amount);
        Assert.Equal(1000000, result.Transactions.Items[1].CashPaidRial);
        Assert.Equal(1000000, result.Totals.SubscriptionPaidRial);
        Assert.Equal(50, result.Totals.CreditPurchasedRial);
        Assert.Equal(.1m, result.Totals.CreditConsumedRial);
        Assert.Equal(50000000, result.Totals.CreditPaidRial);
        Assert.Equal(2000000, result.Totals.TeamSeatsPaidRial);
        Assert.Equal(60, result.Totals.BillableSeconds);
        Assert.Equal(12, result.Transactions.Total);
        Assert.Equal(2, result.Transactions.PageNumber);
    }

    [Fact]
    public void Invoice_paid_cash_and_rial_credit_stay_fixed()
    {
        var source = new InvoiceDto(Guid.NewGuid(), "invoice", "tx", Now, "AiCredit", "credit", "IRT", 5000000, 0, 0, 0, 5000000, 50, null);
        var first = RialBillingOutput.Map(source, Rate());
        var second = RialBillingOutput.Map(source, Rate(700000));
        Assert.Equal(50000000, first.TotalAmount);
        Assert.Equal(first.TotalAmount, second.TotalAmount);
        Assert.Equal(50, first.CreditAmountRial);
        Assert.Equal(50, second.CreditAmountRial);
        Assert.Equal(5000000, source.TotalAmount);
        Assert.Equal("IRT", source.Currency);
    }

    [Fact]
    public void Checkout_and_invoice_convert_all_cash_fields_but_keep_tax_percent_and_fingerprint()
    {
        var source = Sample<CheckoutSummaryDto>() with { Currency = "IRT" };
        var result = RialBillingOutput.Map(source, Rate());
        Assert.Equal(20, result.OriginalAmount); Assert.Equal(20, result.DiscountAmount);
        Assert.Equal(20, result.SubtotalAmount); Assert.Equal(20, result.ProratedCreditAmount);
        Assert.Equal(20, result.TaxAmount); Assert.Equal(20, result.TotalAmount);
        Assert.Equal(20, result.TeamSeatAmountRial);
        Assert.Equal(2, result.TaxPercent); Assert.Equal(2, result.DiscountPercent);
        Assert.Equal(source.QuoteFingerprint, result.QuoteFingerprint);
        Assert.Equal(source.EstimatedStartsAtUtc, result.EstimatedStartsAtUtc);
        var invoice = Sample<InvoiceDto>() with { Currency = "IRT", Subscription = source, TeamSeats = null };
        var mapped = RialBillingOutput.Map(invoice, Rate());
        Assert.Equal(20, mapped.SubtotalAmount); Assert.Equal(20, mapped.ProratedCreditAmount);
        Assert.Equal(20, mapped.TaxAmount); Assert.Equal(20, mapped.TotalAmount);
        Assert.Equal(2, mapped.TaxPercent);
        Assert.Equal(2, mapped.CreditAmountRial);
        Assert.Equal(result, mapped.Subscription);
        var purchase = RialBillingOutput.Map(Sample<SubscriptionPurchaseDto>() with { Currency = "IRT" }, Rate());
        Assert.Equal(20, purchase.PriceAmount); Assert.Equal(1300010, purchase.AiRatePerMinuteRial);
    }

    [Fact]
    public void Team_seats_and_payment_checkout_use_exact_rial_without_forex()
    {
        var state = RialBillingOutput.Map(Sample<TeamSeatStateDto>(), null);
        var quote = RialBillingOutput.Map(Sample<TeamSeatQuoteDto>(), null);
        Assert.Equal(20, state.TeamUserMonthlyPriceRial);
        Assert.Equal(20, quote.MonthlyPriceRial); Assert.Equal(20, quote.AmountRial);
        Assert.Equal(2, quote.RemainingBillingMonths);
        var pay = Sample<PaymentDto>() with { Currency = "IRT" };
        var checkout = RialBillingOutput.Map(new TeamSeatCheckoutDto(new(pay, null, true), Sample<TeamSeatQuoteDto>()), null);
        Assert.Equal(20, checkout.Checkout.Payment.Amount);
        Assert.Equal("IRR", checkout.Checkout.Payment.Currency);
        Assert.Null(checkout.DisplayRate);
    }

    [Fact]
    public async Task Rial_writes_and_cash_reads_work_without_a_live_exchange_quote()
    {
        var responses = new RialBillingResponses(new FailedRate());
        var mutated = false;
        var adjusted = await responses.ConvertAsync(() => { mutated = true; return Task.FromResult(new WalletDto("IRR", 2, 0, 2)); }, default);
        Assert.True(mutated); Assert.Equal(2m, adjusted.Balance);
        var pay = await responses.ConvertAsync(() => Task.FromResult(Sample<PaymentDto>() with { Currency = "IRT" }), default);
        Assert.Equal(20, pay.Amount);
    }

    [Fact]
    public async Task Admin_package_writes_need_no_live_quote_but_landing_catalog_does()
    {
        var responses = new RialBillingResponses(new FailedRate());
        var edited = await responses.ConvertAsync(() => Task.FromResult(Sample<PackageDto>()), default);
        Assert.Null(edited.AiPricePerMinuteRial);
        Assert.Null(edited.DisplayRate);
        Assert.Equal(2m, edited.AiPricePerMinuteUsd);
        var readCatalog = false;
        await Assert.ThrowsAsync<AppException>(() => responses.ConvertAsync(() => {
            readCatalog = true;
            return Task.FromResult<IReadOnlyList<PackageDto>>([Sample<PackageDto>()]);
        }, default));
        Assert.False(readCatalog);
    }

    [Fact]
    public async Task Paged_results_use_one_snapshot_and_preserve_page_metadata()
    {
        var provider = new FixedRate(); var responses = new RialBillingResponses(provider);
        var source = new Page<TransactionDto>([Sample<TransactionDto>(), Sample<TransactionDto>()], 20, 2, 2);
        var result = await responses.ConvertAsync(() => Task.FromResult(source), default);
        Assert.Equal(0, provider.Calls);
        Assert.Equal(20, result.Total); Assert.Equal(2, result.PageNumber); Assert.Equal(2, result.PageSize);
        Assert.All(result.Items, x => { Assert.Equal(2, x.Amount); Assert.Same(result.Items[0].DisplayRate, x.DisplayRate); });
    }

    [Fact]
    public void All_financial_controller_responses_use_explicit_rial_contracts()
    {
        foreach (var controller in new[] { typeof(BillingController), typeof(PricingController), typeof(AdminPricingController) })
        foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            if (new[] { "AiDisplayRate", "Features", "CreateFeature", "UpdateFeature" }.Contains(method.Name)) continue;
            Assert.Contains("Rial", method.ReturnType.ToString());
        }
        var types = typeof(RialWalletDto).Assembly.GetTypes().Where(t => t.Namespace == "HireYar.Api.Billing" && t.Name.StartsWith("Rial") && t.Name.EndsWith("Dto"));
        foreach (var type in types)
        foreach (var property in type.GetProperties())
        {
            Assert.False(property.Name.EndsWith("Irt"), $"{type.Name}.{property.Name} still uses toman.");
            if (type == typeof(RialPackageDto) || type == typeof(RialCreditPackageDto)) continue;
            Assert.False(property.Name.EndsWith("Usd"), $"{type.Name}.{property.Name} still exposes dollar amounts.");
        }
    }

    private static T Sample<T>() => (T)Sample(typeof(T));
    private static object Sample(Type type)
    {
        var nullable = Nullable.GetUnderlyingType(type); if (nullable is not null) return Sample(nullable);
        if (type == typeof(decimal)) return 2m;
        if (type == typeof(int)) return 2;
        if (type == typeof(long)) return 2L;
        if (type == typeof(bool)) return true;
        if (type == typeof(string)) return "sample";
        if (type == typeof(Guid)) return Guid.NewGuid();
        if (type == typeof(DateTimeOffset)) return Now;
        if (type == typeof(byte[])) return new byte[16];
        if (type.IsEnum) return Enum.GetValues(type).GetValue(0)!;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)) return Array.CreateInstance(type.GetGenericArguments()[0], 0);
        var ctor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        return ctor.Invoke(ctor.GetParameters().Select(p => Sample(p.ParameterType)).ToArray());
    }
    private sealed class FailedRate : IAiDisplayRateProvider
    {
        public Task<AiDisplayRateDto> GetAsync(CancellationToken ct) => throw new AppException(503, "ai_display_rate_unavailable", "unavailable");
    }
    private sealed class FixedRate : IAiDisplayRateProvider
    {
        public int Calls;
        public Task<AiDisplayRateDto> GetAsync(CancellationToken ct) { Calls++; return Task.FromResult(Rate()); }
    }
}
