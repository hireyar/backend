using HireYar.Application.Common;
using HireYar.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/subscriptions"), Authorize(Roles = "Owner,Recruiter"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SubscriptionsController(ICurrentUser user, SubscriptionService subscriptions, SubscriptionOrderService orders, InterviewUsageService usage) : ControllerBase
{
    /// <summary>سوابق مصرف مصاحبه‌های سازمان، شامل دعوت‌های لغوشده و منقضی‌شده.</summary>
    [HttpGet("usage"), Authorize]
    public Task<Page<InterviewUsageDto>> Usage([FromQuery] InterviewUsageQuery query, CancellationToken ct) => usage.ListAsync(user.OrganizationId, query, ct);
    /// <summary>جزئیات مصرف و رویدادهای ماندگار یک مصاحبه.</summary>
    [HttpGet("usage/{id:guid}"), Authorize]
    public Task<InterviewUsageDetailDto> UsageDetail(Guid id, CancellationToken ct) => usage.GetAsync(user.OrganizationId, id, ct);
    [HttpGet("current")] public Task<SubscriptionStateDto> Current(CancellationToken ct) => subscriptions.StateAsync(user.OrganizationId, ct);
    [HttpGet("orders"), Authorize] public Task<Page<SubscriptionOrderDto>> Orders([FromQuery] PageQuery page, CancellationToken ct) => orders.ListAsync(user.OrganizationId, page, ct);
    [HttpGet("orders/{id:guid}"), Authorize] public Task<SubscriptionOrderDto> Order(Guid id, CancellationToken ct) => orders.GetAsync(user.OrganizationId, id, ct);
    [HttpPost("orders"), Authorize] public Task<SubscriptionOrderDto> Create(CreateSubscriptionOrderRequest r, CancellationToken ct) => orders.CreateAsync(user.OrganizationId, r, ct);
    [HttpPost("orders/{id:guid}/cancel"), Authorize]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) { await orders.CancelAsync(user.OrganizationId, id, ct); return NoContent(); }
}
