using HireYar.Api.Billing;
using HireYar.Application.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HireYar.Api.Controllers;

[ApiController, Route("api/admin/pricing"), Authorize(Policy = "SystemAdmin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminInterviewTypesController(InterviewTypeService service, RialBillingResponses output) : ControllerBase
{
    [HttpGet("interview-types")]
    public Task<IReadOnlyList<InterviewTypeDto>> Types(CancellationToken ct) => service.ListAsync(false, ct);
    [HttpPost("interview-types")]
    public Task<InterviewTypeDto> Create(InterviewTypeRequest r, CancellationToken ct) => service.SaveAsync(null, r, null, ct);
    [HttpPut("interview-types/{code}")]
    public Task<InterviewTypeDto> Update(string code, InterviewTypeUpdateRequest r, CancellationToken ct)
        => service.SaveAsync(code, r.Type, r.RowVersion, ct);
    // Upsert, activation and price edit share the package concurrency token.
    [HttpPut("packages/{id:guid}/interview-types/{code}")]
    public Task<RialPackageDto> SetPackage(Guid id, string code, PackageInterviewTypeRequest r, CancellationToken ct)
        => output.ConvertAsync(() => service.SetPackageAsync(id, code, r, ct), ct);
}
