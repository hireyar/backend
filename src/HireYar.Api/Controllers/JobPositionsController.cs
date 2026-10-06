using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Application.JobPositions;
using HireYar.Application.InterviewTemplates;
using HireYar.Application.Organizations;
using HireYar.Application.Dashboard;
using HireYar.Domain.JobPositions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/job-positions")]
public sealed class JobPositionsController(JobService service) : ControllerBase
{
    [HttpGet] public Task<Page<JobDto>> List([FromQuery] PageQuery page, [FromQuery, StringLength(200)] string? search, [FromQuery, EnumDataType(typeof(JobStatus))] JobStatus? status, CancellationToken ct) => service.ListAsync(page, search, status, ct);
    [HttpGet("{id:guid}")] public Task<JobDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost] public async Task<ActionResult<JobDto>> Create(JobRequest r, CancellationToken ct) { var result = await service.CreateAsync(r, ct); return CreatedAtAction(nameof(Get), new { id = result.Id }, result); }
    [HttpPut("{id:guid}")] public Task<JobDto> Update(Guid id, UpdateJobRequest r, CancellationToken ct) => service.UpdateAsync(id, r, ct);
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}

