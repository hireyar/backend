using System.Text.Json;

namespace HireYar.Application.Interviews;

public static class InterviewReportValidator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static InterviewReportContent Validate(string json, InterviewReportContext context)
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
