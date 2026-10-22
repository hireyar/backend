namespace HireYar.Application.Interviews;

public sealed record SpeechTranscriptUpdate(string FinalText, string InterimText);
public interface IRealtimeSpeechToTextService
{
    TimeSpan FinalizationTimeout { get; }
    Task<SpeechTranscriptionResult> TranscribeRealtimeAsync(Stream audio,
        Func<SpeechTranscriptUpdate, CancellationToken, Task> update, CancellationToken ct);
}
public sealed record SpeechCapabilitiesDto(string Mode, string? StreamUrl, string Protocol = "hireyar-stt-v1");
public sealed record RealtimeSpeechContext(Guid OrganizationId, Guid InterviewId, string Purpose, string ClientTurnId, Guid? QuestionId, int TurnVersion);
public interface IRealtimeSpeechReceipt
{
    string Issue(RealtimeSpeechContext context, SpeechTranscriptionResult result, string audioSha256, long audioBytes);
    Task<SpeechTranscriptionResult> VerifyAsync(string receipt, RealtimeSpeechContext context, Stream audio, CancellationToken ct);
}
