namespace HireYar.Domain.Interviews;

public sealed class SessionReportPolicy
{
    public Guid InterviewSessionId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid Revision { get; set; }
    public decimal PassingScore { get; set; }
    public decimal StrongScore { get; set; }
    public decimal MinimumCoverage { get; set; }
    public string WeightsJson { get; set; } = "[]";
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid UpdatedByUserId { get; set; }
}
