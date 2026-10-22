using System.ComponentModel.DataAnnotations;
using HireYar.Domain.Organizations;
namespace HireYar.Application.Organizations;

public sealed record InviteTeamMemberRequest(
    [Required, StringLength(11, MinimumLength = 11), RegularExpression("^09[0-9]{9}$")] string Mobile,
    [Required, StringLength(100)] string FirstName,
    [Required, StringLength(100)] string LastName,
    [Required, StringLength(64)] string AccessRoleCode = "interview_manager",
    [Required, EmailAddress, StringLength(256)]
    [property: System.Text.Json.Serialization.JsonRequired] string? Email = null,
    [RegularExpression("^fa$")] string Language = "fa",
    [StringLength(2000)] string? Message = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(LastName) &&
            (FirstName.Trim().Length + 1 + LastName.Trim().Length) > 200)
            yield return new("نام و نام خانوادگی در مجموع حداکثر ۲۰۰ کاراکتر هستند.", [nameof(FirstName), nameof(LastName)]);
    }
}
public sealed record TeamInvitationDto(Guid Id, string Mobile, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc,
    string Status, string? FirstName = null, string? LastName = null,
    string AccessRoleCode = "interview_manager", string? Email = null, string Language = "fa", string EmailDelivery = "NotRequested");
public sealed record TeamMemberDto(Guid Id, string FullName, string? Mobile, OrganizationRole Role, bool IsActive, string? AccessRoleCode = null);

public sealed record BatchTeamInvitationsRequest([Required, MinLength(1), MaxLength(50)] IReadOnlyList<InviteTeamMemberRequest> Invitations);
public sealed record BatchTeamInvitationResult(string Mobile, TeamInvitationDto? Invitation, int Status, string? ErrorCode);

public sealed record TeamInvitationCapacityDto(bool HasActiveSubscription, int? TeamLimit, int ActiveMembers,
    int PendingInvitations, int OccupiedSlots, int? AvailableSlots, bool UnlimitedTeam, int MaxBatchSize);
