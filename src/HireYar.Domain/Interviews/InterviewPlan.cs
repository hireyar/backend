namespace HireYar.Domain.Interviews;

// Session-owned snapshots keep historical questions stable when HR edits a template.
public sealed record InterviewQuestionSnapshot
{
    private InterviewQuestionSnapshot() { }
    public Guid InterviewSessionId { get; internal set; }
    public Guid QuestionId { get; private set; }
    public Guid SkillId { get; private set; }
    public string Text { get; private set; } = "";
    public string Topic { get; private set; } = "";
    public int Difficulty { get; private set; }
    public bool IsActive { get; private set; }
    public int Order { get; private set; }
    public DateTimeOffset? AskedAtUtc { get; private set; }
    internal void MarkAsked(DateTimeOffset now) { InterviewRules.Utc(now); AskedAtUtc ??= now; }

    public InterviewQuestionSnapshot(Guid questionId, Guid skillId, string text, string topic, int difficulty, bool isActive, int order = 0)
    {
        InterviewRules.Require(questionId != Guid.Empty && skillId != Guid.Empty, "invalid_id");
        InterviewRules.Require(difficulty is >= 1 and <= 5, "invalid_difficulty");
        QuestionId = questionId; SkillId = skillId;
        Text = InterviewRules.Text(text, 2000); Topic = InterviewRules.Text(topic, 200);
        Difficulty = difficulty; IsActive = isActive;
        InterviewRules.Require(order is >= 0 and <= 30, "invalid_question_order"); Order = order;
    }
}

public sealed record InterviewSkill
{
    private InterviewSkill() { }
    public Guid InterviewSessionId { get; internal set; }
    public Guid SkillId { get; private set; }
    public string Name { get; private set; } = "";
    public int TargetDifficulty { get; private set; } = 3;
    public bool IsRequired { get; private set; }
    public int Priority { get; private set; }

    public InterviewSkill(Guid skillId, string name, bool isRequired, int priority, int targetDifficulty = 3)
    {
        InterviewRules.Require(skillId != Guid.Empty && priority is >= 0 and <= 100, "invalid_skill");
        SkillId = skillId; Name = InterviewRules.Text(name, 200); IsRequired = isRequired; Priority = priority;
        InterviewRules.Require(targetDifficulty is >= 1 and <= 5, "invalid_difficulty"); TargetDifficulty = targetDifficulty;
    }
}
