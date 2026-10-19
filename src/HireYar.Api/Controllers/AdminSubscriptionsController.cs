using System.Security.Claims;
using HireYar.Application.Common;
using HireYar.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/admin/subscriptions"), Authorize(Policy = "SystemAdmin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminSubscriptionsController(CatalogService catalog, SubscriptionOrderService orders) : ControllerBase
{
    [HttpGet("plan")] public Task<PlanDto> Plan(CancellationToken ct) => catalog.ReadAsync(true, ct);
    [HttpPut("plan")] public Task<PlanDto> Update(PlanUpdateRequest r, CancellationToken ct) => catalog.UpdateAsync(r, ct);
    [HttpPut("bundles/{code}")] public Task<PlanDto> Bundle(string code, BundleUpdateRequest r, CancellationToken ct) => catalog.UpdateBundleAsync(code, r, ct);
    [HttpGet("orders")] public Task<Page<SubscriptionOrderDto>> Orders([FromQuery] PageQuery page, [FromQuery] Guid? organizationId, CancellationToken ct) => orders.ListAsync(organizationId, page, ct);
    [HttpPost("orders/{id:guid}/confirm")] public Task<SubscriptionOrderDto> Confirm(Guid id, ConfirmSubscriptionOrderRequest r, CancellationToken ct)
        => orders.ConfirmAsync(id, Guid.Parse(User.FindFirstValue("sub")!), r.PaymentReference, ct);
}
