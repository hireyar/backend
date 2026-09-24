using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Domain.Organizations;
namespace HireYar.Application.Members;

public sealed class MemberService(ITeamMembers members, ICurrentUser user)
{
    private Guid OwnerOrganization { get { if (user.Role != OrganizationRole.Owner) throw new AppException(403, "forbidden", "Owner access required."); return user.OrganizationId; } }
    public Task<Page<MemberDto>> ListAsync(PageQuery p, CancellationToken ct) => members.ListAsync(OwnerOrganization, p, ct);
    public Task<Page<InvitationDto>> InvitationsAsync(PageQuery p, CancellationToken ct) => members.InvitationsAsync(OwnerOrganization, p, ct);
    public Task<InvitationDelivery> InviteAsync(InviteRequest r, CancellationToken ct) => members.InviteAsync(OwnerOrganization, r, ct);
    public Task<InvitationDelivery> ResendAsync(Guid id, CancellationToken ct) => members.ResendAsync(OwnerOrganization, id, ct);
    public Task CancelAsync(Guid id, CancellationToken ct) => members.CancelAsync(OwnerOrganization, id, ct);
    public Task AcceptAsync(AcceptInviteRequest r, CancellationToken ct) => members.AcceptAsync(r, ct);
    public Task SetActiveAsync(Guid id, ActiveRequest r, CancellationToken ct) => members.SetActiveAsync(OwnerOrganization, id, r.IsActive, ct);
}
