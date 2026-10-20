using System.Security.Claims;
using HireYar.Application.Common;
using HireYar.Application.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HireYar.Api.Controllers;
[ApiController,Route("api/sales"),Authorize(Policy="SalesAgent"),EnableRateLimiting("tickets")]
public sealed class SalesController(SalesService sales,ISalesLeads leads) : ControllerBase
{
    private Guid Agent => Guid.Parse(User.FindFirstValue("sub")!);
    [HttpGet("dashboard")] public Task<SalesStatsDto> Dashboard(CancellationToken ct) => sales.StatsAsync(Agent,ct);
    [HttpGet("leads")] public Task<Page<SalesLeadDto>> Leads([FromQuery] SalesLeadFilter filter,[FromQuery] PageQuery page,CancellationToken ct) => sales.LeadsAsync(Agent,filter,page,ct);
    [HttpPost("leads")] public Task<SalesLeadDto> Create(SalesLeadRequest r,CancellationToken ct) => leads.CreateAsync(Agent,r,ct);
    [HttpPut("leads/{id:guid}/follow-up")] public async Task<IActionResult> FollowUp(Guid id,SalesFollowUpRequest r,CancellationToken ct) { await sales.FollowUpAsync(Agent,id,r,ct); return NoContent(); }
    [HttpGet("commissions")] public Task<SalesCommissionPage> Commissions([FromQuery] SalesCommissionFilter filter,[FromQuery] PageQuery page,CancellationToken ct) => sales.CommissionsAsync(Agent,filter,page,ct);
    [HttpGet("payouts")] public Task<Page<SalesPayoutDto>> Payouts([FromQuery] PageQuery page,CancellationToken ct) => sales.PayoutsAsync(Agent,page,ct);
    [HttpPost("payouts")] public Task<SalesPayoutDto> RequestPayout(SalesPayoutRequest r,CancellationToken ct) => sales.RequestPayoutAsync(Agent,r,ct);
}
