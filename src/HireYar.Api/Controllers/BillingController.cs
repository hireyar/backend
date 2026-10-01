using HireYar.Application.Billing;
using HireYar.Application.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/billing"), Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Owner,Recruiter")]
public sealed class BillingController(ICurrentUser user, BillingQueryService query, WalletService wallet, PaymentService payments) : ControllerBase
{
    [HttpGet("subscription")] public Task<SubscriptionOverviewDto> Subscription(CancellationToken ct) => query.SubscriptionsAsync(user.OrganizationId, ct);
    [HttpGet("ai-credit")] public Task<WalletDto> Credit(CancellationToken ct) => wallet.GetAsync(user.OrganizationId, ct);
    [HttpGet("ai-credit/transactions")] public Task<Page<TransactionDto>> Transactions([FromQuery] PageQuery page, CancellationToken ct) => query.TransactionsAsync(user.OrganizationId, page, ct);
    [HttpGet("ai-usage")] public Task<Page<UsageDto>> Usage([FromQuery] PageQuery page, CancellationToken ct) => query.UsageAsync(user.OrganizationId, page, ct);
    [HttpGet("payments")] public Task<Page<PaymentDto>> Payments([FromQuery] PageQuery page, CancellationToken ct) => query.PaymentsAsync(user.OrganizationId, page, ct);
    [HttpGet("payments/{id:guid}")] public Task<PaymentDto> Payment(Guid id, CancellationToken ct) => query.PaymentAsync(user.OrganizationId, id, ct);
    [HttpGet("purchases")] public Task<Page<SubscriptionPurchaseDto>> Purchases([FromQuery] PageQuery page, CancellationToken ct) => query.PurchasesAsync(user.OrganizationId, page, ct);
    [HttpPost("subscription/purchases"), Authorize(Roles = "Owner")]
    public Task<PurchaseResult> BuySubscription(SubscriptionPurchaseRequest r, CancellationToken ct) => payments.PurchaseSubscriptionAsync(user.OrganizationId, r.PackagePeriodId, ct);
    [HttpPost("ai-credit/purchases"), Authorize(Roles = "Owner")]
    public Task<PurchaseResult> BuyCredit(CreditPurchaseRequest r, CancellationToken ct) => payments.PurchaseCreditAsync(user.OrganizationId, r.CreditPackageId, ct);
    [HttpPost("payments/{id:guid}/verify"), Authorize(Roles = "Owner")]
    public Task<PaymentDto> Verify(Guid id, CancellationToken ct) => payments.VerifyAsync(user.OrganizationId, id, ct);
}
