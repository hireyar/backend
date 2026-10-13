using HireYar.Application.Common;
namespace HireYar.Application.Organizations;

public interface ITeamMembers
{
    Task<Page<TeamMemberDto>> ListAsync(PageQuery page, CancellationToken ct);
    Task<Page<TeamInvitationDto>> InvitationsAsync(PageQuery page, CancellationToken ct);
    Task<TeamInvitationDto> InviteAsync(InviteTeamMemberRequest request, CancellationToken ct);
    Task CancelAsync(Guid id, CancellationToken ct);
    Task DeactivateAsync(Guid id, CancellationToken ct);
}
