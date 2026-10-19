using HireYar.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/subscriptions/catalog"), AllowAnonymous, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SubscriptionCatalogController(CatalogService catalog) : ControllerBase
{
    [HttpGet] public Task<PlanDto> Get(CancellationToken ct) => catalog.ReadAsync(false, ct);
    [HttpPost("quote")] public Task<PurchaseQuote> Quote(PurchaseSelection request, CancellationToken ct) => catalog.QuoteAsync(request, ct);
}
