using HireYar.Application.Common;
using HireYar.Application.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ApiController, Authorize(Roles = "Owner"), Route("api/team")]
public sealed class TeamMembersController(ITeamMembers team) : ControllerBase
{
    [HttpGet("members")]
    public Task<Page<TeamMemberDto>> Members([FromQuery] PageQuery pagination, CancellationToken ct) => team.ListAsync(pagination, ct);
    [HttpGet("invitations")]
    public Task<Page<TeamInvitationDto>> Invitations([FromQuery] PageQuery pagination, CancellationToken ct) => team.InvitationsAsync(pagination, ct);
    [HttpPost("invitations")]
    public async Task<IActionResult> Invite(InviteTeamMemberRequest request, CancellationToken ct)
    {
        var result = await team.InviteAsync(request, ct);
        return Created($"/api/team/invitations", result);
    }
    [HttpDelete("invitations/{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) { await team.CancelAsync(id, ct); return NoContent(); }
    [HttpPost("members/{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) { await team.DeactivateAsync(id, ct); return NoContent(); }
}
