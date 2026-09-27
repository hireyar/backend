using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Domain.Organizations;
namespace HireYar.Application.Members;

public sealed record InviteRequest([Required, EmailAddress, StringLength(256)] string Email);
public sealed record AcceptInviteRequest([Required, StringLength(64, MinimumLength = 64)] string Token,
    [Required, StringLength(200)] string FullName, [Required, StringLength(128, MinimumLength = 12)] string Password);
public sealed record ActiveRequest(bool IsActive);
public sealed record MemberDto(Guid Id, string? Email, string? Mobile, string FullName, OrganizationRole Role, bool IsActive);
public sealed record InvitationDto(Guid Id, string Email, DateTimeOffset ExpiresAtUtc, DateTimeOffset? AcceptedAtUtc, DateTimeOffset? CancelledAtUtc);
public sealed record InvitationDelivery(Guid Id, bool EmailSent);
