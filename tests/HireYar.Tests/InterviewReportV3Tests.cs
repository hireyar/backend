using System.Text.Json;
using HireYar.Api.Controllers;
using HireYar.Application.Interviews;

namespace HireYar.Tests;

public sealed class InterviewReportV3Tests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    [Fact]
    public void Metrics_require_catalog_coverage_final_answer_citations_and_unknowns_without_scores()
    {
        var answer = new InterviewReportAnswer(Guid.NewGuid(), Guid.NewGuid(), "Explain transactions", "I use transactions and resolve conflict through discussion", false, null);
        var context = new InterviewReportContext("Backend", "SQL transactions", null, null, "fa-IR", "Manual", [answer], [], [], [], "general");
        var metrics = InterviewReportMetricCatalog.Codes("general").Select(code => new InterviewReportMetric(code, null, null, "شواهد کافی نیست", [])).ToArray();
        metrics[0] = new("communication", 3, 0.8m, "توضیح منظم", [new(answer.AnswerId, "I use transactions")]);
        var content = new InterviewReportContent { Summary = "خلاصه", Findings = [], Limitations = ["محدودیت"], Metrics = metrics };
        InterviewReportContent Validate(InterviewReportContent value, InterviewReportContext? ctx = null) =>
            InterviewReportValidator.Validate(JsonSerializer.Serialize(value, Json), ctx ?? context, requireMetrics: true);
        Assert.Equal(7, Validate(content).Metrics!.Count);
        foreach (var invalid in new[] {
            content with { Metrics = null }, content with { Metrics = metrics.Take(6).ToArray() },
            content with { Metrics = metrics.Select(m => m with { Code = "communication" }).ToArray() },
            content with { Metrics = metrics.Select(m => m with { Score = 4, Confidence = 0.8m, Evidence = [] }).ToArray() },
            content with { Metrics = metrics.Select(m => m with { Confidence = 0.8m }).ToArray() },
            content with { Metrics = metrics.Select(m => m with { Score = 6 }).ToArray() },
            content with { Metrics = metrics.Select(m => m with { Evidence = [new(Guid.NewGuid(), "I use transactions")] }).ToArray() },
            content with { Metrics = metrics.Select(m => m with { Evidence = [new(answer.AnswerId, "invented evidence")] }).ToArray() }
        }) Assert.Throws<InterviewProviderException>(() => Validate(invalid));
        Assert.Throws<InterviewProviderException>(() => Validate(content, context with { Answers = [answer with { IsInteraction = true }] }));
        var specialized = content with { Metrics = InterviewReportMetricCatalog.Codes("specialized").Select(code =>
            new InterviewReportMetric(code, 3, 0.8m, "توضیح", [new(answer.AnswerId, "I use transactions")])).ToArray() };
        Assert.Throws<InterviewProviderException>(() => Validate(specialized, context with { InterviewType = "specialized", PositionDescription = null }));
    }

    [Fact]
    public void Printable_report_escapes_untrusted_text_and_does_not_inject_media_or_scripts()
    {
        var report = new InterviewReportDto(Guid.NewGuid(), "test", "test", "report-v3", DateTimeOffset.UtcNow,
            new() { Summary = "<script>alert('x')</script>", Findings = [], Limitations = ["<img src=x onerror=alert(1)>"] });
        var html = InterviewReportPrint.Render(report);
        Assert.DoesNotContain("<script>", html); Assert.DoesNotContain("<img", html);
        Assert.Contains("&lt;script&gt;", html); Assert.Contains("@media print", html);
    }
}
