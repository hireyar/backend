namespace HireYar.Application.Interviews;

public sealed record AnswerExcerpt(int Id, int Start, string Text);
public sealed record AnswerAssessmentInput(string PositionTitle, string InterviewType, string Question,
    string Topic, int Difficulty, IReadOnlyList<AnswerExcerpt> Excerpts, string? PositionDescription = null, IReadOnlyList<AnswerDimensionDefinition>? Dimensions = null);
public sealed record AnswerAssessmentResponse(string Json, string Provider, string Model);
public interface IAnswerAssessmentProvider
{
    Task<AnswerAssessmentResponse> AssessAnswerAsync(AnswerAssessmentInput input, CancellationToken ct);
}
public sealed class AnswerAssessmentOptions
{
    public bool WorkerEnabled { get; set; } = true;
    public int PollSeconds { get; set; } = 2;
    public int AttemptTimeoutSeconds { get; set; } = 75;
    public const int MaxAttempts = 3;
    public const string Version = "answer-quality/2";
}
