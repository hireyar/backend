namespace HireYar.Domain.Hiring;

public enum SessionStatus { NotStarted, InProgress, Completed, Expired, Cancelled }
public enum DeliveryStatus { Pending, Sent, Failed }
public enum AnswerStatus { Draft, Submitted, Skipped }
public sealed class InterviewSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid JobApplicationId { get; set; }
    public Guid SourceInterviewTemplateId { get; set; }
    public SessionStatus Status { get; set; }
    public DateTimeOffset AvailableFromUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? DeadlineAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public DateTimeOffset? LastActivityAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public string OrganizationName { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string InterviewTitle { get; set; } = "";
    public string CandidateName { get; set; } = "";
    public string CandidateEmail { get; set; } = "";
    public string Language { get; set; } = "";
    public int DurationMinutes { get; set; }
    public int MaxFollowUpQuestionsPerQuestion { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string? ConsentVersion { get; set; }
    public DateTimeOffset? ConsentAcceptedAtUtc { get; set; }
    public InterviewInvitation Invitation { get; set; } = null!;
    public List<SnapshotQuestion> Questions { get; set; } = [];
    public List<InterviewAnswer> Answers { get; set; } = [];
}
public sealed class SnapshotQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InterviewSessionId { get; set; }
    public int Order { get; set; }
    public string Text { get; set; } = "";
    public string Competency { get; set; } = "";
    public string EvaluationGuidance { get; set; } = "";
}
public sealed class InterviewInvitation
{
    public Guid InterviewSessionId { get; set; }
    public Guid OrganizationId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? UsedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; }
    public DateTimeOffset? LastSentAtUtc { get; set; }
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public int SendAttempts { get; set; }
    public string? CredentialHash { get; set; }
    public DateTimeOffset? CredentialExpiresAtUtc { get; set; }
}
public sealed class InterviewAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InterviewSessionId { get; set; }
    public Guid SnapshotQuestionId { get; set; }
    public string? Text { get; set; }
    public AnswerStatus Status { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
