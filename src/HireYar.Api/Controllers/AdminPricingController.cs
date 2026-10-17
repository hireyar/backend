using HireYar.Api.Billing;
using HireYar.Application.Billing;
using HireYar.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ApiController, Route("api/admin/pricing"), Authorize(Policy = "SystemAdmin")]
public sealed class AdminPricingController(PricingService service, BillingQueryService query, WalletService wallet, RialBillingResponses output) : ControllerBase
{
    [HttpGet("packages")] public Task<Page<RialPackageDto>> Packages([FromQuery] PageQuery pagination, [FromQuery] SearchQuery filter, CancellationToken ct) => output.ConvertAsync(() => service.AdminPackagesAsync(pagination, ct, filter.Search), ct);
    [HttpGet("packages/{id:guid}")] public Task<RialPackageDto> Package(Guid id, CancellationToken ct) => output.ConvertAsync(() => service.GetAsync(id, ct), ct);
    [HttpPost("packages")] public Task<RialPackageDto> CreatePackage(PackageRequest r, CancellationToken ct) => output.ConvertAsync(() => service.SaveAsync(null, r, null, ct), ct);
    [HttpPut("packages/{id:guid}")] public Task<RialPackageDto> UpdatePackage(Guid id, PackageUpdateRequest r, CancellationToken ct) => output.ConvertAsync(() => service.SaveAsync(id, r.Package, r.RowVersion, ct), ct);
    [HttpPost("packages/{id:guid}/periods")] public Task<RialPackageDto> CreatePeriod(Guid id, PeriodWriteRequest r, CancellationToken ct) => output.ConvertAsync(() => service.SavePeriodAsync(id, null, r, ct), ct);
    [HttpPut("packages/{id:guid}/periods/{periodId:guid}")] public Task<RialPackageDto> UpdatePeriod(Guid id, Guid periodId, PeriodWriteRequest r, CancellationToken ct) => output.ConvertAsync(() => service.SavePeriodAsync(id, periodId, r, ct), ct);
    [HttpGet("features")] public Task<IReadOnlyList<FeatureDto>> Features(CancellationToken ct) => service.FeaturesAsync(ct);
    [HttpPost("features")] public Task<FeatureDto> CreateFeature(FeatureRequest r, CancellationToken ct) => service.SaveFeatureDefinitionAsync(null, r, ct);
    [HttpPut("features/{id:guid}")] public Task<FeatureDto> UpdateFeature(Guid id, FeatureRequest r, CancellationToken ct) => service.SaveFeatureDefinitionAsync(id, r, ct);
    [HttpPut("packages/{id:guid}/features")] public Task<RialPackageDto> SetFeature(Guid id, FeatureWriteRequest r, CancellationToken ct) => output.ConvertAsync(() => service.SetFeatureAsync(id, r, ct), ct);
    [HttpGet("ai-credit-packages")] public Task<Page<RialCreditPackageDto>> Credits([FromQuery] PageQuery pagination, [FromQuery] SearchQuery filter, CancellationToken ct) => output.ConvertAsync(() => service.AdminCreditsAsync(pagination, ct, filter.Search), ct);
    [HttpPost("ai-credit-packages")] public Task<RialCreditPackageDto> CreateCredit(CreditPackageRequest r, CancellationToken ct) => output.ConvertAsync(() => service.SaveCreditAsync(null, r, null, ct), ct);
    [HttpPut("ai-credit-packages/{id:guid}")] public Task<RialCreditPackageDto> UpdateCredit(Guid id, CreditPackageUpdateRequest r, CancellationToken ct) => output.ConvertAsync(() => service.SaveCreditAsync(id, r.Package, r.RowVersion, ct), ct);
    [HttpGet("organizations/{org:guid}/subscription")] public Task<RialSubscriptionOverviewDto> Subscriptions(Guid org, CancellationToken ct) => output.ConvertAsync(() => query.SubscriptionsAsync(org, ct), ct);
    [HttpGet("organizations/{org:guid}/ai-credit")] public Task<RialWalletDto> Wallet(Guid org, CancellationToken ct) => output.ConvertAsync(() => wallet.GetAsync(org, ct), ct);
    [HttpPost("organizations/{org:guid}/ai-credit/adjustments")] public Task<RialWalletDto> Adjust(Guid org, AdjustmentRequest r, CancellationToken ct) => output.ConvertAsync(() => wallet.AdjustAsync(org, r, ct), ct);
}
