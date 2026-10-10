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
        if (session.ProgressiveTopicQuestions)
        {
            if (session.MediumFirstTopicQuestions)
            {
                var allowed = session.GetProgressiveQuestions().OrderBy(q => q.Order).ThenBy(q => q.QuestionId);
                var pendingMedium = session.CurrentQuestionId is { } medium && session.Questions.Single(q => q.QuestionId == medium).Difficulty == 3 &&
                    !session.Answers.Any(a => a.QuestionId == medium && a.Status == AnswerStatus.Processed && !a.IsInteraction);
                return allowed.Take(session.RequiredCapabilityCode == "hr_interview" && !pendingMedium ? 1 : shortlistLimit).ToArray();
            }
            var progressive = session.GetProgressiveQuestions().Where(q => !asked.Contains(q.QuestionId))
                .OrderByDescending(q => session.CurrentQuestionId is { } id &&
                    session.Questions.Single(x => x.QuestionId == id).Topic == q.Topic)
                .ThenByDescending(q => session.Skills.Single(s => s.SkillId == q.SkillId).IsRequired)
                .ThenByDescending(q => session.Skills.Single(s => s.SkillId == q.SkillId).Priority)
                .ThenBy(q => q.Order).ThenBy(q => q.QuestionId);
            // Keep both pass/fail branches during evaluation; HR topic order is fixed.
            return (session.RequiredCapabilityCode == "hr_interview"
                ? progressive.Where(q => q.Topic == progressive.FirstOrDefault()?.Topic || q.Difficulty == 1)
                    .GroupBy(q => q.Difficulty == 1).Select(g => g.First())
                : progressive).Take(shortlistLimit).ToArray();
        }
        if (session.RequiredCapabilityCode == "hr_interview")
            return session.Questions.Where(q => q.IsActive && !asked.Contains(q.QuestionId) &&
                (!session.AskOneQuestionPerTopic || q.Difficulty == 3 && !session.Questions.Any(a => asked.Contains(a.QuestionId) && a.Topic == q.Topic)))
                .OrderBy(q => q.Order).ThenBy(q => q.QuestionId).Take(1).ToArray();
        var askedTopics = session.Questions.Where(q => asked.Contains(q.QuestionId)).Select(q => q.Topic).ToHashSet();
        var covered = session.CoveredTopics.Where(x => x.Coverage >= coverageThreshold)
            .Select(x => (x.SkillId, x.Topic)).ToHashSet();
        var candidates = session.Questions.Where(q => q.IsActive && !asked.Contains(q.QuestionId) &&
                (!session.AskOneQuestionPerTopic || !askedTopics.Contains(q.Topic)) && !covered.Contains((q.SkillId, q.Topic)) && session.Skills.Any(s => s.SkillId == q.SkillId))
            .OrderBy(q => session.SkillStates.Single(s => s.SkillId == q.SkillId).EvidenceCount)
            .ThenByDescending(q => session.Skills.Single(s => s.SkillId == q.SkillId).IsRequired)
            .ThenByDescending(q => session.Skills.Single(s => s.SkillId == q.SkillId).Priority)
            .ThenBy(q => Math.Abs(q.Difficulty - (adaptDifficulty ? TargetDifficulty(session, q.SkillId) : session.Skills.Single(s => s.SkillId == q.SkillId).TargetDifficulty)))
            .ThenBy(q => q.Order).ThenBy(q => q.QuestionId);
        return (session.AskOneQuestionPerTopic ? candidates.DistinctBy(q => q.Topic) : candidates).Take(shortlistLimit).ToArray();
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
        if (!session.ProgressiveTopicQuestions && session.RequiredCapabilityCode != "hr_interview" && required.Length > 0 && required.All(s => session.SkillStates.Any(x => x.SkillId == s.SkillId &&
                x.EvidenceCount >= minimumEvidenceCount && x.Confidence >= confidenceThreshold)))
            return InterviewCompletionReason.SufficientEvidence;
        return GetAllowedQuestions(session, now).Count == 0 ? InterviewCompletionReason.NoQuestions : null;
    }

    private static int TargetDifficulty(InterviewSession session, Guid skillId)
    {
        if (session.AskOneQuestionPerTopic)
        {
            var observed = session.SkillStates.Where(s => s.EvidenceCount > 0).ToArray();
            return observed.Length == 0 ? 3 : 1 + (int)Math.Round(observed.Average(s => s.CurrentScore) * 4);
        }
        var state = session.SkillStates.Single(x => x.SkillId == skillId);
        return state.EvidenceCount == 0 ? session.Skills.Single(x => x.SkillId == skillId).TargetDifficulty : 1 + (int)Math.Round(state.CurrentScore * 4);
    }

    private static void Validate(int shortlistLimit, decimal coverageThreshold)
    {
        if (shortlistLimit is < 5 or > 20 || coverageThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(shortlistLimit));
    }
}
