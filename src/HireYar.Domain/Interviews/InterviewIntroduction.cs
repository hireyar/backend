namespace HireYar.Domain.Interviews;

public enum IntroductionStatus { Uploaded, Transcribing, Processed, Failed }

public sealed class InterviewIntroduction
{
    private InterviewIntroduction() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; private set; }
    public Guid InterviewSessionId { get; private set; }
    public string ClientTurnId { get; private set; } = "";
    public string AudioStorageKey { get; private set; } = "";
    public string AudioContentType { get; private set; } = "";
    public long AudioSizeBytes { get; private set; }
    public string? VideoStorageKey { get; private set; }
    public string? VideoContentType { get; private set; }
    public long? VideoSizeBytes { get; private set; }
    public int? DurationSeconds { get; private set; }
    public DateTimeOffset? LastSpeechEndUtc { get; private set; }
    public DateTimeOffset SubmittedAtUtc { get; private set; }
    public IntroductionStatus Status { get; private set; } = IntroductionStatus.Uploaded;
    public string? Transcript { get; private set; }
    public string? TranscriptProvider { get; private set; }
    public string? TranscriptModel { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public string? FailureCode { get; private set; }
    public Guid? ProcessingLeaseId { get; private set; }
    public DateTimeOffset? ProcessingLeaseExpiresAtUtc { get; private set; }
    public DateTimeOffset? NextRecoveryAtUtc { get; private set; }
    public int RecoveryAttempts { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public InterviewIntroduction(Guid organizationId, Guid sessionId, string turnId, string audioKey, string audioMime,
        long audioSize, DateTimeOffset submittedAt, int? duration, DateTimeOffset? speechEnd,
        string? videoKey, string? videoMime, long? videoSize)
    {
        InterviewRules.Utc(submittedAt); if (speechEnd is { } ended) InterviewRules.Utc(ended);
        InterviewRules.Require(audioSize > 0 && duration is null or > 0, "audio_invalid");
        InterviewRules.Require(videoKey is null ? videoMime is null && videoSize is null : videoMime is not null && videoSize > 0, "video_invalid");
        OrganizationId = organizationId; InterviewSessionId = sessionId;
        ClientTurnId = InterviewRules.Text(turnId, 128); AudioStorageKey = InterviewRules.Text(audioKey, 512);
        AudioContentType = InterviewRules.Text(audioMime, 100); AudioSizeBytes = audioSize;
        SubmittedAtUtc = submittedAt; DurationSeconds = duration; LastSpeechEndUtc = speechEnd;
        VideoStorageKey = videoKey; VideoContentType = videoMime; VideoSizeBytes = videoSize;
    }
    public Guid Claim(DateTimeOffset now, int leaseSeconds, bool recovery)
    {
        InterviewRules.Utc(now);
        InterviewRules.Require(Status != IntroductionStatus.Processed && (ProcessingLeaseExpiresAtUtc is null || ProcessingLeaseExpiresAtUtc <= now), "processing_in_progress");
        ProcessingLeaseId = Guid.NewGuid(); ProcessingLeaseExpiresAtUtc = now.AddSeconds(leaseSeconds);
        Status = IntroductionStatus.Transcribing; FailureCode = null; NextRecoveryAtUtc = null;
        if (recovery) RecoveryAttempts++;
        return ProcessingLeaseId.Value;
    }
    public void CheckLease(Guid lease, DateTimeOffset now) => InterviewRules.Require(
        ProcessingLeaseId == lease && ProcessingLeaseExpiresAtUtc > now && Status == IntroductionStatus.Transcribing, "processing_lease_expired");
    public void Complete(Guid lease, string transcript, string provider, string model, DateTimeOffset now)
    {
        CheckLease(lease, now);
        Transcript = InterviewRules.Text(transcript, 32000); TranscriptProvider = InterviewRules.Text(provider, 100);
        TranscriptModel = InterviewRules.Text(model, 100); ProcessedAtUtc = now; Status = IntroductionStatus.Processed;
        ProcessingLeaseId = null; ProcessingLeaseExpiresAtUtc = null; NextRecoveryAtUtc = null; FailureCode = null;
    }
    public void Fail(Guid lease, string code, DateTimeOffset now)
    {
        CheckLease(lease, now); FailureCode = InterviewRules.Text(code, 100); Status = IntroductionStatus.Failed;
        NextRecoveryAtUtc = now.AddSeconds(30 * (1 << Math.Min(RecoveryAttempts, 5)));
        ProcessingLeaseId = null; ProcessingLeaseExpiresAtUtc = null;
    }
}
