namespace HireYar.Domain.Support;

public enum TicketStatus { Open, Answered, Closed }
public enum TicketPriority { Low, Normal, High }

public sealed class SupportTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Subject { get; set; } = "";
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
public sealed class TicketMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid TicketId { get; set; }
    public Guid AuthorUserId { get; set; }
    public bool IsStaff { get; set; }
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
public sealed class TicketAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid TicketId { get; set; }
    public Guid MessageId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public int Length { get; set; }
    public string StorageKey { get; set; } = "";
}

