using HireYar.Domain.Organizations;
using Microsoft.AspNetCore.Identity;
namespace HireYar.Infrastructure.Auth;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = "";
    public Guid OrganizationId { get; set; }
    public OrganizationRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public string Hash { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public Guid? ReplacedById { get; set; }
}
