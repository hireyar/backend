using HireYar.Domain.Interviews;

namespace HireYar.Application.Interviews;

public sealed class InterviewPolicy
{
    public IReadOnlyList<InterviewQuestionSnapshot> GetAllowedQuestions(InterviewSession session, DateTimeOffset now,
        int shortlistLimit = 10, decimal coverageThreshold = 0.8m, bool adaptDifficulty = true)
    {
        Validate(shortlistLimit, coverageThreshold);
        if (session.Status is InterviewStatus.Completed or InterviewStatus.Cancelled ||
            session.AnsweredQuestionCount >= session.MaxQuestionCount || session.RemainingSeconds(now) == 0)
            return [];
        var asked = session.Answers.Select(x => x.QuestionId).ToHashSet();
        if (session.CurrentQuestionId is { } current) asked.Add(current);
        var covered = session.CoveredTopics.Where(x => x.Coverage >= coverageThreshold)
            .Select(x => (x.SkillId, x.Topic)).ToHashSet();
        return session.Questions.Where(q => q.IsActive && !asked.Contains(q.QuestionId) &&
                !covered.Contains((q.SkillId, q.Topic)) && session.Skills.Any(s => s.SkillId == q.SkillId))
            .OrderBy(q => session.SkillStates.Single(s => s.SkillId == q.SkillId).EvidenceCount)
            .ThenByDescending(q => session.Skills.Single(s => s.SkillId == q.SkillId).IsRequired)
            .ThenByDescending(q => session.Skills.Single(s => s.SkillId == q.SkillId).Priority)
            .ThenBy(q => Math.Abs(q.Difficulty - (adaptDifficulty ? TargetDifficulty(session, q.SkillId) : session.Skills.Single(s => s.SkillId == q.SkillId).TargetDifficulty)))
            .ThenBy(q => q.QuestionId).Take(shortlistLimit).ToArray();
    }

    public InterviewQuestionSnapshot? SelectFallback(InterviewSession session, DateTimeOffset now, int shortlistLimit = 10) =>
        GetAllowedQuestions(session, now, shortlistLimit).FirstOrDefault();

    public InterviewCompletionReason? GetCompletionReason(InterviewSession session, DateTimeOffset now,
        int minimumEvidenceCount = 2, decimal confidenceThreshold = 0.8m)
    {
        if (minimumEvidenceCount is < 1 or > 30 || confidenceThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(minimumEvidenceCount));
        if (session.Status is InterviewStatus.Completed or InterviewStatus.Cancelled) return null;
        if (session.RemainingSeconds(now) == 0) return InterviewCompletionReason.DurationLimit;
        if (session.AnsweredQuestionCount >= session.MaxQuestionCount) return InterviewCompletionReason.QuestionLimit;
        var required = session.Skills.Where(x => x.IsRequired).ToArray();
        if (required.Length > 0 && required.All(s => session.SkillStates.Any(x => x.SkillId == s.SkillId &&
                x.EvidenceCount >= minimumEvidenceCount && x.Confidence >= confidenceThreshold)))
            return InterviewCompletionReason.SufficientEvidence;
        return GetAllowedQuestions(session, now).Count == 0 ? InterviewCompletionReason.NoQuestions : null;
    }

    private static int TargetDifficulty(InterviewSession session, Guid skillId)
    {
        var state = session.SkillStates.Single(x => x.SkillId == skillId);
        return state.EvidenceCount == 0 ? session.Skills.Single(x => x.SkillId == skillId).TargetDifficulty : 1 + (int)Math.Round(state.CurrentScore * 4);
    }

    private static void Validate(int shortlistLimit, decimal coverageThreshold)
    {
        if (shortlistLimit is < 5 or > 20 || coverageThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(shortlistLimit));
    }
}
