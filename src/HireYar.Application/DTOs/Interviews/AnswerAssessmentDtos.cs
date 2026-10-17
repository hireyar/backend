namespace HireYar.Application.Interviews;

public sealed record ReportAnswerAssessment(string State, decimal? Value, decimal? Confidence, string? Formula,
    string? Explanation, IReadOnlyList<AnswerAssessmentEvidence> Evidence, int Attempts, bool CanRetry,
    string? FailureCode, DateTimeOffset? RequestedAtUtc, DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc, DateTimeOffset? NextAttemptAtUtc, string? EvaluatorVersion,
    string? Provider, string? Model, IReadOnlyList<AnswerDimension>? Dimensions = null, ReportDimensionRecovery? DimensionRecovery = null)
{
    public bool IsPending => State is "Queued" or "Processing" or "RecoveryPending";
}
public sealed record RetryAnswerAssessmentResponse(Guid InterviewId, Guid AnswerId, ReportAnswerAssessment Assessment);
public sealed record AnswerAssessmentEvidence(Guid AnswerId, int Start, int Length, string Quote);
public sealed record ReportDimensionRecovery(string State, int Attempts, bool CanRetry, string? FailureCode)
{
    public bool ShouldPoll => State == "Processing";
}
