namespace HireYar.Infrastructure.Interviews;

public sealed class LiaraOptions : IOpenAiInterviewOptions
{
    public string EvaluationModel { get; set; } = "openai/gpt-6-luna";
    public int EvaluationTimeoutSeconds { get; set; } = 90;
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string SttModel { get; set; } = "openai/whisper-large-v3";
    public string LlmModel { get; set; } = "openai/gpt-5.6-luna";
    public string TtsModel { get; set; } = "openai/tts-1-hd";
    public string TtsVoice { get; set; } = "nova";
    public decimal TtsSpeed { get; set; } = 1;
    public int SttTimeoutSeconds { get; set; } = 60;
    public int LlmTimeoutSeconds { get; set; } = 30;
    public int TtsTimeoutSeconds { get; set; } = 30;
}
