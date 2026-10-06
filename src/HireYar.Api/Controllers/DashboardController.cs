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

[ApiController, Route("api/dashboard")]
public sealed class DashboardController(DashboardService service) : ControllerBase
{
    [HttpGet] public Task<DashboardDto> Get(CancellationToken ct) => service.GetAsync(ct);
}

