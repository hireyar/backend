using System.ComponentModel.DataAnnotations;
namespace HireYar.Application.Interviews;

public sealed record TopicQuestionRequest(
    [Required, RegularExpression("^(easy|medium|hard)$")] string Level,
    [Required, StringLength(2000)] string Text);
public sealed record QuestionTopicRequest([Required, StringLength(200)] string Name,
    [Required, MinLength(3), MaxLength(3)] List<TopicQuestionRequest> Questions);
public sealed record QuestionSuggestionRequest(Guid JobPositionId, [Range(5, 10)] int? TopicCount = 7);
public sealed record TopicSuggestionRequest(Guid JobPositionId, [Required, StringLength(200)] string TopicName);
public sealed record QuestionSuggestionsDto(Guid JobPositionId, IReadOnlyList<QuestionTopicRequest> Topics,
    int TopicCount, int BankQuestionCount, int InterviewQuestionCount);
public static class QuestionTopicRules
{
    public static int Difficulty(string level) => level switch { "easy" => 1, "medium" => 3, "hard" => 5, _ => throw new ArgumentException("Invalid level") };
    public static string Level(int difficulty) => difficulty switch { 1 => "easy", 3 => "medium", 5 => "hard", _ => throw new ArgumentException("Invalid difficulty") };
    public static bool IsValid(IReadOnlyList<QuestionTopicRequest> topics, bool single = false) =>
        topics.Count >= (single ? 1 : 5) && topics.Count <= (single ? 1 : 10) &&
        topics.All(t => t is not null && !string.IsNullOrWhiteSpace(t.Name) && t.Name.Length <= 200 && t.Questions is { Count: 3 } &&
            t.Questions.All(q => q is not null && q.Level is "easy" or "medium" or "hard" && !string.IsNullOrWhiteSpace(q.Text) && q.Text.Length <= 2000) &&
            t.Questions.Select(q => q.Level).Distinct().Count() == 3) &&
        topics.Select(t => t.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == topics.Count;
}
