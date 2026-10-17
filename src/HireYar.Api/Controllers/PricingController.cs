using HireYar.Application.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/pricing"), AllowAnonymous]
public sealed class PricingController(PricingService service, IAiDisplayRateProvider displayRate) : ControllerBase
{
    [HttpGet("ai-display-rate"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<AiDisplayRateDto> AiDisplayRate(CancellationToken ct) => displayRate.GetAsync(ct);
    [HttpGet("packages")] public async Task<object> Packages(CancellationToken ct) => new { Packages = await service.ListAsync(true, ct) };
    [HttpGet("ai-credit-packages")] public async Task<object> Credits(CancellationToken ct) => new { Packages = await service.CreditsAsync(true, ct) };
}
