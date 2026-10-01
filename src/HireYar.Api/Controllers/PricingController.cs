using HireYar.Application.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/pricing"), AllowAnonymous]
public sealed class PricingController(PricingService service) : ControllerBase
{
    [HttpGet("packages")] public async Task<object> Packages(CancellationToken ct) => new { Packages = await service.ListAsync(true, ct) };
    [HttpGet("ai-credit-packages")] public async Task<object> Credits(CancellationToken ct) => new { Packages = await service.CreditsAsync(true, ct) };
}
