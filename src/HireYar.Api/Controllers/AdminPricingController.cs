using HireYar.Application.Billing;
using HireYar.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/admin/pricing"), Authorize(Policy = "SystemAdmin")]
public sealed class AdminPricingController(PricingService service, BillingQueryService query, WalletService wallet) : ControllerBase
{
    [HttpGet("packages")] public Task<Page<PackageDto>> Packages([FromQuery] PageQuery pagination, CancellationToken ct) => service.AdminPackagesAsync(pagination, ct);
    [HttpGet("packages/{id:guid}")] public Task<PackageDto> Package(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost("packages")] public Task<PackageDto> CreatePackage(PackageRequest r, CancellationToken ct) => service.SaveAsync(null, r, null, ct);
    [HttpPut("packages/{id:guid}")] public Task<PackageDto> UpdatePackage(Guid id, PackageUpdateRequest r, CancellationToken ct) => service.SaveAsync(id, r.Package, r.RowVersion, ct);
    [HttpPost("packages/{id:guid}/periods")] public Task<PackageDto> CreatePeriod(Guid id, PeriodWriteRequest r, CancellationToken ct) => service.SavePeriodAsync(id, null, r, ct);
    [HttpPut("packages/{id:guid}/periods/{periodId:guid}")] public Task<PackageDto> UpdatePeriod(Guid id, Guid periodId, PeriodWriteRequest r, CancellationToken ct) => service.SavePeriodAsync(id, periodId, r, ct);
    [HttpGet("features")] public Task<IReadOnlyList<FeatureDto>> Features(CancellationToken ct) => service.FeaturesAsync(ct);
    [HttpPost("features")] public Task<FeatureDto> CreateFeature(FeatureRequest r, CancellationToken ct) => service.SaveFeatureDefinitionAsync(null, r, ct);
    [HttpPut("features/{id:guid}")] public Task<FeatureDto> UpdateFeature(Guid id, FeatureRequest r, CancellationToken ct) => service.SaveFeatureDefinitionAsync(id, r, ct);
    [HttpPut("packages/{id:guid}/features")] public Task<PackageDto> SetFeature(Guid id, FeatureWriteRequest r, CancellationToken ct) => service.SetFeatureAsync(id, r, ct);
    [HttpGet("ai-credit-packages")] public Task<Page<CreditPackageDto>> Credits([FromQuery] PageQuery pagination, CancellationToken ct) => service.AdminCreditsAsync(pagination, ct);
    [HttpPost("ai-credit-packages")] public Task<CreditPackageDto> CreateCredit(CreditPackageRequest r, CancellationToken ct) => service.SaveCreditAsync(null, r, null, ct);
    [HttpPut("ai-credit-packages/{id:guid}")] public Task<CreditPackageDto> UpdateCredit(Guid id, CreditPackageUpdateRequest r, CancellationToken ct) => service.SaveCreditAsync(id, r.Package, r.RowVersion, ct);
    [HttpGet("organizations/{org:guid}/subscription")] public Task<SubscriptionOverviewDto> Subscriptions(Guid org, CancellationToken ct) => query.SubscriptionsAsync(org, ct);
    [HttpGet("organizations/{org:guid}/ai-credit")] public Task<WalletDto> Wallet(Guid org, CancellationToken ct) => wallet.GetAsync(org, ct);
    [HttpPost("organizations/{org:guid}/ai-credit/adjustments")] public Task<WalletDto> Adjust(Guid org, AdjustmentRequest r, CancellationToken ct) => wallet.AdjustAsync(org, r, ct);
}
