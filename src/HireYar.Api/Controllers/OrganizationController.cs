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

[ApiController, Route("api/organization")]
public sealed class OrganizationController(OrganizationService service) : ControllerBase
{
    [HttpGet] public Task<OrganizationDto> Get(CancellationToken ct) => service.GetAsync(ct);
    [Authorize(Roles = "Owner"), HttpPut] public Task<OrganizationDto> Update(OrganizationRequest r, CancellationToken ct) => service.UpdateAsync(r, ct);
}

