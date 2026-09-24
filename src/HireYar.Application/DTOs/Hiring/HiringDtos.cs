using System.ComponentModel.DataAnnotations;
using HireYar.Domain.Hiring;
namespace HireYar.Application.Hiring;

public sealed record CandidateRequest([Required, StringLength(200)] string FullName, [Required, EmailAddress, StringLength(256)] string Email, [StringLength(30)] string? PhoneNumber);
public sealed record UpdateCandidateRequest([Required] CandidateRequest Candidate, [Required, MinLength(16), MaxLength(16)] byte[] RowVersion);
public sealed record CandidateDto(Guid Id, string FullName, string Email, string? PhoneNumber, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, byte[] RowVersion);
public sealed record ApplicationRequest(Guid CandidateId, Guid JobPositionId);
public sealed record ApplicationStatusRequest([EnumDataType(typeof(ApplicationStatus))] ApplicationStatus Status, [Required, MinLength(16), MaxLength(16)] byte[] RowVersion);
public sealed record ApplicationDto(Guid Id, Guid CandidateId, Guid JobPositionId, ApplicationStatus Status, DateTimeOffset CreatedAtUtc, byte[] RowVersion);
public sealed record CreateSessionRequest(Guid JobApplicationId, Guid InterviewTemplateId, DateTimeOffset AvailableFromUtc, DateTimeOffset ExpiresAtUtc, [Required, StringLength(100, MinimumLength = 8)] string IdempotencyKey);
public sealed record VersionRequest([Required, MinLength(16), MaxLength(16)] byte[] RowVersion);
public sealed record InvitationDto(DeliveryStatus DeliveryStatus, int SendAttempts, DateTimeOffset? LastSentAtUtc, DateTimeOffset? UsedAtUtc, DateTimeOffset? RevokedAtUtc);
public sealed record SessionListDto(Guid Id, Guid JobApplicationId, SessionStatus Status, string CandidateName, string JobTitle, string InterviewTitle, DateTimeOffset AvailableFromUtc, DateTimeOffset ExpiresAtUtc, DateTimeOffset? DeadlineAtUtc, byte[] RowVersion, InvitationDto Invitation);
public sealed record HrQuestionDto(Guid Id, int Order, string Text, string Competency, string EvaluationGuidance);
public sealed record AnswerDto(Guid Id, Guid SnapshotQuestionId, string? Text, AnswerStatus Status, DateTimeOffset UpdatedAtUtc, DateTimeOffset? SubmittedAtUtc, byte[] RowVersion);
public sealed record HrSessionDto(SessionListDto Session, string OrganizationName, string CandidateEmail, string Language, int DurationMinutes, int MaxFollowUpQuestionsPerQuestion, DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc, DateTimeOffset? CancelledAtUtc, DateTimeOffset? LastActivityAtUtc, string? ConsentVersion, DateTimeOffset? ConsentAcceptedAtUtc, IReadOnlyList<HrQuestionDto> Questions, IReadOnlyList<AnswerDto> Answers);
public sealed record InvitationTokenRequest([Required, StringLength(128, MinimumLength = 32)] string Token);
public sealed record ConsentRequest(bool Accepted, [Required, StringLength(50)] string Version);
public sealed record AnswerRequest([EnumDataType(typeof(AnswerStatus))] AnswerStatus Status, [StringLength(10000)] string? Text, [MinLength(16), MaxLength(16)] byte[]? RowVersion);
public sealed record PublicQuestionDto(Guid Id, int Order, string Text);
public sealed record PublicSessionDto(Guid Id, string OrganizationName, string JobTitle, string InterviewTitle, string Language, int DurationMinutes, DateTimeOffset AvailableFromUtc, DateTimeOffset ExpiresAtUtc, SessionStatus Status, DateTimeOffset? StartedAtUtc, DateTimeOffset? DeadlineAtUtc, DateTimeOffset? CompletedAtUtc, string ConsentVersion, string ConsentText, PublicQuestionDto? CurrentQuestion, IReadOnlyList<AnswerDto> Answers);
// Internal transport to the HTTP cookie boundary; never returned as an API response.
public sealed record CandidateExchange(string? Credential, DateTimeOffset ExpiresAtUtc, PublicSessionDto Session);
