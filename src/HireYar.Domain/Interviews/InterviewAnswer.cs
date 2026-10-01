namespace HireYar.Domain.Interviews;

public sealed class InterviewAnswer
{
    private InterviewAnswer() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid InterviewSessionId { get; private set; }
    public Guid QuestionId { get; private set; }
    public string AudioStorageKey { get; private set; } = "";
    public string AudioContentType { get; private set; } = "";
    public long AudioSizeBytes { get; private set; }
    public DateTimeOffset SubmittedAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public string? Transcript { get; private set; }
    public string? TranscriptProvider { get; private set; }
    public string? TranscriptModel { get; private set; }
    public string? DecisionModel { get; private set; }
    public string? PromptVersion { get; private set; }
    public AnswerStatus Status { get; private set; } = AnswerStatus.Uploaded;
    public string? FailureStage { get; private set; }
    public string? FailureCode { get; private set; }
    public string? SelectionSource { get; private set; }
    public void SetDiagnostics(string? failureStage, string? failureCode, string? selectionSource)
    {
        FailureStage = failureStage is null ? null : InterviewRules.Text(failureStage, 40);
        FailureCode = failureCode is null ? null : InterviewRules.Text(failureCode, 100);
        SelectionSource = selectionSource is null ? null : InterviewRules.Text(selectionSource, 20);
    }
    internal void Resume()
    {
        InterviewRules.Require(Status != AnswerStatus.Processed, "answer_already_processed");
        Status = Transcript is null ? AnswerStatus.Uploaded : AnswerStatus.Transcribed;
    }

    internal InterviewAnswer(Guid sessionId, Guid questionId, string storageKey, string contentType,
        long size, DateTimeOffset submittedAt)
    {
        InterviewRules.Require(size > 0, "empty_audio");
        AudioStorageKey = InterviewRules.Text(storageKey, 512);
        AudioContentType = InterviewRules.Text(contentType, 100);
        InterviewSessionId = sessionId; QuestionId = questionId;
        AudioSizeBytes = size; SubmittedAtUtc = submittedAt;
    }

    internal void BeginTranscription()
    {
        InterviewRules.Require(Status == AnswerStatus.Uploaded, "invalid_answer_state");
        Status = AnswerStatus.Transcribing;
    }

    internal void SetTranscript(string transcript, string provider, string model)
    {
        InterviewRules.Require(Status == AnswerStatus.Transcribing, "invalid_answer_state");
        var text = InterviewRules.Text(transcript, 32000);
        var providerName = InterviewRules.Text(provider, 100);
        var modelName = InterviewRules.Text(model, 100);
        Transcript = text; TranscriptProvider = providerName; TranscriptModel = modelName;
        Status = AnswerStatus.Transcribed;
    }

    internal void BeginEvaluation()
    {
        InterviewRules.Require(Status == AnswerStatus.Transcribed, "invalid_answer_state");
        Status = AnswerStatus.Evaluating;
    }

    internal void Finish(string model, string promptVersion, DateTimeOffset now)
    {
        DecisionModel = model; PromptVersion = promptVersion; ProcessedAtUtc = now;
        Status = AnswerStatus.Processed;
    }

    internal void Fail()
    {
        InterviewRules.Require(Status is not (AnswerStatus.Processed or AnswerStatus.Failed), "invalid_answer_state");
        Status = AnswerStatus.Failed;
    }

    internal void Retry()
    {
        InterviewRules.Require(Status == AnswerStatus.Failed, "invalid_answer_state");
        Status = Transcript is null ? AnswerStatus.Uploaded : AnswerStatus.Transcribed;
    }
}
