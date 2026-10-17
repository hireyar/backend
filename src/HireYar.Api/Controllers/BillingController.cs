using HireYar.Api.Billing;
using HireYar.Application.Billing;
using HireYar.Application.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ApiController, Route("api/billing"), Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Owner")]
public sealed class BillingController(ICurrentUser user, BillingQueryService query, WalletService wallet, PaymentService payments, BillingPanelService panel, TeamSeatService seats, RialBillingResponses output) : ControllerBase
{
    [HttpGet("panel")] public Task<RialBillingPanelDto> Panel(CancellationToken ct) => output.ConvertAsync(() => panel.OverviewAsync(user.OrganizationId, User.IsInRole("Owner"), ct), ct);
    [HttpPost("subscription/quote")] public Task<RialCheckoutSummaryDto> Quote(SubscriptionQuoteRequest r, CancellationToken ct) => output.ConvertAsync(() => panel.QuoteAsync(user.OrganizationId, r, ct), ct);
    [HttpPost("subscription/checkout"), Authorize(Roles = "Owner")]
    public Task<RialPanelCheckoutResult> Checkout(SubscriptionQuoteRequest r, CancellationToken ct) => output.ConvertAsync(() => payments.PurchasePanelSubscriptionAsync(user.OrganizationId, r, ct), ct);
    [HttpGet("financial-history")] public Task<RialFinancialHistoryDto> FinancialHistory([FromQuery] FinancialHistoryQuery r, CancellationToken ct) => output.ConvertAsync(() => panel.HistoryAsync(user.OrganizationId, r, ct), ct);
    [HttpGet("payments/{id:guid}/invoice")] public Task<RialInvoiceDto> Invoice(Guid id, CancellationToken ct) => output.ConvertAsync(() => panel.InvoiceAsync(user.OrganizationId, id, ct), ct);
    [HttpGet("subscription")] public Task<RialSubscriptionOverviewDto> Subscription(CancellationToken ct) => output.ConvertAsync(() => query.SubscriptionsAsync(user.OrganizationId, ct), ct);
    [HttpGet("ai-credit")] public Task<RialWalletDto> Credit(CancellationToken ct) => output.ConvertAsync(() => wallet.GetAsync(user.OrganizationId, ct), ct);
    [HttpGet("ai-credit/transactions")] public Task<Page<RialTransactionDto>> Transactions([FromQuery] PageQuery pagination, CancellationToken ct) => output.ConvertAsync(() => query.TransactionsAsync(user.OrganizationId, pagination, ct), ct);
    [HttpGet("ai-usage")] public Task<Page<RialUsageDto>> Usage([FromQuery] PageQuery pagination, CancellationToken ct) => output.ConvertAsync(() => query.UsageAsync(user.OrganizationId, pagination, ct), ct);
    [HttpGet("payments")] public Task<Page<RialPaymentDto>> Payments([FromQuery] PageQuery pagination, CancellationToken ct) => output.ConvertAsync(() => query.PaymentsAsync(user.OrganizationId, pagination, ct), ct);
    [HttpGet("payments/{id:guid}")] public Task<RialPaymentDto> Payment(Guid id, CancellationToken ct) => output.ConvertAsync(() => query.PaymentAsync(user.OrganizationId, id, ct), ct);
    [HttpGet("purchases")] public Task<Page<RialSubscriptionPurchaseDto>> Purchases([FromQuery] PageQuery pagination, CancellationToken ct) => output.ConvertAsync(() => query.PurchasesAsync(user.OrganizationId, pagination, ct), ct);
    [HttpPost("subscription/purchases"), Authorize(Roles = "Owner")]
    public Task<RialPurchaseResult> BuySubscription(SubscriptionPurchaseRequest r, CancellationToken ct) => output.ConvertAsync(() => payments.PurchaseSubscriptionAsync(user.OrganizationId, r.PackagePeriodId, ct, r.TeamCapacity), ct);
    [HttpGet("team-seats")] public Task<RialTeamSeatStateDto> TeamSeats(CancellationToken ct) => output.ConvertAsync(() => seats.StateAsync(user.OrganizationId, ct), ct);
    [HttpPost("team-seats/quote")] public Task<RialTeamSeatQuoteDto> TeamQuote(TeamSeatQuoteRequest r, CancellationToken ct) => output.ConvertAsync(() => seats.QuoteAsync(user.OrganizationId, r, ct), ct);
    [HttpPost("team-seats/checkout")] public Task<RialTeamSeatCheckoutDto> TeamCheckout(TeamSeatQuoteRequest r, CancellationToken ct) => output.ConvertAsync(() => payments.PurchaseTeamSeatsAsync(user.OrganizationId, r, ct), ct);
    [HttpPost("ai-credit/purchases"), Authorize(Roles = "Owner")]
    public Task<RialPurchaseResult> BuyCredit(CreditPurchaseRequest r, CancellationToken ct) => output.ConvertAsync(() => payments.PurchaseCreditAsync(user.OrganizationId, r.CreditPackageId, ct), ct);
    [HttpPost("payments/{id:guid}/verify"), Authorize(Roles = "Owner")]
    public Task<RialPaymentDto> Verify(Guid id, CancellationToken ct) => output.ConvertAsync(() => payments.VerifyAsync(user.OrganizationId, id, ct), ct);
}
