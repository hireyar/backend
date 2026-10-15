namespace HireYar.Application.Interviews;

public sealed record ReportSkillWeight(Guid SkillId, decimal Weight);
public sealed record ReportPolicyRequest(Guid Revision, decimal PassingScore, decimal StrongScore,
    decimal MinimumCoverage, IReadOnlyList<ReportSkillWeight> Weights);
public sealed record ReportPolicy(string State, Guid Revision, decimal PassingScore, decimal StrongScore,
    decimal MinimumCoverage, IReadOnlyList<ReportSkillWeight> Weights, string WeightSource, bool CanEdit, string? WarningCode);
public sealed record ReportCoverage(int AskedQuestions, int ScoredQuestions, int UnansweredQuestions,
    int FailedAssessments, int PendingAssessments, decimal Ratio, bool IsProvisional);
public sealed record ReportFinding(Guid QuestionId, Guid AnswerId, string Topic, decimal Score,
    string Text, IReadOnlyList<AnswerAssessmentEvidence> Evidence);
public sealed record ReportGap(string Code, Guid? QuestionId, Guid? SkillId, string Message);
public sealed record ReportDistribution(int Strong, int Satisfactory, int NeedsImprovement, int Unscored,
    decimal PassingScore, decimal StrongScore);
public sealed record ReportMetric(string Code, string Title, string State, decimal? Value, decimal? Confidence,
    int ScoredAnswers, int ExpectedAnswers, IReadOnlyList<Guid> QuestionIds,
    IReadOnlyList<AnswerAssessmentEvidence> Evidence, string Message);
public sealed record ReportProfileAxis(Guid SkillId, string Topic, string State, decimal? Value, decimal? Confidence,
    int AskedQuestions, int ScoredQuestions, IReadOnlyList<int> DifficultyLevels,
    IReadOnlyList<Guid> QuestionIds, IReadOnlyList<AnswerAssessmentEvidence> Evidence);
public sealed record ReportProfile(string State, string Kind, IReadOnlyList<ReportProfileAxis> Axes,
    IReadOnlyList<ReportMetric> Metrics, ReportDistribution Distribution, string Message);
public sealed record ReportSkillFit(Guid SkillId, string Name, bool IsRequired, int TargetDifficulty, decimal Weight,
    string State, decimal? Value, int QualifyingQuestions, IReadOnlyList<Guid> QuestionIds);
public sealed record ReportPositionFit(string State, decimal? Value, decimal? Confidence, string Formula,
    string Classification, decimal Coverage, IReadOnlyList<ReportSkillFit> Skills, string Message);
public sealed record ReportReviewDecision(Guid Id, Guid ActorUserId, string Decision, string? Note,
    DateTimeOffset RecordedAtUtc, string? ReportFingerprint, bool MatchesCurrentReport, ReportReviewSnapshot? Snapshot = null);
public sealed record ReportHumanReview(string State, bool CanSubmit, string SubmitUrl, string ReportFingerprint,
    ReportReviewDecision? Latest, IReadOnlyList<ReportReviewDecision> History, string Message);
public sealed record ReportReviewSnapshot(string Version, ReportScore Score, ReportCoverage Coverage, ReportPositionFit Fit, ReportPolicy Policy);
public sealed record ReportDecisionRequest(Guid RequestId, string Decision, string ReportFingerprint, string? Note = null);
public sealed record AnswerDimension(string Code, string State, decimal? Value, decimal? Confidence,
    string? Explanation, IReadOnlyList<AnswerAssessmentEvidence> Evidence, string? FailureCode);
