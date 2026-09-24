using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Application.JobPositions;
using HireYar.Application.InterviewTemplates;
using HireYar.Application.Organizations;
using HireYar.Application.Members;
using HireYar.Application.Dashboard;
using HireYar.Domain.JobPositions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/interview-templates")]
public sealed class InterviewTemplatesController(TemplateService service) : ControllerBase
{
    [HttpGet] public Task<Page<TemplateListDto>> List([FromQuery] PageQuery page, [FromQuery] Guid? jobPositionId, CancellationToken ct) => service.ListAsync(page, jobPositionId, ct);
    [HttpGet("{id:guid}")] public Task<TemplateDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost] public async Task<ActionResult<TemplateDto>> Create(TemplateRequest r, CancellationToken ct) { var result = await service.CreateAsync(r, ct); return CreatedAtAction(nameof(Get), new { id = result.Id }, result); }
    [HttpPut("{id:guid}")] public Task<TemplateDto> Update(Guid id, UpdateTemplateRequest r, CancellationToken ct) => service.UpdateAsync(id, r, ct);
}

