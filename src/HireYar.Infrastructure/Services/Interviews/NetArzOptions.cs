namespace HireYar.Infrastructure.Interviews;

public sealed class NetArzOptions : IOpenAiInterviewOptions
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://netarz.ir/api/ai/v1";
    public string ApiKey { get; set; } = "";
    public string SttModel { get; set; } = "gpt-transcribe";
    public string LlmModel { get; set; } = "gpt-5.6-luna";
    public string TtsModel { get; set; } = "tts-1-hd";
    public string TtsVoice { get; set; } = "nova";
    public decimal TtsSpeed { get; set; } = 1;
    public int SttTimeoutSeconds { get; set; } = 60;
    public int LlmTimeoutSeconds { get; set; } = 30;
    public int TtsTimeoutSeconds { get; set; } = 30;

}
public sealed class InterviewStorageOptions
{
    public string VideoProvider { get; set; } = "Local";
    public InterviewS3Options S3 { get; set; } = new();
    public string RootPath { get; set; } = "storage/interviews";
}
