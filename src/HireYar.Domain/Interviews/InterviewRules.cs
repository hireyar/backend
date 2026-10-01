namespace HireYar.Domain.Interviews;

public enum InterviewStatus { Created, WaitingForAnswer, ProcessingAnswer, Completed, Cancelled }
public enum AnswerStatus { Uploaded, Transcribing, Transcribed, Evaluating, Processed, Failed }
public enum InterviewCompletionReason { QuestionLimit, DurationLimit, SufficientEvidence, NoQuestions, Manual }

public sealed class InterviewRuleException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

internal static class InterviewRules
{
    public static void Require(bool condition, string code)
    {
        if (!condition) throw new InterviewRuleException(code);
    }

    public static void Unit(decimal value) => Require(value is >= 0 and <= 1, "invalid_score");
    public static void Utc(DateTimeOffset value) => Require(value.Offset == TimeSpan.Zero, "utc_required");
    public static string Text(string value, int maximum)
    {
        Require(!string.IsNullOrWhiteSpace(value) && value.Length <= maximum, "invalid_text");
        return value.Trim();
    }
}
