using System.ComponentModel.DataAnnotations;
using HireYar.Domain.Organizations;
namespace HireYar.Application.Organizations;

public sealed record InviteTeamMemberRequest([Required, StringLength(11, MinimumLength = 11), RegularExpression("^09[0-9]{9}$")] string Mobile);
public sealed record TeamInvitationDto(Guid Id, string Mobile, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc, string Status);
public sealed record TeamMemberDto(Guid Id, string FullName, string? Mobile, OrganizationRole Role, bool IsActive);
