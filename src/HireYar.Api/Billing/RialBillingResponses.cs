using HireYar.Application.Billing;
using HireYar.Application.Common;

namespace HireYar.Api.Billing;

// Resolve exchange quotes before mutations and outside organization database locks.
public sealed class RialBillingResponses(IAiDisplayRateProvider rates)
{
    public async Task<RialPackageDto> ConvertAsync(Func<Task<PackageDto>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = await rates.GetAsync(ct);
        return RialBillingOutput.Map(await action(), rate);
    }

    public async Task<RialCreditPackageDto> ConvertAsync(Func<Task<CreditPackageDto>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = await rates.GetAsync(ct);
        return RialBillingOutput.Map(await action(), rate);
    }

    public async Task<RialWalletDto> ConvertAsync(Func<Task<WalletDto>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = await rates.GetAsync(ct);
        return RialBillingOutput.Map(await action(), rate);
    }

    public async Task<RialPaymentDto> ConvertAsync(Func<Task<PaymentDto>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = null;
        return RialBillingOutput.Map(await action(), rate);
    }

    public async Task<RialPurchaseResult> ConvertAsync(Func<Task<PurchaseResult>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = null;
        return RialBillingOutput.Map(await action(), rate);
    }

    public async Task<RialSubscriptionOverviewDto> ConvertAsync(Func<Task<SubscriptionOverviewDto>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = await rates.GetAsync(ct);
        return RialBillingOutput.Map(await action(), rate);
    }

    public async Task<RialSubscriptionPurchaseDto> ConvertAsync(Func<Task<SubscriptionPurchaseDto>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = await rates.GetAsync(ct);
        return RialBillingOutput.Map(await action(), rate);
    }

    public async Task<RialBillingPanelDto> ConvertAsync(Func<Task<BillingPanelDto>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = await rates.GetAsync(ct);
        return RialBillingOutput.Map(await action(), rate);
    }

    public async Task<RialCheckoutSummaryDto> ConvertAsync(Func<Task<CheckoutSummaryDto>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = await rates.GetAsync(ct);
        return RialBillingOutput.Map(await action(), rate);
    }

    public async Task<RialPanelCheckoutResult> ConvertAsync(Func<Task<PanelCheckoutResult>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = await rates.GetAsync(ct);
        return RialBillingOutput.Map(await action(), rate);
    }

    public async Task<RialFinancialHistoryDto> ConvertAsync(Func<Task<FinancialHistoryDto>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = await rates.GetAsync(ct);
        return RialBillingOutput.Map(await action(), rate);
    }

    public async Task<RialInvoiceDto> ConvertAsync(Func<Task<InvoiceDto>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = await rates.GetAsync(ct);
        return RialBillingOutput.Map(await action(), rate);
    }

    public async Task<RialTeamSeatStateDto> ConvertAsync(Func<Task<TeamSeatStateDto>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = null;
        return RialBillingOutput.Map(await action(), rate);
    }

    public async Task<RialTeamSeatQuoteDto> ConvertAsync(Func<Task<TeamSeatQuoteDto>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = null;
        return RialBillingOutput.Map(await action(), rate);
    }

    public async Task<RialTeamSeatCheckoutDto> ConvertAsync(Func<Task<TeamSeatCheckoutDto>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = null;
        return RialBillingOutput.Map(await action(), rate);
    }

    public async Task<Page<RialPackageDto>> ConvertAsync(Func<Task<Page<PackageDto>>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = await rates.GetAsync(ct);
        var page = await action();
        return new(page.Items.Select(x => RialBillingOutput.Map(x, rate)).ToArray(), page.Total, page.PageNumber, page.PageSize);
    }

    public async Task<Page<RialCreditPackageDto>> ConvertAsync(Func<Task<Page<CreditPackageDto>>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = await rates.GetAsync(ct);
        var page = await action();
        return new(page.Items.Select(x => RialBillingOutput.Map(x, rate)).ToArray(), page.Total, page.PageNumber, page.PageSize);
    }

    public async Task<Page<RialTransactionDto>> ConvertAsync(Func<Task<Page<TransactionDto>>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = await rates.GetAsync(ct);
        var page = await action();
        return new(page.Items.Select(x => RialBillingOutput.Map(x, rate)).ToArray(), page.Total, page.PageNumber, page.PageSize);
    }

    public async Task<Page<RialUsageDto>> ConvertAsync(Func<Task<Page<UsageDto>>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = await rates.GetAsync(ct);
        var page = await action();
        return new(page.Items.Select(x => RialBillingOutput.Map(x, rate)).ToArray(), page.Total, page.PageNumber, page.PageSize);
    }

    public async Task<Page<RialPaymentDto>> ConvertAsync(Func<Task<Page<PaymentDto>>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = null;
        var page = await action();
        return new(page.Items.Select(x => RialBillingOutput.Map(x, rate)).ToArray(), page.Total, page.PageNumber, page.PageSize);
    }

    public async Task<Page<RialSubscriptionPurchaseDto>> ConvertAsync(Func<Task<Page<SubscriptionPurchaseDto>>> action, CancellationToken ct)
    {
        AiDisplayRateDto? rate = await rates.GetAsync(ct);
        var page = await action();
        return new(page.Items.Select(x => RialBillingOutput.Map(x, rate)).ToArray(), page.Total, page.PageNumber, page.PageSize);
    }

    public async Task<IReadOnlyList<RialPackageDto>> ConvertAsync(Func<Task<IReadOnlyList<PackageDto>>> action, CancellationToken ct)
    {
        var rate = await rates.GetAsync(ct);
        return (await action()).Select(x => RialBillingOutput.Map(x, rate)).ToArray();
    }

    public async Task<IReadOnlyList<RialCreditPackageDto>> ConvertAsync(Func<Task<IReadOnlyList<CreditPackageDto>>> action, CancellationToken ct)
    {
        var rate = await rates.GetAsync(ct);
        return (await action()).Select(x => RialBillingOutput.Map(x, rate)).ToArray();
    }

}

