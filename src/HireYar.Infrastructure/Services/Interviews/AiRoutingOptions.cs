using Microsoft.Extensions.Configuration;

namespace HireYar.Infrastructure.Interviews;

public sealed class AiRoutingOptions
{
    public Dictionary<string, AiConnectionOptions> Providers { get; set; } = new();
    public AiCapabilityOptions SpeechToText { get; set; } = new();
    public AiCapabilityOptions InterviewDecision { get; set; } = new();
    public AiCapabilityOptions QuestionSuggestion { get; set; } = new();
    public AiCapabilityOptions AnswerAssessment { get; set; } = new();
    public AiCapabilityOptions ReportNarrative { get; set; } = new();
    public AiCapabilityOptions TextToSpeech { get; set; } = new();

    public static readonly string[] Capabilities = [nameof(SpeechToText), nameof(InterviewDecision), nameof(QuestionSuggestion), nameof(AnswerAssessment), nameof(ReportNarrative), nameof(TextToSpeech)];
    public static bool IsConfigured(IConfiguration config) => Capabilities.Any(c => config.GetSection($"AI:{c}").Exists()) || config.GetSection("AI:Providers").Exists();

    public IEnumerable<(string Name, AiCapabilityOptions Route)> Routes => [
        (nameof(SpeechToText), SpeechToText), (nameof(InterviewDecision), InterviewDecision),
        (nameof(QuestionSuggestion), QuestionSuggestion), (nameof(AnswerAssessment), AnswerAssessment),
        (nameof(ReportNarrative), ReportNarrative), (nameof(TextToSpeech), TextToSpeech)];

    public string? ValidationError()
    {
        foreach (var (name, route) in Routes)
        {
            if (route.Provider == "Disabled") continue;
            var text = name is not (nameof(SpeechToText) or nameof(TextToSpeech));
            if (!(route.Provider is "NetArz" or "Liara" || route.Provider == "Cloudflare" && text || route.Provider == "Soniox" && name == nameof(SpeechToText)))
                return $"AI:{name}:Provider does not support this capability.";
            if (string.IsNullOrWhiteSpace(route.Model) || route.Model.Length > 100 || route.TimeoutSeconds is < 1 or > 120)
                return $"AI:{name} requires a model and timeout between 1 and 120 seconds.";
            if (!Providers.TryGetValue(route.Provider, out var connection)) return $"AI:Providers:{route.Provider} is required.";
            if (route.Provider == "Cloudflare")
            {
                if (string.IsNullOrWhiteSpace(connection.ApiToken) || connection.AccountId.Length != 32 || !connection.AccountId.All(Uri.IsHexDigit))
                    return "AI:Providers:Cloudflare requires an API token and a 32 character hexadecimal account ID.";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(connection.ApiKey)) return $"AI:Providers:{route.Provider}:ApiKey is required.";
                var endpoint = route.Provider == "Soniox" ? connection.WebSocketUrl : connection.BaseUrl;
                if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != (route.Provider == "Soniox" ? "wss" : "https") ||
                    !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                    return $"AI:Providers:{route.Provider} has an invalid secure endpoint.";
            }
            if (name == nameof(TextToSpeech) && (string.IsNullOrWhiteSpace(route.Options.Voice) || route.Options.Voice.Length > 100 || route.Options.Speed is < 0.25m or > 4))
                return "AI:TextToSpeech:Options has an invalid voice or speed.";
            if (name == nameof(SpeechToText) && (route.Options.LanguageHints.Length > 10 || route.Options.LanguageHints.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 20)))
                return "AI:SpeechToText:Options:LanguageHints is invalid.";
        }
        return null;
    }
}

public sealed class AiConnectionOptions
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string ApiToken { get; set; } = "";
    public string WebSocketUrl { get; set; } = "wss://stt-rt.soniox.com/transcribe-websocket";
}

public sealed class AiCapabilityOptions
{
    public string Provider { get; set; } = "Disabled";
    public string Model { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 60;
    public AiCapabilitySettings Options { get; set; } = new();
}

public sealed class AiCapabilitySettings
{
    public string[] LanguageHints { get; set; } = ["fa"];
    public string Voice { get; set; } = "nova";
    public decimal Speed { get; set; } = 1;
}
