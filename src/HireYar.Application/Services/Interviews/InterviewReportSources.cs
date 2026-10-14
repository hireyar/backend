using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HireYar.Application.Interviews;

// References are local to one report request; only final-answer transcripts are eligible evidence.
public sealed class InterviewReportSources
{
    public sealed record Source(string AnswerRef, string SourceRef, Guid AnswerId, string Quote);
    private readonly Dictionary<string, Source> bySource;
    private readonly Dictionary<string, Guid> byAnswer;
    public IReadOnlyList<Source> Entries { get; }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public InterviewReportSources(InterviewReportContext context)
    {
        var sources = new List<Source>();
        byAnswer = new(StringComparer.Ordinal);
        var number = 0;
        foreach (var answer in context.Answers.Where(a => !a.IsInteraction)) {
            var answerRef = "A" + (++number).ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(answer.Transcript))
                throw new InterviewProviderException("report_invalid_response", "source_transcript_missing");
            byAnswer.Add(answerRef, answer.AnswerId);
            var part = 0;
            for (var start = 0; start < answer.Transcript.Length;) {
                var length = Math.Min(500, answer.Transcript.Length - start);
                if (start + length < answer.Transcript.Length && char.IsHighSurrogate(answer.Transcript[start + length - 1]) &&
                    char.IsLowSurrogate(answer.Transcript[start + length])) length--;
                var quote = answer.Transcript.Substring(start, length).Trim();
                if (quote.Length != 0) sources.Add(new(answerRef, answerRef + "." + (++part).ToString(CultureInfo.InvariantCulture), answer.AnswerId, quote));
                start += length;
            }
        }
        Entries = sources;
        bySource = sources.ToDictionary(s => s.SourceRef, StringComparer.Ordinal);
    }

    public string RequestJson(InterviewReportContext context) => JsonSerializer.Serialize(new {
        interview = context,
        evidenceSources = Entries.Select(s => new { s.AnswerRef, s.SourceRef, Text = s.Quote })
    }, Json);

    // Convert references to the existing public schema, with verbatim quotes supplied by the server.
    public string Resolve(string modelJson)
    {
        var reason = "source_reference_schema_invalid";
        try {
            if (modelJson.Length > 32000) throw new JsonException();
            var root = JsonNode.Parse(modelJson) as JsonObject ?? throw new JsonException();
            JsonArray Array(JsonObject parent, string key) => parent[key] as JsonArray ?? throw new JsonException();
            JsonObject Object(JsonNode? node) => node as JsonObject ?? throw new JsonException();
            Source Citation(JsonObject citation) {
                reason = "source_reference_invalid";
                if (citation.ContainsKey("answerId") || citation.ContainsKey("quote") ||
                    !citation.Remove("sourceRef", out var reference) || reference is null ||
                    !bySource.TryGetValue(reference.GetValue<string>(), out var source)) throw new JsonException();
                citation["answerId"] = source.AnswerId;
                citation["quote"] = source.Quote;
                return source;
            }
            foreach (var finding in Array(root, "findings")) Citation(Object(finding));
            foreach (var metric in Array(root, "metrics"))
                foreach (var citation in Array(Object(metric), "evidence")) Citation(Object(citation));
            foreach (var evaluationNode in Array(root, "questionEvaluations")) {
                var evaluation = Object(evaluationNode);
                reason = "answer_reference_invalid";
                if (!evaluation.Remove("answerRef", out var reference) || reference is null ||
                    !byAnswer.TryGetValue(reference.GetValue<string>(), out var answerId)) throw new JsonException();
                var source = Citation(evaluation);
                reason = "answer_source_mismatch";
                if (source.AnswerId != answerId) throw new JsonException();
            }
            return root.ToJsonString(Json);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException or NotSupportedException) {
            throw new InterviewProviderException("report_invalid_response", reason);
        }
    }
}
