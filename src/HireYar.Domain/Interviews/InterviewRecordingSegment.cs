namespace HireYar.Domain.Interviews;

public sealed class InterviewRecordingSegment
{
    public Guid InterviewSessionId { get; init; }
    public Guid OrganizationId { get; init; }
    public int SegmentNumber { get; init; }
    public Guid UploadId { get; init; }
    public string StorageKey { get; init; } = "";
    public string ContentType { get; init; } = "";
    public string ContentHash { get; init; } = "";
    public long SizeBytes { get; init; }
    public int DurationSeconds { get; init; }
    public string TimelineJson { get; init; } = "[]";
    public DateTimeOffset UploadedAtUtc { get; init; }
}
