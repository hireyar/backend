namespace HireYar.Domain.Interviews;

// Independent post-interview assessment; never modifies the live answer or its billing.
public sealed class AnswerReportAssessment
{
    public Guid AnswerId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid InterviewSessionId { get; set; }
    public string State { get; set; } = "Queued";
    public int Attempts { get; set; }
    public Guid? AttemptId { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public string? FailureCode { get; set; }
    public string? InputHash { get; set; }
    public string EvaluatorVersion { get; set; } = "answer-quality/2";
    public decimal? Score { get; set; }
    public decimal? Confidence { get; set; }
    public string? Explanation { get; set; }
    public string DimensionsJson { get; set; } = "[]";
    public string EvidenceJson { get; set; } = "[]";
    public string? Provider { get; set; }
    public string? Model { get; set; }
}
