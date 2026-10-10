using System.Text.Json;

namespace HireYar.Application.Interviews;

public static class InterviewReportValidator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static InterviewReportContent Validate(string json, InterviewReportContext context, bool requireEvaluations = false, bool requireMetrics = false)
    {
        try
        {
            if (json.Length > 32000) throw new JsonException();
            var result = JsonSerializer.Deserialize<InterviewReportContent>(json, Json);
            bool Text(string? text, int max) => !string.IsNullOrWhiteSpace(text) && text.Length <= max;
            if (result is null || !Text(result.Summary, 4000) || result.Findings is null || result.Findings.Count > 30 ||
                result.Limitations is null || result.Limitations.Count is < 1 or > 10 || result.Limitations.Any(x => !Text(x, 1000)))
                throw new JsonException();
            var answers = context.Answers.Where(a => !a.IsInteraction).ToDictionary(a => a.AnswerId);
            if (result.Details is not null) throw new JsonException(); // Calculated only by the server.
            if (requireMetrics || result.Metrics is not null)
            {
                var codes = InterviewReportMetricCatalog.Codes(context.InterviewType);
                if (result.Metrics is null || result.Metrics.Count != codes.Count ||
                    result.Metrics.Any(m => m is null) || result.Metrics.Select(m => m.Code).Distinct().Count() != codes.Count)
                    throw new JsonException();
                foreach (var metric in result.Metrics)
                {
                    if (!codes.Contains(metric.Code) || !Text(metric.Explanation, 1000) || metric.Evidence is null || metric.Evidence.Count > 5)
                        throw new JsonException();
                    if (metric.Score is null)
                    {
                        if (metric.Confidence is not null || metric.Evidence.Count != 0) throw new JsonException();
                    }
                    else if (metric.Score is < 0 or > 5 || metric.Confidence is null or < 0 or > 1 || metric.Evidence.Count == 0 ||
                        metric.Code == "job_alignment" && string.IsNullOrWhiteSpace(context.PositionDescription)) throw new JsonException();
                    foreach (var citation in metric.Evidence)
                        if (citation is null || !answers.TryGetValue(citation.AnswerId, out var answer) || !Text(citation.Quote, 500) ||
                            answer.Transcript is null || !answer.Transcript.Contains(citation.Quote, StringComparison.Ordinal)) throw new JsonException();
                }
            }
            if (requireEvaluations || result.QuestionEvaluations is not null)
            {
                var evaluations = result.QuestionEvaluations;
                if (evaluations is null || evaluations.Count != answers.Count ||
                    evaluations.Any(e => e is null) || evaluations.Select(e => e.AnswerId).Distinct().Count() != evaluations.Count ||
                    result.NextRoundFocus is null || result.NextRoundFocus.Count > 5 || result.NextRoundFocus.Any(s => !Text(s, 1000))) throw new JsonException();
                foreach (var e in evaluations)
                    if (!answers.TryGetValue(e.AnswerId, out var answer) || e.Score is < 0 or > 5 || e.Confidence is < 0 or > 1 ||
                        !Text(e.Feedback, 2000) || !Text(e.Quote, 2000) || answer.Transcript is null || !answer.Transcript.Contains(e.Quote, StringComparison.Ordinal) ||
                        e.Highlights is null || e.Gaps is null || e.Highlights.Count > 5 || e.Gaps.Count > 4 ||
                        e.Highlights.Concat(e.Gaps).Any(s => !Text(s, 500))) throw new JsonException();
            }
            foreach (var finding in result.Findings)
                if (finding is null || !Text(finding.Quote, 2000) || !Text(finding.Observation, 2000) || finding.Kind is not ("strength" or "gap") ||
                    !answers.TryGetValue(finding.AnswerId, out var answer) || answer.Transcript is null ||
                    !answer.Transcript.Contains(finding.Quote, StringComparison.Ordinal)) throw new JsonException();
            return result;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or ArgumentException)
        { throw new InterviewProviderException("report_invalid_response"); }
    }
}
