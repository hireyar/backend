using System.Text.Json.Serialization;

namespace HireYar.Application.Interviews;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InterviewReportContent
{
    public required string Summary { get; init; }
    public required IReadOnlyList<InterviewReportFinding> Findings { get; init; }
    public required IReadOnlyList<string> Limitations { get; init; }
    public IReadOnlyList<InterviewQuestionEvaluation>? QuestionEvaluations { get; init; }
    public IReadOnlyList<string>? NextRoundFocus { get; init; }
    public InterviewReportDetails? Details { get; init; }
    public IReadOnlyList<InterviewReportMetric>? Metrics { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InterviewReportMetric([property: JsonRequired] string Code, [property: JsonRequired] int? Score,
    [property: JsonRequired] decimal? Confidence, [property: JsonRequired] string Explanation,
    [property: JsonRequired] IReadOnlyList<InterviewReportCitation> Evidence);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InterviewReportCitation([property: JsonRequired] Guid AnswerId, [property: JsonRequired] string Quote);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InterviewQuestionEvaluation([property: JsonRequired] Guid AnswerId,
    [property: JsonRequired] int Score, [property: JsonRequired] decimal Confidence,
    [property: JsonRequired] string Feedback, [property: JsonRequired] string Quote,
    [property: JsonRequired] IReadOnlyList<string> Highlights, [property: JsonRequired] IReadOnlyList<string> Gaps);

public sealed record InterviewReportDetails(string InterviewType, int AskedCount, int BankCount, int EvaluatedCount,
    decimal? OverallScore, decimal? Confidence, bool RequiresHumanReview, int ExcellentCount, int GoodCount,
    int WeakCount, decimal? AboveThresholdPercent, IReadOnlyList<InterviewReportQuestion> Questions,
    IReadOnlyList<InterviewReportTopic> Topics, IReadOnlyList<string> Strengths, IReadOnlyList<string> Gaps)
{
    public IReadOnlyList<InterviewReportMetric>? Metrics { get; init; }
    public decimal? FitScore { get; init; }
    public decimal? AverageAnswerSeconds { get; init; }
    public string ScoreFormula { get; init; } = "mean(answerScore)*20";
    public IReadOnlyDictionary<string, decimal>? TopicWeights { get; init; }
    public decimal? ReviewThreshold { get; init; }
}
public sealed record InterviewReportQuestion(Guid QuestionId, string Text, string Topic, int Difficulty,
    DateTimeOffset? AskedAtUtc, Guid? AnswerId, string? Transcript, int? Score, decimal? Confidence,
    string? Feedback, string? Quote, string? AudioUrl, string? VideoUrl);
public sealed record InterviewReportTopic(string Topic, decimal? Score, int ReachedDifficulty, int EvaluatedCount);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InterviewReportFinding
{
    public required Guid AnswerId { get; init; }
    public required string Quote { get; init; }
    public required string Observation { get; init; }
    public required string Kind { get; init; }
}

public sealed record InterviewReportDto(Guid InterviewId, string Model, string Provider, string PromptVersion,
    DateTimeOffset CreatedAtUtc, InterviewReportContent Content);
public sealed record InterviewReportAnswer(Guid AnswerId, Guid QuestionId, string Question, string? Transcript,
    bool IsInteraction, string? AssistantReply, string? SelectionSource = null);
public sealed record InterviewReportContext(string PositionTitle, string? PositionDescription, string? Resume,
    string? Introduction, string Language, string? CompletionReason, IReadOnlyList<InterviewReportAnswer> Answers,
    IReadOnlyList<HrEvidenceDto> Evidence, IReadOnlyList<HrSkillStateDto> SkillStates,
    IReadOnlyList<HrQuestionDto> Questions, string InterviewType = "specialized");

public sealed record GenerateInterviewReportRequest(IReadOnlyDictionary<string, decimal>? TopicWeights = null,
    decimal? ReviewThreshold = null);

public sealed record InterviewReportGenerationDto(Guid InterviewId, string Status, string Stage, string Message,
    bool CanRetry, int Attempts, DateTimeOffset? RequestedAtUtc, DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc, string? FailureCode, string? FailureStage, bool ReportAvailable);
