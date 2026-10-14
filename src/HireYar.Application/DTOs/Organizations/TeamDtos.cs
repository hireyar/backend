using System.ComponentModel.DataAnnotations;
using HireYar.Domain.Organizations;
namespace HireYar.Application.Organizations;

public sealed record InviteTeamMemberRequest(
    [Required, StringLength(11, MinimumLength = 11), RegularExpression("^09[0-9]{9}$")] string Mobile,
    [Required, StringLength(100)] string FirstName,
    [Required, StringLength(100)] string LastName) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(LastName) &&
            (FirstName.Trim().Length + 1 + LastName.Trim().Length) > 200)
            yield return new("نام و نام خانوادگی در مجموع حداکثر ۲۰۰ کاراکتر هستند.", [nameof(FirstName), nameof(LastName)]);
    }
}
public sealed record TeamInvitationDto(Guid Id, string Mobile, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc,
    string Status, string? FirstName = null, string? LastName = null);
public sealed record TeamMemberDto(Guid Id, string FullName, string? Mobile, OrganizationRole Role, bool IsActive);
