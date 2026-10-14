using System.Text.Json;
using HireYar.Application.Interviews;

namespace HireYar.Tests;

public sealed class InterviewReportSafetyRulesTests
{
    [Fact]
    public void Validation_diagnostics_distinguish_catalog_and_citations_without_exposing_content()
    {
        var answer = new InterviewReportAnswer(Guid.NewGuid(), Guid.NewGuid(), "Question", "Synthetic confidential transcript", false, null);
        var context = new InterviewReportContext("Role", "JD", null, null, "fa-IR", "Manual", [answer], [], [], []);
        var content = new InterviewReportContent {
            Summary = "Summary", Findings = [], Limitations = ["Limited evidence"],
            Metrics = InterviewReportMetricCatalog.Codes(context.InterviewType).Select(code =>
                new InterviewReportMetric(code, null, null, "No evidence", [])).ToArray()
        };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        InterviewProviderException Reject(InterviewReportContent bad) => Assert.Throws<InterviewProviderException>(() =>
            InterviewReportValidator.Validate(JsonSerializer.Serialize(bad, options), context, requireMetrics: true));
        var catalog = Reject(content with { Metrics = [] });
        Assert.Equal("report_invalid_response", catalog.Code); Assert.Equal("metric_catalog_invalid", catalog.Reason);
        var citation = Reject(content with { Metrics = content.Metrics!.Select(m => m with {
            Score = 3, Confidence = 0.8m, Evidence = [new(answer.AnswerId, "Invented quote")]
        }).ToArray() });
        Assert.Equal("metric_citation_invalid", citation.Reason);
        Assert.DoesNotContain(answer.Transcript!, citation.ToString()); Assert.DoesNotContain("Invented quote", citation.ToString());
        Assert.Equal("json_schema_invalid", Assert.Throws<InterviewProviderException>(() =>
            InterviewReportValidator.Validate("not-json", context)).Reason);
    }

    [Theory]
    [InlineData("general", 7)]
    [InlineData("specialized", 3)]
    public void Report_prompt_specifies_actual_required_catalog_and_final_answer_count(string type, int count)
    {
        var context = new InterviewReportContext("Role", "JD", null, null, "fa-IR", "Manual", [
            new(Guid.NewGuid(), Guid.NewGuid(), "Question", "Final answer", false, null),
            new(Guid.NewGuid(), Guid.NewGuid(), "Question", "Clarification", true, null)
        ], [], [], [], type);
        var prompt = InterviewReportPrompt.For(context);
        Assert.Equal(count, InterviewReportMetricCatalog.Codes(type).Count);
        Assert.Contains("Required metric codes for this interview (include every code exactly once, even when unassessed): " +
            string.Join(", ", InterviewReportMetricCatalog.Codes(type)), prompt);
        Assert.Contains("Required questionEvaluations count: 1.", prompt);
        Assert.Contains("do not correct spelling, punctuation or whitespace", prompt);
    }
}
