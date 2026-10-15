using HireYar.Application.Interviews;
namespace HireYar.Infrastructure.Interviews;

public sealed class DisabledInterviewProvider
    : ISpeechToTextService, IInterviewDecisionService, ITextToSpeechService, IQuestionSuggestionProvider, IAnswerAssessmentProvider
{
    public SpeechProfile Profile => new("disabled", "disabled", 1);
    public Task<SpeechTranscriptionResult> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken ct) =>
        Task.FromException<SpeechTranscriptionResult>(Unavailable());
    public Task<InterviewDecisionResult> EvaluateAsync(InterviewDecisionContext context, CancellationToken ct) =>
        Task.FromException<InterviewDecisionResult>(Unavailable());
    public Task<TextToSpeechResult> GenerateAsync(string text, CancellationToken ct) =>
        Task.FromException<TextToSpeechResult>(Unavailable());
    public Task<string> SuggestAsync(QuestionSuggestionContext context, CancellationToken ct) =>
        Task.FromException<string>(Unavailable());
    public Task<AnswerAssessmentResponse> AssessAnswerAsync(AnswerAssessmentInput input, CancellationToken ct) =>
        Task.FromException<AnswerAssessmentResponse>(Unavailable());
    private static InterviewProviderException Unavailable() => new("provider_not_configured");
}
