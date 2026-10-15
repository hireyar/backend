namespace HireYar.Application.Interviews;

public sealed record ReportAnswerAssessment(string State, decimal? Value, decimal? Confidence, string? Formula,
    string? Explanation, IReadOnlyList<AnswerAssessmentEvidence> Evidence, int Attempts, bool CanRetry,
    string? FailureCode, DateTimeOffset? RequestedAtUtc, DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc, DateTimeOffset? NextAttemptAtUtc, string? EvaluatorVersion,
    string? Provider, string? Model);
public sealed record AnswerAssessmentEvidence(Guid AnswerId, int Start, int Length, string Quote);
