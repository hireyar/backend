namespace HireYar.Domain.Subscriptions;

public sealed class InterviewUsageEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid InterviewSessionId { get; set; }
    public string EventType { get; set; } = "";
    public DateTimeOffset OccurredAtUtc { get; set; }
    public bool IsHistorical { get; set; }
}
