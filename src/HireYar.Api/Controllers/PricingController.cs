using HireYar.Api.Billing;
using HireYar.Application.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ApiController, Route("api/pricing"), AllowAnonymous]
public sealed class PricingController(PricingService service, IAiDisplayRateProvider displayRate, RialBillingResponses output) : ControllerBase
{
    [HttpGet("ai-display-rate"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<AiDisplayRateDto> AiDisplayRate(CancellationToken ct) => displayRate.GetAsync(ct);
    [HttpGet("packages")] public async Task<RialPackageCatalogDto> Packages(CancellationToken ct) => new(await output.ConvertAsync(() => service.ListAsync(true, ct), ct));
    [HttpGet("ai-credit-packages")] public async Task<RialCreditPackageCatalogDto> Credits(CancellationToken ct) => new(await output.ConvertAsync(() => service.CreditsAsync(true, ct), ct));
}
