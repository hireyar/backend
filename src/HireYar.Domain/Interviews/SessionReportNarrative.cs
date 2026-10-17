namespace HireYar.Domain.Interviews;

// Independent evidence-based prose; never changes assessments, hiring decisions or billing.
public sealed class SessionReportNarrative
{
    public Guid InterviewSessionId { get; set; }
    public Guid OrganizationId { get; set; }
    public string InputHash { get; set; } = "";
    public string ReportFingerprint { get; set; } = "";
    public string State { get; set; } = "Processing";
    public int Attempts { get; set; }
    public Guid? AttemptId { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public string? FailureCode { get; set; }
    public string ResultJson { get; set; } = "{}";
    public string? Provider { get; set; }
    public string? Model { get; set; }
}