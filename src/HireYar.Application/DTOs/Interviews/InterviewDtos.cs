using System.ComponentModel.DataAnnotations;
using HireYar.Domain.Interviews;
namespace HireYar.Application.Interviews;

public sealed record CandidateApplicationRequest(
    [Required, StringLength(200, MinimumLength = 1)] string FirstName,
    [Required, StringLength(200, MinimumLength = 1)] string LastName,
    [Required, EmailAddress, StringLength(256)] string Email, [StringLength(30)] string? Phone);
public sealed record SkillRequest([Required, StringLength(200, MinimumLength = 1)] string Name);
public sealed record PositionSkillRequest(bool IsRequired, [Range(0, 100)] int Priority, [Range(1, 5)] int TargetDifficulty);
public sealed record BankQuestionRequest(Guid SkillId, [Required, StringLength(2000, MinimumLength = 1)] string Text,
    [Required, StringLength(200, MinimumLength = 1)] string Topic, [Range(1, 5)] int Difficulty, bool IsActive);
public sealed record UpdateBankQuestionRequest(BankQuestionRequest Question, [Required, MinLength(16), MaxLength(16)] byte[] RowVersion);
public sealed record BankQuestionDto(Guid Id, Guid SkillId, string Text, string Topic, int Difficulty, bool IsActive, byte[] RowVersion);
public sealed record CreateInterviewRequest(Guid TemplateId, [Range(1, 30)] int MaxQuestionCount = 12, [Range(1, 168)] int ExpiresInHours = 72,
    [RegularExpression("^(hr_interview|technical_interview)$")] string RequiredCapabilityCode = "technical_interview", bool SendEmail = false, bool SendSms = false,
    [RegularExpression("^[a-z][a-z0-9_]{1,63}$")] string? InterviewType = null);
public sealed record InterviewInvitation(Guid InterviewId, string Token, DateTimeOffset ExpiresAtUtc, string CandidateRoute,
    string? AccessCode = null, string? InterviewUrl = null, string? EmailDelivery = null, string? SmsDelivery = null);
public sealed record InterviewAccessDetails(Guid InterviewId, string InterviewUrl, string? AccessCode,
    DateTimeOffset ExpiresAtUtc, bool CanResend, string AccessStatus);
public sealed record InterviewAccessStatus(string Status, string? Code = null, string? Message = null);
public sealed record InterviewProgress(int AnsweredQuestions, int MaxQuestions, int RemainingSeconds);
public sealed record CandidateQuestionDto(Guid Id, string Text, string? AudioUrl, bool AudioAvailable, string? InteractionMessage = null);
public sealed record CandidateAnswerDto(Guid Id, Guid QuestionId, string Status, bool CanRetry, string? ErrorCode,
    string? Transcript = null, string? ClientTurnId = null, DateTimeOffset? SubmittedAtUtc = null,
    DateTimeOffset? LastSpeechEndUtc = null, int? DurationSeconds = null, bool VideoAvailable = false,
    string? FailureStage = null, string? FailureCode = null);
public sealed record InterviewMediaCapabilities(bool VideoAnswers, long MaxAudioBytes, long MaxVideoBytes, int MaxAnswerSeconds);
public sealed record CandidateMessageDto(string Id, Guid? QuestionId, Guid? AnswerId, string Role, string? Text,
    DateTimeOffset CreatedAtUtc, string? Status = null, string? Stage = null);
public sealed record CandidateIntroductionDto(Guid Id, string ClientTurnId, string Status, string? Transcript,
    bool CanRetry, string? ErrorCode, int RecoveryAttempts, bool BackgroundRetryPending,
    DateTimeOffset SubmittedAtUtc, DateTimeOffset? LastSpeechEndUtc, int? DurationSeconds, bool VideoAvailable);
public sealed record CandidateInterviewDto(Guid InterviewId, string Status, bool CanRecord, bool IsProcessing,
    CandidateQuestionDto? Question, InterviewProgress Progress, CandidateAnswerDto? Answer, string? CompletionReason,
    string? WelcomeMessage = null, string? GoodbyeMessage = null, InterviewMediaCapabilities? MediaCapabilities = null,
    string Stage = "Questions", string? StageMessage = null, bool CandidateIntroductionEnabled = false,
    bool CompanyIntroductionEnabled = false, int RecoveryAttempts = 0, bool BackgroundRetryPending = false,
    string? StageAudioUrl = null, CandidateIntroductionDto? Introduction = null,
    string ProcessingState = "idle", string? ProcessingMessage = null, DateTimeOffset? NextRecoveryAtUtc = null,
    bool DiagnosticsEnabled = false, string? DiagnosticsUrl = null);
public sealed record AdvanceInterviewStageRequest([Required] string Stage, [StringLength(32000)] string? Introduction = null);
public sealed record HrAnswerDto(Guid Id, Guid QuestionId, string Status, string? Transcript, string? SelectionSource,
    string? FailureStage, string? FailureCode, string AudioUrl, string? VideoUrl = null,
    string? ClientTurnId = null, DateTimeOffset? LastSpeechEndUtc = null, int? DurationSeconds = null,
    bool IsInteraction = false, string? AssistantMessage = null);
public sealed record HrQuestionDto(Guid Id, Guid SkillId, string Text, string Topic, int Difficulty);
public sealed record HrSkillStateDto(Guid SkillId, decimal CurrentScore, int EvidenceCount, decimal Confidence);
public sealed record HrEvidenceDto(Guid AnswerId, Guid QuestionId, Guid SkillId, decimal ScoreBefore, decimal ScoreAfter,
    decimal Delta, decimal Confidence, string Evidence, DateTimeOffset CreatedAtUtc);
public sealed record HrTopicDto(Guid SkillId, string Topic, decimal Coverage, int EvidenceCount);
public sealed record HrIntroductionDto(Guid Id, string Status, string? Transcript, DateTimeOffset SubmittedAtUtc,
    string AudioUrl, string? VideoUrl);
public sealed record HrInterviewDto(Guid Id, Guid CandidateApplicationId, Guid JobPositionId, string Status,
    string? CompletionReason, DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<HrQuestionDto> Questions, IReadOnlyList<HrAnswerDto> Answers,
    IReadOnlyList<HrSkillStateDto> SkillStates, IReadOnlyList<HrEvidenceDto> Evidence,
    IReadOnlyList<HrTopicDto> CoveredTopics, string? CandidateIntroductionTranscript = null, HrIntroductionDto? Introduction = null,
    bool DiagnosticsEnabled = false, string? DiagnosticsUrl = null, bool DiagnosticPromptsEnabled = false);
public interface ICurrentInterviewAccess { Guid InterviewId { get; } }
public sealed record AnswerCapture(string? ClientTurnId, DateTimeOffset? LastSpeechEndUtc,
    Stream? Video = null, string? VideoFileName = null, string? VideoContentType = null, long VideoLength = 0);
