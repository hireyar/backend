using System.Security.Claims;
using HireYar.Application.Common;
using HireYar.Application.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HireYar.Api.Controllers;
[ApiController,Route("api/admin/sales"),Authorize(Policy="SystemAdmin"),EnableRateLimiting("tickets")]
public sealed class AdminSalesController(SalesService sales,ISalesAccounts accounts) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue("sub")!);
    [HttpGet("dashboard")] public Task<SalesStatsDto> Dashboard(CancellationToken ct) => sales.StatsAsync(null,ct);
    [HttpGet("agents")] public Task<Page<SalesAgentDto>> Agents([FromQuery] PageQuery page,CancellationToken ct) => accounts.ListAsync(page,ct);
    [HttpPost("agents")] public Task<SalesCredentialsDto> Create(SalesAgentRequest r,CancellationToken ct) => accounts.CreateAsync(Actor,r,ct);
    [HttpPut("agents/{id:guid}")] public Task<SalesAgentDto> Update(Guid id,SalesAgentRequest r,CancellationToken ct) => accounts.UpdateAsync(Actor,id,r,ct);
    [HttpPost("agents/{id:guid}/reset-password")] public Task<SalesCredentialsDto> ResetPassword(Guid id,CancellationToken ct) => accounts.ResetPasswordAsync(Actor,id,ct);
    [HttpPost("agents/{id:guid}/transfer")] public Task<int> Transfer(Guid id,SalesTransferRequest r,CancellationToken ct) => sales.TransferAsync(Actor,id,r,ct);
    [HttpGet("leads")] public Task<Page<SalesLeadDto>> Leads([FromQuery] SalesLeadFilter filter,[FromQuery] PageQuery page,CancellationToken ct) => sales.LeadsAsync(filter.AgentId,filter,page,ct);
    [HttpGet("commissions")] public Task<SalesCommissionPage> Commissions([FromQuery] SalesCommissionFilter filter,[FromQuery] PageQuery page,CancellationToken ct) => sales.CommissionsAsync(filter.AgentId,filter,page,ct);
    [HttpGet("payouts")] public Task<Page<SalesPayoutDto>> Payouts([FromQuery] Guid? agentId,[FromQuery] PageQuery page,CancellationToken ct) => sales.PayoutsAsync(agentId,page,ct);
    [HttpPost("payouts/{id:guid}/review")] public Task<SalesPayoutDto> Review(Guid id,SalesPayoutReviewRequest r,CancellationToken ct) => sales.ReviewAsync(Actor,id,r,ct);
    [HttpGet("audits")] public Task<Page<SalesAuditDto>> Audits([FromQuery] PageQuery page,CancellationToken ct) => sales.AuditsAsync(page,ct);
}
