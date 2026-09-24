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

[ApiController, Route("api/members"), Authorize(Roles = "Owner")]
public sealed class MembersController(MemberService service) : ControllerBase
{
    [HttpGet] public Task<Page<MemberDto>> List([FromQuery] PageQuery page, CancellationToken ct) => service.ListAsync(page, ct);
    [HttpGet("invitations")] public Task<Page<InvitationDto>> Invitations([FromQuery] PageQuery page, CancellationToken ct) => service.InvitationsAsync(page, ct);
    [HttpPost("invitations"), EnableRateLimiting("auth")] public Task<InvitationDelivery> Invite(InviteRequest r, CancellationToken ct) => service.InviteAsync(r, ct);
    [HttpPost("invitations/{id:guid}/resend"), EnableRateLimiting("auth")] public Task<InvitationDelivery> Resend(Guid id, CancellationToken ct) => service.ResendAsync(id, ct);
    [HttpDelete("invitations/{id:guid}")] public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) { await service.CancelAsync(id, ct); return NoContent(); }
    [HttpPut("{id:guid}/active")] public async Task<IActionResult> Active(Guid id, ActiveRequest r, CancellationToken ct) { await service.SetActiveAsync(id, r, ct); return NoContent(); }
}

