using HireYar.Application.Common;
using HireYar.Application.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ApiController, Authorize, Route("api/team")]
public sealed class TeamMembersController(ITeamMembers team, IAccessControl access, IServiceScopeFactory scopes) : ControllerBase
{
    [HttpGet("access-sections")]
    public Task<IReadOnlyList<AccessSectionDto>> Sections(CancellationToken ct) => access.SectionsAsync(ct);
    [HttpPost("invitations/batch"), Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
    public async Task<IReadOnlyList<BatchTeamInvitationResult>> Batch(BatchTeamInvitationsRequest request, CancellationToken ct)
    {
        if (request.Invitations.Any(x => x is null)) throw AppException.Invalid("invalid_invitation_batch");
        var results = new List<BatchTeamInvitationResult>();
        foreach (var item in request.Invitations) {
            try { await using var scope = scopes.CreateAsyncScope(); var scopedTeam = scope.ServiceProvider.GetRequiredService<ITeamMembers>(); results.Add(new(item.Mobile, await scopedTeam.InviteAsync(item, ct), 201, null)); }
            catch (AppException e) when (e.Status is 400 or 403 or 404 or 409) { results.Add(new(item.Mobile, null, e.Status, e.Code)); }
        }
        return results;
    }
    [HttpGet("access-roles")]
    public Task<IReadOnlyList<AccessRoleDto>> Roles(CancellationToken ct) => access.RolesAsync(false, ct);
    [HttpPut("members/{id:guid}/access")]
    public async Task<IActionResult> ChangeAccess(Guid id, ChangeMemberAccessRequest request, CancellationToken ct) { await team.ChangeAccessAsync(id, request, ct); return NoContent(); }
    [HttpPost("invitations/{id:guid}/resend"), Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
    public Task<TeamInvitationDto> Resend(Guid id, CancellationToken ct) => team.ResendAsync(id, ct);
    [HttpGet("members")]
    public Task<Page<TeamMemberDto>> Members([FromQuery] PageQuery pagination, CancellationToken ct) => team.ListAsync(pagination, ct);
    [HttpGet("invitations")]
    public Task<Page<TeamInvitationDto>> Invitations([FromQuery] PageQuery pagination, CancellationToken ct) => team.InvitationsAsync(pagination, ct);
    [HttpPost("invitations"), Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
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
