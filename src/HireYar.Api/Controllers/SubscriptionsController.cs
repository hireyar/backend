using HireYar.Application.Common;
using HireYar.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/subscriptions"), Authorize(Roles = "Owner,Recruiter"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SubscriptionsController(ICurrentUser user, SubscriptionService subscriptions, SubscriptionOrderService orders) : ControllerBase
{
    [HttpGet("current")] public Task<SubscriptionStateDto> Current(CancellationToken ct) => subscriptions.StateAsync(user.OrganizationId, ct);
    [HttpGet("orders"), Authorize(Roles = "Owner")] public Task<Page<SubscriptionOrderDto>> Orders([FromQuery] PageQuery page, CancellationToken ct) => orders.ListAsync(user.OrganizationId, page, ct);
    [HttpGet("orders/{id:guid}"), Authorize(Roles = "Owner")] public Task<SubscriptionOrderDto> Order(Guid id, CancellationToken ct) => orders.GetAsync(user.OrganizationId, id, ct);
    [HttpPost("orders"), Authorize(Roles = "Owner")] public Task<SubscriptionOrderDto> Create(CreateSubscriptionOrderRequest r, CancellationToken ct) => orders.CreateAsync(user.OrganizationId, r, ct);
    [HttpPost("orders/{id:guid}/cancel"), Authorize(Roles = "Owner")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) { await orders.CancelAsync(user.OrganizationId, id, ct); return NoContent(); }
}
