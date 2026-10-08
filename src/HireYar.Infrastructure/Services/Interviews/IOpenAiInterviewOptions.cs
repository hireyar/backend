namespace HireYar.Infrastructure.Interviews;

// Shared wire protocol; each provider owns its configuration and concrete client.
public interface IOpenAiInterviewOptions
{
    bool Enabled { get; }
    string BaseUrl { get; }
    string ApiKey { get; }
    string SttModel { get; }
    string LlmModel { get; }
    string EvaluationModel { get; }
    int EvaluationTimeoutSeconds { get; }
    string TtsModel { get; }
    string TtsVoice { get; }
    decimal TtsSpeed { get; }
    int SttTimeoutSeconds { get; }
    int LlmTimeoutSeconds { get; }
    int TtsTimeoutSeconds { get; }
}
