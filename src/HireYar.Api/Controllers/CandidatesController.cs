using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Application.Hiring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/candidates"), Authorize(Roles = "Owner,Recruiter")]
public sealed class CandidatesController(CandidateService service) : ControllerBase
{
    [HttpGet] public Task<Page<CandidateDto>> List([FromQuery] PageQuery page, [FromQuery, StringLength(200)] string? search, CancellationToken ct) => service.ListAsync(page, search, ct);
    [HttpGet("{id:guid}")] public Task<CandidateDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost] public async Task<ActionResult<CandidateDto>> Create(CandidateRequest r, CancellationToken ct) { var result = await service.CreateAsync(r, ct); return CreatedAtAction(nameof(Get), new { id = result.Id }, result); }
    [HttpPut("{id:guid}")] public Task<CandidateDto> Update(Guid id, UpdateCandidateRequest r, CancellationToken ct) => service.UpdateAsync(id, r, ct);
}

[ApiController, Route("api/job-applications"), Authorize(Roles = "Owner,Recruiter")]
public sealed class JobApplicationsController(CandidateService service) : ControllerBase
{
    [HttpGet] public Task<Page<ApplicationDto>> List([FromQuery] PageQuery page, [FromQuery] Guid? candidateId, [FromQuery] Guid? jobPositionId, CancellationToken ct) => service.ApplicationsAsync(page, candidateId, jobPositionId, ct);
    [HttpPost] public Task<ApplicationDto> Create(ApplicationRequest r, CancellationToken ct) => service.CreateApplicationAsync(r, ct);
    [HttpPut("{id:guid}/status")] public Task<ApplicationDto> Update(Guid id, ApplicationStatusRequest r, CancellationToken ct) => service.UpdateApplicationAsync(id, r, ct);
}
