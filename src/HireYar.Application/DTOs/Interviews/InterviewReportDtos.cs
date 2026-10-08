using System.Text.Json.Serialization;

namespace HireYar.Application.Interviews;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InterviewReportContent
{
    public required string Summary { get; init; }
    public required IReadOnlyList<InterviewReportFinding> Findings { get; init; }
    public required IReadOnlyList<string> Limitations { get; init; }
}

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
    IReadOnlyList<HrQuestionDto> Questions);
