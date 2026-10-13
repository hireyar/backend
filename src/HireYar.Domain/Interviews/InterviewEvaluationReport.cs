namespace HireYar.Domain.Interviews;

public sealed class InterviewEvaluationReport
{
    public Guid InterviewSessionId { get; set; }
    public Guid OrganizationId { get; set; }
    public string GenerationStage { get; set; } = "Failed";
    public int GenerationAttempts { get; set; }
    public DateTimeOffset? RequestedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureStage { get; set; }
    public string? GenerationSettingsJson { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public string? ResultJson { get; set; }
    public string? Model { get; set; }
    public string? Provider { get; set; }
    public string? PromptVersion { get; set; }
    public DateTimeOffset? CreatedAtUtc { get; set; }
}
