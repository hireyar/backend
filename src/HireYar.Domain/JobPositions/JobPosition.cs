namespace HireYar.Domain.JobPositions;

public enum JobStatus { Draft, Active, Archived }
public sealed class JobPosition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Seniority { get; set; } = "";
    public JobStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
