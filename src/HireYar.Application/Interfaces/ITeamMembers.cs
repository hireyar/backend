using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Domain.Organizations;
namespace HireYar.Application.Members;

public interface ITeamMembers
{
    Task<Page<MemberDto>> ListAsync(Guid org, PageQuery page, CancellationToken ct);
    Task<Page<InvitationDto>> InvitationsAsync(Guid org, PageQuery page, CancellationToken ct);
    Task<InvitationDelivery> InviteAsync(Guid org, InviteRequest r, CancellationToken ct);
    Task<InvitationDelivery> ResendAsync(Guid org, Guid id, CancellationToken ct);
    Task CancelAsync(Guid org, Guid id, CancellationToken ct);
    Task AcceptAsync(AcceptInviteRequest r, CancellationToken ct);
    Task SetActiveAsync(Guid org, Guid id, bool active, CancellationToken ct);
    Task<int> ActiveCountAsync(Guid org, CancellationToken ct);
    Task<int> PendingCountAsync(Guid org, CancellationToken ct);
}
