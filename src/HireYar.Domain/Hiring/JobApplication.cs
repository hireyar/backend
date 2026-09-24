namespace HireYar.Domain.Hiring;

public enum ApplicationStatus { Active, Withdrawn, Closed }
public sealed class JobApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid CandidateId { get; set; }
    public Guid JobPositionId { get; set; }
    public ApplicationStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
