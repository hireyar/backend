namespace HireYar.Application.Interviews;

public sealed record ReportAnswerAssessment(string State, decimal? Value, decimal? Confidence, string? Formula,
    string? Explanation, IReadOnlyList<AnswerAssessmentEvidence> Evidence, int Attempts, bool CanRetry,
    string? FailureCode, DateTimeOffset? RequestedAtUtc, DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc, DateTimeOffset? NextAttemptAtUtc, string? EvaluatorVersion,
    string? Provider, string? Model, IReadOnlyList<AnswerDimension>? Dimensions = null)
{
    public bool IsPending => State is "Queued" or "Processing" or "RecoveryPending";
}
public sealed record RetryAnswerAssessmentResponse(Guid InterviewId, Guid AnswerId, ReportAnswerAssessment Assessment);
public sealed record AnswerAssessmentEvidence(Guid AnswerId, int Start, int Length, string Quote);
