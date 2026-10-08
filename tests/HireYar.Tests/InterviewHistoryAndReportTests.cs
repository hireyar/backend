using System.Net;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HireYar.Tests;

public sealed class InterviewHistoryAndReportTests
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

    private static InterviewReportContext ReportContext() => new("Backend", "SQL role", "Resume", "Introduction", "fa-IR", "Manual",
        [new(Guid.NewGuid(), Guid.NewGuid(), "Explain SQL", "I used transactions with explicit locking", false, null)], [], [], []);
    private static string Report(InterviewReportContext context, Guid? answerId = null, string quote = "explicit locking", string kind = "strength") =>
        JsonSerializer.Serialize(new InterviewReportContent { Summary = "Observed SQL experience, subject to evidence limitations.",
            Findings = [new() { AnswerId = answerId ?? context.Answers[0].AnswerId, Quote = quote, Observation = "Explained locking", Kind = kind }],
            Limitations = ["One answer only; human review required."] }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public void Reports_require_real_answer_ids_exact_quotes_and_final_answer_evidence()
    {
        var context = ReportContext();
        Assert.Single(InterviewReportValidator.Validate(Report(context), context).Findings);
        foreach (var invalid in new[] { Report(context, Guid.NewGuid()), Report(context, quote: "invented experience"),
            Report(context, kind: "hire"), "{}", "not-json", Report(context).Replace("\"summary\":", "\"extra\":1,\"summary\":") })
            Assert.Equal("report_invalid_response", Assert.Throws<InterviewProviderException>(() => InterviewReportValidator.Validate(invalid, context)).Code);
        var interaction = context with { Answers = [context.Answers[0] with { IsInteraction = true }] };
        Assert.Throws<InterviewProviderException>(() => InterviewReportValidator.Validate(Report(context), interaction));
    }

    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Models { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Models.Add(json.RootElement.GetProperty("model").GetString()!);
            var system = json.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!;
            if (Models.Count == 1) Assert.Contains("completed interview", system);
            else Assert.Contains("interview decision engine", system);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}") };
        }
    }

    [Fact]
    public async Task Report_model_and_prompt_are_independent_of_live_model_for_both_providers()
    {
        foreach (var liara in new[] { true, false })
        {
            var handler = new Handler(); var http = new HttpClient(handler);
            OpenAiCompatibleInterviewClient client = liara
                ? new LiaraClient(http, Options.Create(new LiaraOptions { BaseUrl = "https://test.example/v1", ApiKey = "synthetic", LlmModel = "live-test", EvaluationModel = "openai/gpt-6-luna" }), NullLogger<LiaraClient>.Instance)
                : new NetArzClient(http, Options.Create(new NetArzOptions { ApiKey = "synthetic", LlmModel = "live-test", EvaluationModel = "openai/gpt-6-luna" }), NullLogger<NetArzClient>.Instance);
            var report = await client.GenerateReportAsync(ReportContext(), default);
            var (s, answer) = History(); var q = s.Questions[^1];
            await client.EvaluateAsync(new("Backend", q.QuestionId, "ProcessingAnswer", 7, 8, 100, q, answer.Transcript!, s.Skills, s.SkillStates, s.CoveredTopics, []), default);
            Assert.Equal(["openai/gpt-6-luna", "live-test"], handler.Models); Assert.Equal("openai/gpt-6-luna", report.Model);
        }
    }
}
