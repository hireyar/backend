using System.Security.Cryptography;
using System.Text;
using HireYar.Api.Controllers;
using HireYar.Application.Interviews;

namespace HireYar.Tests;

public sealed class SessionReportTemplateTests
{
    private static SessionReportDocument Example(bool general, bool scored)
    {
        var now = new DateTimeOffset(2026, 10, 17, 8, 30, 0, TimeSpan.Zero);
        var id = Guid.NewGuid();
        var evidence = new[] { new AnswerAssessmentEvidence(id, 0, 4, "test") };
        var assessment = new ReportAnswerAssessment(scored ? "Succeeded" : "Failed", scored ? 80 : null, scored ? 0.8m : null,
            "Synthetic <script>alert(1)</script>", "test", evidence, 1, false, null, now, now, now, null, "answer-quality/2", "Synthetic", "synthetic");
        var question = new ReportQuestion(Guid.NewGuid(), 1, "Question <img src=x onerror=alert(1)>", "Synthetic topic", 3, now, "Processed",
            new(id, "Processed", "test <script>alert(1)</script>", null, now, 20, null, null), [], assessment);
        var distribution = new ReportDistribution(scored ? 1 : 0, 0, 0, scored ? 0 : 1, 60, 80);
        var metrics = new[] { new ReportMetric("communication", "ارتباطات و بیان", scored ? "Available" : "InsufficientEvidence", scored ? 75 : null, scored ? 0.7m : null, scored ? 1 : 0, 1, [question.QuestionId], evidence, "Synthetic") };
        var axes = Enumerable.Range(1, 3).Select(i => new ReportProfileAxis(Guid.NewGuid(), "Topic " + i, "Partial", scored && i < 3 ? 80 : null, null, 1, 0, [3], [], [])).ToArray();
        var profile = new ReportProfile("Partial", general ? "general" : "specialized", axes, metrics, distribution, "Synthetic");
        var score = new ReportScore(scored ? "Available" : "InsufficientEvidence", scored ? 80 : null, scored ? 0.8m : null, "hidden_formula");
        return new("session-report/1", Guid.NewGuid(), "BaseReady",
            new(Guid.NewGuid(), "Synthetic <script>candidate</script>", "Synthetic position", general ? "general" : "specialized", "Completed", now, now, now.AddMinutes(5), 300, "Manual"),
            new(1, 3, 1, 1, 0, 0, 0, 20, [3]), new(true, "WithinRetention", "/synthetic/print"), [question],
            new("Missing", null, []), new("Unknown", "Unknown", []), score, new("Available", "Synthetic summary"), new("NoFindings", []), new("NoFindings", []), [],
            ReportProcessingStatus.From([assessment], [], new()), new(1, scored ? 1 : 0, 0, scored ? 0 : 1, 0, scored ? 1 : 0, !scored),
            new("Available", 99, 0.9m, "hidden_fit_formula", "AtConfiguredThreshold", 1, [], "hidden_fit"), profile, profile,
            new("Available", Guid.NewGuid(), 60, 80, 0.6m, [], "hidden_weights", false, null),
            new("AwaitingReview", true, "/synthetic/review", "synthetic", null, [], "تصمیم نهایی با کارشناس است."), [], "synthetic");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Template_uses_actual_type_and_values_and_keeps_unknowns_and_untrusted_text_safe(bool general, bool scored)
    {
        var html = SessionReportPage.Render(Example(general, scored));
        Assert.Contains(general ? "id='rep-gen'" : "id='rep-spec'", html);
        Assert.DoesNotContain(general ? "id='rep-spec'" : "id='rep-gen'", html);
        foreach (var section in new[] { "navigation", "identity", "score", "statistics", "summary", "strengthsAndImprovements", general ? "generalProfile" : "specializedProfile", "questions", "signals", "humanReview" })
            Assert.Contains($"data-section='{section}'", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("hidden_", html);
        Assert.DoesNotContain("file:///", html);
        Assert.DoesNotContain("fill='rgba(16,185,129,.28)'", html); // Missing radar axis must not be plotted as zero.
        Assert.Contains("<details class='q'", html);
        Assert.Contains("@media print", html);
        Assert.Contains("تصمیم انسانی ثبت نشده است", html);
        Assert.Contains("نامعلوم", html);
        if (scored) { Assert.Contains("۸۰٪", html); Assert.Contains("۴ / ۵", html); }
        else Assert.DoesNotContain("conic-gradient(#38bdf8 0 0%", html);
    }

    [Fact]
    public void CSP_only_allows_the_fixed_print_handler()
    {
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(SessionReportPage.PrintScript)));
        Assert.Contains($"script-src 'sha256-{hash}'", SessionReportPage.ContentSecurityPolicy);
        Assert.Contains("default-src 'none'", SessionReportPage.ContentSecurityPolicy);
        Assert.Contains("form-action 'none'", SessionReportPage.ContentSecurityPolicy);
        Assert.DoesNotContain("script-src 'unsafe-inline'", SessionReportPage.ContentSecurityPolicy);
    }
}
