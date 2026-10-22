namespace HireYar.Infrastructure.Members;

public sealed class MobileTeamInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    // Historical invitations have no name; new API requests require both parts.
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string AccessRoleCode { get; set; } = "interview_manager";
    public string? Email { get; set; }
    public string Language { get; set; } = "fa";
    public string? Message { get; set; }
    public string Mobile { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
}
