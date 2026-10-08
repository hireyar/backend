using System.Text.RegularExpressions;
using HireYar.Domain.Interviews;

namespace HireYar.Application.Interviews;

public sealed record SelectedInterviewHistory(IReadOnlyList<InterviewDialogueTurn> Turns, int TotalTurns)
{
    public bool IsPartial => Turns.Count < TotalTurns;
}

// No network call or lossy summary: select whole persisted turns and preserve current-question corrections.
public static class InterviewHistorySelector
{
    private static readonly Regex Words = new(@"[\p{L}\p{N}]{3,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly string[] Recall = ["قبلی", "قبلاً", "قبلا", "پیشتر", "تناقض", "اصلاح", "گفتم", "earlier", "previous", "before", "contradict", "correct", "said", "that answer"];

    public static SelectedInterviewHistory Select(InterviewSession session, InterviewAnswer current, InterviewOptions options)
    {
        var history = session.Answers.Where(a => a.Status == AnswerStatus.Processed && a.Id != current.Id)
            .OrderBy(a => a.SubmittedAtUtc).ThenBy(a => a.ProcessedAtUtc).ThenBy(a => a.Id).ToArray();
        var questions = session.Questions.ToDictionary(q => q.QuestionId);
        InterviewDialogueTurn Map(InterviewAnswer a) => new(a.QuestionId, questions[a.QuestionId].Text, a.Transcript, a.AssistantMessage);
        if (!options.SelectiveHistoryEnabled || history.Sum(a => Size(a, questions)) <= options.HistoryCharacterBudget ||
            Recall.Any(word => (current.Transcript ?? "").Contains(word, StringComparison.OrdinalIgnoreCase)))
            return new(history.Select(Map).ToArray(), history.Length);

        var question = questions[current.QuestionId];
        var query = Tokens(question.Text + " " + question.Topic + " " + current.Transcript);
        // Current question and recent dialogue are mandatory even when the soft budget is exceeded.
        var mandatoryQuestions = history.TakeLast(2).Select(a => a.QuestionId).Append(current.QuestionId).ToHashSet();
        var chosen = history.Where(a => mandatoryQuestions.Contains(a.QuestionId)).ToHashSet();
        var used = chosen.Sum(a => Size(a, questions));
        var ranked = history.Where(a => !chosen.Contains(a)).GroupBy(a => a.QuestionId).Select(group => {
            var q = questions[group.Key]; var turns = group.ToArray();
            var overlap = Tokens(q.Text + " " + q.Topic + " " + string.Join(" ", turns.Select(a => a.Transcript))).Count(query.Contains);
            return new { Turns = turns, Rank = overlap + (q.SkillId == question.SkillId ? 10 : 0) + (q.Topic == question.Topic ? 20 : 0) };
        }).Where(x => x.Rank > 0).OrderByDescending(x => x.Rank).ThenByDescending(x => x.Turns[^1].SubmittedAtUtc);
        foreach (var item in ranked)
        {
            var size = item.Turns.Sum(a => Size(a, questions));
            if (used + size > options.HistoryCharacterBudget) continue;
            chosen.UnionWith(item.Turns); used += size;
        }
        return new(history.Where(chosen.Contains).Select(Map).ToArray(), history.Length);
    }

    private static int Size(InterviewAnswer answer, Dictionary<Guid, InterviewQuestionSnapshot> questions) =>
        questions[answer.QuestionId].Text.Length + (answer.Transcript?.Length ?? 0) + (answer.AssistantMessage?.Length ?? 0);
    private static HashSet<string> Tokens(string text) => Words.Matches(text.Replace('ي', 'ی').Replace('ك', 'ک').ToLowerInvariant())
        .Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
}
