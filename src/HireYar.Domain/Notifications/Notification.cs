namespace HireYar.Domain.Notifications;

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid RecipientUserId { get; set; }
    public string Type { get; set; } = "";
    public Guid? InterviewSessionId { get; set; }
    public string SourceKey { get; set; } = "";
    public string Target { get; set; } = "";
    public DateTimeOffset? ResolvedAtUtc { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }
}
