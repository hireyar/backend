namespace HireYar.Domain.Interviews;

public sealed record SkillEvidence(Guid Id, Guid InterviewSessionId, Guid InterviewAnswerId,
    Guid QuestionId, Guid SkillId, decimal ScoreBefore, decimal ScoreAfter, decimal Delta,
    decimal Confidence, string Evidence, DateTimeOffset CreatedAtUtc);

public sealed class CandidateSkillState
{
    private CandidateSkillState() { }
    public Guid InterviewSessionId { get; internal set; }
    public Guid SkillId { get; private set; }
    public decimal CurrentScore { get; private set; }
    public int EvidenceCount { get; private set; }
    public decimal Confidence { get; private set; }
    public DateTimeOffset? LastUpdatedAtUtc { get; private set; }

    internal CandidateSkillState(Guid skillId) => SkillId = skillId;

    internal SkillEvidence Apply(Guid sessionId, InterviewAnswer answer, decimal delta,
        decimal confidence, string evidence, DateTimeOffset now)
    {
        var before = CurrentScore;
        CurrentScore = Math.Clamp(before + delta, 0, 1);
        Confidence = confidence; EvidenceCount++; LastUpdatedAtUtc = now;
        return new(Guid.NewGuid(), sessionId, answer.Id, answer.QuestionId, SkillId,
            before, CurrentScore, CurrentScore - before, confidence, evidence.Trim(), now);
    }
}

public sealed class InterviewCoveredTopic
{
    private InterviewCoveredTopic() { }
    public Guid InterviewSessionId { get; internal set; }
    public Guid SkillId { get; private set; }
    public string Topic { get; private set; } = "";
    public decimal Coverage { get; private set; }
    public int EvidenceCount { get; private set; }
    public DateTimeOffset LastUpdatedAtUtc { get; private set; }

    internal InterviewCoveredTopic(Guid skillId, string topic) { SkillId = skillId; Topic = topic; }
    internal void Apply(decimal coverage, DateTimeOffset now)
    {
        Coverage = Math.Max(Coverage, coverage); EvidenceCount++; LastUpdatedAtUtc = now;
    }
}

public sealed record SkillAssessment(Guid SkillId, decimal Delta, decimal Confidence, string Evidence);
public sealed record TopicAssessment(Guid SkillId, string Topic, decimal Coverage);
