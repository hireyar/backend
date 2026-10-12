using HireYar.Domain.Interviews;
namespace HireYar.Application.Interviews;

public sealed record SpeechTranscriptionResult(string Text, string Provider, string Model);
public sealed record SpeechProfile(string Model, string Voice, decimal Speed);
public sealed record TextToSpeechResult(byte[] Audio, string ContentType);
public sealed record InterviewDecisionResult(string Json, string Provider, string Model);
public sealed record InterviewDecisionContext(string PositionTitle, Guid CurrentQuestionId, string Status,
    int AnsweredQuestionCount, int MaxQuestionCount, int RemainingSeconds,
    InterviewQuestionSnapshot Question, string CandidateTranscript,
    IReadOnlyList<InterviewSkill> Skills, IReadOnlyList<CandidateSkillState> SkillStates,
    IReadOnlyList<InterviewCoveredTopic> CoveredTopics, IReadOnlyList<InterviewQuestionSnapshot> AvailableQuestions,
    string? PositionDescription = null, string? ResumeText = null, string? CandidateIntroduction = null,
    string InterviewType = "specialized", string Language = "fa-IR",
    IReadOnlyList<InterviewQuestionSnapshot>? QuestionBank = null,
    IReadOnlyList<InterviewDialogueTurn>? PreviousAnswers = null,
    string? MasterPrompt = null, int RemainingInteractions = 0,
    int TotalPreviousAnswers = 0, bool HistoryIsPartial = false,
    IReadOnlyList<HrEvidenceDto>? PreviousEvidence = null, bool ProgressiveTopicQuestions = false, bool MediumFirstTopicQuestions = false);
public sealed record InterviewDialogueTurn(Guid QuestionId, string Question, string? Answer, string? AssistantReply = null);
public interface IResumeTextExtractor
{
    Task<string?> ExtractAsync(byte[] file, string extension, CancellationToken ct);
}
public interface ISpeechToTextService
{
    Task<SpeechTranscriptionResult> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken ct);
}
public interface IInterviewDecisionService
{
    Task<InterviewDecisionResult> EvaluateAsync(InterviewDecisionContext context, CancellationToken ct);
}
public interface ITextToSpeechService
{
    SpeechProfile Profile { get; }
    Task<TextToSpeechResult> GenerateAsync(string text, CancellationToken ct);
}
public sealed class InterviewProviderException(string code) : Exception("Interview provider unavailable.")
{
    public string Code { get; } = code;
}
public sealed record StoredAudio(string Key, long SizeBytes);
public interface IInterviewMediaMetadata
{
    string EntityTag { get; }
}
public interface IInterviewAudioStorage
{
    Task<StoredAudio> SaveAsync(Stream audio, string category, Guid ownerId, long maxBytes, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
public sealed class InterviewOptions
{
    public int ShortlistLimit { get; set; } = 10;
    public int LeaseSeconds { get; set; } = 300;
    public int MaxAnswerSeconds { get; set; } = 600;
    public long MaxUploadBytes { get; set; } = 20 * 1024 * 1024;
    public long MaxVideoUploadBytes { get; set; } = 40 * 1024 * 1024;
    public bool BackgroundRecoveryEnabled { get; set; } = true;
    public int MaxRecoveryAttempts { get; set; } = 3;
    public int RecoveryPollSeconds { get; set; } = 15;
    public bool SelectiveHistoryEnabled { get; set; } = true;
    public int HistoryCharacterBudget { get; set; } = 12000;
}
