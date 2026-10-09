using HireYar.Domain.Interviews;

namespace HireYar.Application.Interviews;

public static class InterviewReportDetailsBuilder
{
    public static InterviewReportContent Build(InterviewReportContent content, InterviewSession session)
    {
        var evaluations = content.QuestionEvaluations!.ToDictionary(e => e.AnswerId);
        var questions = session.Questions.Where(q => q.AskedAtUtc is not null || session.Answers.Any(a => a.QuestionId == q.QuestionId))
            .OrderBy(q => q.AskedAtUtc).ThenBy(q => q.Order).Select(q => {
                var answer = session.Answers.SingleOrDefault(a => a.QuestionId == q.QuestionId && !a.IsInteraction && a.Status == AnswerStatus.Processed);
                var evaluation = answer is null ? null : evaluations.GetValueOrDefault(answer.Id);
                var route = answer is null ? null : $"/api/interviews/{session.Id}/answers/{answer.Id}";
                return new InterviewReportQuestion(q.QuestionId, q.Text, q.Topic, q.Difficulty, q.AskedAtUtc,
                    answer?.Id, answer?.Transcript, evaluation?.Score, evaluation?.Confidence, evaluation?.Feedback,
                    evaluation?.Quote, route is null ? null : route + "/audio",
                    answer?.VideoStorageKey is null ? null : route + "/video");
            }).ToArray();
        var scored = questions.Where(q => q.Score is not null).ToArray();
        decimal? Mean(IEnumerable<InterviewReportQuestion> items) {
            var values = items.Where(q => q.Score is not null).Select(q => (decimal)q.Score!.Value).ToArray();
            return values.Length == 0 ? null : Math.Round(values.Average() * 20, 2);
        }
        var confidence = scored.Length == 0 ? (decimal?)null : Math.Round(scored.Average(q => q.Confidence!.Value), 4);
        return content with { Details = new(session.RequiredCapabilityCode == "hr_interview" ? "general" : "specialized",
            questions.Length, session.Questions.Count, scored.Length, Mean(scored), confidence,
            scored.Length != questions.Length || confidence is null or < 0.7m || scored.Any(q => q.Confidence < 0.7m) ||
                session.Answers.Any(a => a.SelectionSource == "Fallback"),
            scored.Count(q => q.Score >= 4), scored.Count(q => q.Score == 3), scored.Count(q => q.Score <= 2),
            scored.Length == 0 ? null : Math.Round(scored.Count(q => q.Score >= 3) * 100m / scored.Length, 2), questions,
            questions.GroupBy(q => q.Topic).Select(g => new InterviewReportTopic(g.Key, Mean(g), g.Max(q => q.Difficulty), g.Count(q => q.Score is not null))).ToArray(),
            evaluations.Values.SelectMany(e => e.Highlights).Distinct().Take(5).ToArray(),
            evaluations.Values.SelectMany(e => e.Gaps).Distinct().Take(4).ToArray()) };
    }
}
