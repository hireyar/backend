using System.Text.Json;
using HireYar.Application.Interviews;

namespace HireYar.Tests;

public sealed class FakeInterviewReportProvider : IInterviewReportProvider
{
    public int Calls;
    public bool Fail;
    public bool Invalid;
    public bool IgnoreCancellation;
    public InterviewReportContext? LastContext;
    public TaskCompletionSource? Entered;
    public TaskCompletionSource? Release;
    public void Reset() { Calls = 0; Fail = Invalid = IgnoreCancellation = false; LastContext = null; Entered = Release = null; }
    public async Task<InterviewDecisionResult> GenerateReportAsync(InterviewReportContext context, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls); LastContext = context;
        Entered?.TrySetResult(); if (Release is { } release) await release.Task.WaitAsync(IgnoreCancellation ? CancellationToken.None : ct);
        if (Fail) throw new InterviewProviderException("report_timeout");
        if (Invalid) return new("not-json", "Test", "openai/gpt-6-luna");
        var answer = context.Answers.FirstOrDefault(a => !a.IsInteraction && a.Transcript is not null);
        var content = new InterviewReportContent { Summary = "Synthetic job-relevant evaluation for human review.",
            Findings = answer is null ? [] : [new() { AnswerId = answer.AnswerId, Quote = answer.Transcript![..Math.Min(100, answer.Transcript!.Length)], Observation = "Observed answer", Kind = "strength" }],
            Limitations = ["Synthetic limited evidence; final decision requires human review."],
            QuestionEvaluations = context.Answers.Where(a => !a.IsInteraction).Select(a => new InterviewQuestionEvaluation(a.AnswerId,
                3, 0.8m, "ارزیابی آزمایشی پاسخ", a.Transcript![..Math.Min(100, a.Transcript!.Length)], ["شاهد آزمایشی"], [])).ToArray(),
            NextRoundFocus = ["بررسی بیشتر شواهد"],
            Metrics = InterviewReportMetricCatalog.Codes(context.InterviewType).Select(code => new InterviewReportMetric(code, null, null,
                "شواهد کافی نیست", [])).ToArray() };
        return new(JsonSerializer.Serialize(content, new JsonSerializerOptions(JsonSerializerDefaults.Web)), "Test", "openai/gpt-6-luna");
    }
}
