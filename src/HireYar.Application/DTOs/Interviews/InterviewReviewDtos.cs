namespace HireYar.Application.Interviews;

public sealed record InterviewEventRequest(Guid RequestId, string Kind, DateTimeOffset OccurredAtUtc, string? ConsentVersion = null);
public sealed record InterviewHumanDecisionRequest(Guid RequestId, string Decision, string? Note = null);
public sealed record InterviewReviewEntryDto(Guid Id, string Kind, Guid? ActorUserId, string? Note,
    DateTimeOffset? ClientOccurredAtUtc, DateTimeOffset RecordedAtUtc);
public sealed record InterviewRecordingOffset(Guid AnswerId, decimal StartSeconds, decimal EndSeconds);
public sealed record InterviewRecordingDto(string VideoUrl, int DurationSeconds, IReadOnlyList<InterviewRecordingOffset> Timeline,
    string Source = "candidate_reported");
public sealed record InterviewReviewDto(IReadOnlyList<InterviewReviewEntryDto> Entries,
    string ConsentStatus, string RecordingStatus, InterviewRecordingDto? Recording);
