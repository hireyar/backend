namespace HireYar.Application.Interviews;
public sealed record QuestionSuggestionContext(string PositionTitle, string PositionDescription, int? TopicCount, string? TopicName);
public interface IQuestionSuggestionProvider
{
    Task<string> SuggestAsync(QuestionSuggestionContext context, CancellationToken ct);
}
