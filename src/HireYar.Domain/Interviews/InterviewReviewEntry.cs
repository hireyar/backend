namespace HireYar.Domain.Interviews;

public sealed class InterviewReviewEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public Guid InterviewSessionId { get; init; }
    public Guid RequestId { get; init; }
    public Guid? ActorUserId { get; init; }
    public string Kind { get; init; } = "";
    public string? Note { get; init; }
    public DateTimeOffset? ClientOccurredAtUtc { get; init; }
    public DateTimeOffset RecordedAtUtc { get; init; }
}
