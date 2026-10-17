namespace HireYar.Application.Interviews;

public sealed record ReportExpectedLevel(string State, int BelowTargetAnswers, int AtTargetAnswers, int AboveTargetAnswers,
    IReadOnlyList<ReportExpectedLevelAnswer> Answers, string Message);
public sealed record ReportExpectedLevelAnswer(Guid QuestionId, string Topic, int AskedDifficulty, int TargetDifficulty, string Relation);
public sealed record ReportNarrativeSource(int Id, string Kind, Guid? AnswerId, int Start, string Text);
public sealed record ReportNarrativeAnswer(Guid QuestionId, string Question, string Topic, int Difficulty, string AssessmentState, decimal? Score);
public sealed record ReportNarrativeInput(string PositionTitle, string? PositionDescription,
    IReadOnlyList<ReportNarrativeSource> Sources, IReadOnlyList<ReportNarrativeAnswer> Answers);
public sealed record ReportNarrativeText(string Text, IReadOnlyList<ReportNarrativeSource> Evidence);
public sealed record ReportFollowUpQuestion(string Text, string Topic, string Reason, IReadOnlyList<ReportNarrativeSource> Evidence);
public sealed record ReportExperience(string State, IReadOnlyList<ReportNarrativeSource> Statements, string Message);
public sealed record ReportNarrative(string State, ReportNarrativeText? Summary, ReportNarrativeText? NextStep,
    IReadOnlyList<ReportFollowUpQuestion> FollowUpQuestions, ReportExperience Experience, int Attempts, bool CanGenerate,
        string? FailureCode, string InputHash, DateTimeOffset? GeneratedAtUtc, string Message)
{
    public bool ShouldPoll => State == "Processing";
}
public sealed record GenerateReportNarrativeRequest(string ReportFingerprint);