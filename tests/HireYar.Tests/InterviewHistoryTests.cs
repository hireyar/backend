using System.Net;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HireYar.Tests;

public sealed class InterviewHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
    private static (InterviewSession Session, InterviewAnswer Current) History(string transcript = "SQL locking details")
    {
        var questions = Enumerable.Range(0, 8).Select(i => new InterviewQuestionSnapshot(Guid.NewGuid(), Guid.NewGuid(),
            i == 0 || i == 7 ? "SQL locking" : $"Unrelated topic {i}", i == 0 || i == 7 ? "locking" : $"topic{i}", 3, true, i + 1)).ToArray();
        var s = new InterviewSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), questions,
            questions.Select(q => new InterviewSkill(q.SkillId, q.Topic, false, 10)), 8, 1800, Now, Now.AddDays(1));
        s.SetInteractionLimit(3); s.Start(questions[0].QuestionId, Now);
        InterviewAnswer Turn(string text, int seconds)
        {
            var a = s.SubmitAnswer(s.CurrentQuestionId!.Value, "audio", "audio/webm", 16, Now.AddSeconds(seconds));
            s.BeginTranscription(a.Id); s.SetTranscript(a.Id, text, "Test", "stt"); s.BeginEvaluation(a.Id); return a;
        }
        for (var i = 0; i < 7; i++)
        {
            var a = Turn(i == 0 ? "SQL locking experience" : new string((char)('a' + i), 1500), i * 10 + 1);
            s.FinishAnswer(a.Id, [], [], "Test", "v2", Now.AddSeconds(i * 10 + 2));
            s.AskNext(questions[i + 1].QuestionId, Now.AddSeconds(i * 10 + 3));
        }
        var clarification = Turn("Please clarify lock isolation", 80);
        s.ContinueQuestion(clarification.Id, "Describe isolation", "Test", "v2", Now.AddSeconds(81));
        var current = Turn(transcript, 82);
        return (s, current);
    }

    [Fact]
    public void Selection_preserves_current_question_recent_turns_and_relevant_older_history_without_mutation()
    {
        var (s, current) = History(); var all = s.Answers.Count;
        var selected = InterviewHistorySelector.Select(s, current, new() { HistoryCharacterBudget = 3400 });
        Assert.True(selected.IsPartial); Assert.Equal(8, selected.TotalTurns);
        Assert.Contains(selected.Turns, a => a.Answer == "SQL locking experience");
        Assert.Contains(selected.Turns, a => a.QuestionId == current.QuestionId && a.AssistantReply == "Describe isolation");
        Assert.Contains(selected.Turns, a => a.QuestionId == s.Questions[6].QuestionId);
        Assert.DoesNotContain(selected.Turns, a => a.QuestionId == s.Questions[1].QuestionId);
        Assert.Equal(all, s.Answers.Count); Assert.DoesNotContain(selected.Turns, a => a.Answer == current.Transcript);
        var full = InterviewHistorySelector.Select(s, current, new() { SelectiveHistoryEnabled = false });
        var fullChars = JsonSerializer.Serialize(full.Turns).Length;
        Assert.True(JsonSerializer.Serialize(selected.Turns).Length < fullChars / 2);
    }

    [Theory]
    [InlineData("As I said earlier, the correction is...")]
    [InlineData("پاسخ قبلی را اصلاح می‌کنم")]
    public void Explicit_recall_or_correction_uses_full_persisted_history(string answer)
    {
        var (s, current) = History(answer);
        var result = InterviewHistorySelector.Select(s, current, new() { HistoryCharacterBudget = 2000 });
        Assert.False(result.IsPartial); Assert.Equal(8, result.Turns.Count);
    }

    [Fact]
    public void Small_history_and_disabled_selection_keep_the_full_context()
    {
        var (s, current) = History();
        Assert.False(InterviewHistorySelector.Select(s, current, new() { HistoryCharacterBudget = 128000 }).IsPartial);
        Assert.False(InterviewHistorySelector.Select(s, current, new() { SelectiveHistoryEnabled = false }).IsPartial);
    }

}
