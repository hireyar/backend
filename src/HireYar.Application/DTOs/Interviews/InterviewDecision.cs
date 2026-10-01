using System.Text.Json.Serialization;
using HireYar.Domain.Interviews;

namespace HireYar.Application.Interviews;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InterviewDecision
{
    public required AnswerAssessment AnswerAssessment { get; init; }
    public required IReadOnlyList<SkillDecision> SkillUpdates { get; init; }
    public required IReadOnlyList<TopicDecision> CoveredTopics { get; init; }
    public required Guid? NextQuestionId { get; init; }
    public required bool ShouldComplete { get; init; }
    public required string Reason { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnswerAssessment
{
    public required decimal Relevance { get; init; }
    public required decimal Clarity { get; init; }
    public required decimal EvidenceQuality { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SkillDecision
{
    public required Guid SkillId { get; init; }
    public required decimal Delta { get; init; }
    public required decimal Confidence { get; init; }
    public required string Evidence { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TopicDecision
{
    public required string Topic { get; init; }
    public required decimal Coverage { get; init; }
}

public sealed record ValidatedInterviewDecision(IReadOnlyList<SkillAssessment> SkillUpdates,
    IReadOnlyList<TopicAssessment> CoveredTopics, Guid? NextQuestionId, bool UsedFallback,
    InterviewCompletionReason? CompletionReason);
