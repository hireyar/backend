using System.Text.Json;
using HireYar.Domain.Interviews;

namespace HireYar.Application.Interviews;

public sealed class InterviewDecisionValidator(InterviewPolicy policy)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // The supplied shortlist is intersected with current backend policy; provider recommendations cannot widen it.
    public ValidatedInterviewDecision ValidateOrFallback(string? json, InterviewSession session,
        IReadOnlyList<Guid> suppliedQuestionIds, DateTimeOffset now)
    {
        var asked = session.Answers.Select(x => x.QuestionId).Append(session.CurrentQuestionId ?? Guid.Empty).ToHashSet();
        var allowed = suppliedQuestionIds.Distinct().Select(id => session.Questions.SingleOrDefault(x => x.QuestionId == id))
            .Where(x => x is not null && x.IsActive && !asked.Contains(x.QuestionId)).Select(x => x!).ToArray();
        var completion = policy.GetCompletionReason(session, now);
        var fallback = new ValidatedInterviewDecision([], [], completion is null ? allowed.FirstOrDefault()?.QuestionId : null,
            true, completion);
        if (string.IsNullOrWhiteSpace(json) || json.Length > 32000) return fallback;
        InterviewDecision? decision;
        try { decision = JsonSerializer.Deserialize<InterviewDecision>(json, JsonOptions); }
        catch (JsonException) { return fallback; }
        if (decision is null || decision.AnswerAssessment is null || decision.SkillUpdates is null || decision.CoveredTopics is null ||
            string.IsNullOrWhiteSpace(decision.Reason) || decision.Reason.Length > 500) return fallback;
        var question = session.Questions.SingleOrDefault(x => x.QuestionId == session.CurrentQuestionId);
        if (question is null || !Unit(decision.AnswerAssessment.Relevance) || !Unit(decision.AnswerAssessment.Clarity) ||
            !Unit(decision.AnswerAssessment.EvidenceQuality) || decision.SkillUpdates.Count > 1 || decision.CoveredTopics.Count > 1)
            return fallback;
        if (decision.SkillUpdates.Any(x => x is null || x.SkillId != question.SkillId || !Unit(x.Confidence) ||
                x.Delta is < -1 or > 1 || string.IsNullOrWhiteSpace(x.Evidence) || x.Evidence.Length > 2000) ||
            decision.CoveredTopics.Any(x => x is null || x.Topic != question.Topic || !Unit(x.Coverage))) return fallback;
        if (decision.NextQuestionId is { } next && allowed.All(x => x.QuestionId != next)) return fallback;
        if (decision.Action is not ("advance" or "clarify" or "followUp" or "redirect")) return fallback;
        if (decision.Action != "advance")
        {
            if (session.RemainingInteractions == 0 || session.RemainingSeconds(now) < 15 ||
                string.IsNullOrWhiteSpace(decision.CandidateMessage) || decision.CandidateMessage.Length > 2000 ||
                decision.NextQuestionId is not null || decision.ShouldComplete || decision.SkillUpdates.Count > 0 || decision.CoveredTopics.Count > 0) return fallback;
            return new([], [], null, false, null, decision.Action, decision.CandidateMessage);
        }
        if (decision.CandidateMessage is not null) return fallback;
        var assessment = decision.AnswerAssessment;
        if ((assessment.Correctness is null) != (assessment.Confidence is null) ||
            assessment.Correctness is { } score && !Unit(score) || assessment.Confidence is { } confidence && !Unit(confidence) ||
            session.ProgressiveTopicQuestions && assessment.Correctness is null) return fallback;
        if (completion is null && decision.NextQuestionId is null && !session.ProgressiveTopicQuestions) return fallback;
        return new(decision.SkillUpdates.Select(x => new SkillAssessment(x.SkillId, x.Delta, x.Confidence, x.Evidence)).ToArray(),
            decision.CoveredTopics.Select(x => new TopicAssessment(question.SkillId, x.Topic, x.Coverage)).ToArray(),
            completion is null ? decision.NextQuestionId : null, false, completion,
            Correctness: assessment.Correctness, AssessmentConfidence: assessment.Confidence);
    }

    private static bool Unit(decimal value) => value is >= 0 and <= 1;
}
