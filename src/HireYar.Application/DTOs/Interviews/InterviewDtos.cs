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
public sealed record CreateInterviewRequest(Guid TemplateId, [Range(1, 30)] int MaxQuestionCount = 12, [Range(1, 168)] int ExpiresInHours = 72);
public sealed record InterviewInvitation(Guid InterviewId, string Token, DateTimeOffset ExpiresAtUtc, string CandidateRoute);
public sealed record InterviewProgress(int AnsweredQuestions, int MaxQuestions, int RemainingSeconds);
public sealed record CandidateQuestionDto(Guid Id, string Text, string? AudioUrl, bool AudioAvailable);
public sealed record CandidateAnswerDto(Guid Id, Guid QuestionId, string Status, bool CanRetry, string? ErrorCode);
public sealed record CandidateInterviewDto(Guid InterviewId, string Status, bool CanRecord, bool IsProcessing,
    CandidateQuestionDto? Question, InterviewProgress Progress, CandidateAnswerDto? Answer, string? CompletionReason);
public sealed record HrAnswerDto(Guid Id, Guid QuestionId, string Status, string? Transcript, string? SelectionSource,
    string? FailureStage, string? FailureCode, string AudioUrl);
public sealed record HrQuestionDto(Guid Id, Guid SkillId, string Text, string Topic, int Difficulty);
public sealed record HrSkillStateDto(Guid SkillId, decimal CurrentScore, int EvidenceCount, decimal Confidence);
public sealed record HrEvidenceDto(Guid AnswerId, Guid QuestionId, Guid SkillId, decimal ScoreBefore, decimal ScoreAfter,
    decimal Delta, decimal Confidence, string Evidence, DateTimeOffset CreatedAtUtc);
public sealed record HrTopicDto(Guid SkillId, string Topic, decimal Coverage, int EvidenceCount);
public sealed record HrInterviewDto(Guid Id, Guid CandidateApplicationId, Guid JobPositionId, string Status,
    string? CompletionReason, DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<HrQuestionDto> Questions, IReadOnlyList<HrAnswerDto> Answers,
    IReadOnlyList<HrSkillStateDto> SkillStates, IReadOnlyList<HrEvidenceDto> Evidence,
    IReadOnlyList<HrTopicDto> CoveredTopics);
public interface ICurrentInterviewAccess { Guid InterviewId { get; } }
