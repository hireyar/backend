using HireYar.Application.Common;
using HireYar.Application.Hiring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/interview-sessions"), Authorize(Roles = "Owner,Recruiter")]
public sealed class InterviewSessionsController(InterviewManagementService service) : ControllerBase
{
    [HttpGet] public Task<Page<SessionListDto>> List([FromQuery] PageQuery page, [FromQuery] Guid? jobApplicationId, CancellationToken ct) => service.ListAsync(page, jobApplicationId, ct);
    [HttpGet("{id:guid}")] public Task<HrSessionDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost, EnableRateLimiting("auth")] public Task<SessionListDto> Create(CreateSessionRequest r, CancellationToken ct) => service.CreateAsync(r, ct);
    [HttpPost("{id:guid}/resend"), EnableRateLimiting("auth")] public Task<SessionListDto> Resend(Guid id, CancellationToken ct) => service.ResendAsync(id, ct);
    [HttpPost("{id:guid}/cancel")] public Task<SessionListDto> Cancel(Guid id, VersionRequest r, CancellationToken ct) => service.CancelAsync(id, r, ct);
}
