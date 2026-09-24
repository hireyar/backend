using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Domain.Organizations;
namespace HireYar.Application.Members;

public sealed record InviteRequest([Required, EmailAddress, StringLength(256)] string Email);
public sealed record AcceptInviteRequest([Required, StringLength(64, MinimumLength = 64)] string Token,
    [Required, StringLength(200)] string FullName, [Required, StringLength(128, MinimumLength = 12)] string Password);
public sealed record ActiveRequest(bool IsActive);
public sealed record MemberDto(Guid Id, string Email, string FullName, OrganizationRole Role, bool IsActive);
public sealed record InvitationDto(Guid Id, string Email, DateTimeOffset ExpiresAtUtc, DateTimeOffset? AcceptedAtUtc, DateTimeOffset? CancelledAtUtc);
public sealed record InvitationDelivery(Guid Id, bool EmailSent);
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

