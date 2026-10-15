namespace HireYar.Application.Interviews;

public sealed record SessionReportDocument(string SchemaVersion, Guid InterviewId, string State,
    ReportIdentity Identity, ReportStatistics Statistics, ReportAccess Access,
    IReadOnlyList<ReportQuestion> Questions, ReportMedia Media, ReportSignals Signals,
    ReportScore OverallScore, ReportText Summary, ReportNotes Strengths, ReportNotes Improvements,
    IReadOnlyList<ReportSection> Sections);
public sealed record ReportIdentity(Guid CandidateId, string CandidateName, string PositionTitle,
    string InterviewType, string InterviewStatus, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc, int? ElapsedSeconds, string? CompletionReason);
public sealed record ReportStatistics(int AskedQuestions, int BankQuestions, int SubmittedAnswers, int ProcessedAnswers,
    int UnansweredQuestions, int FailedAnswers, int ClarificationTurns, decimal? AverageAnswerSeconds,
    IReadOnlyList<int> AskedDifficultyLevels);
public sealed record ReportAccess(bool AssessmentIncluded, string MediaState, string PrintUrl);
public sealed record ReportSection(string Code, string Title, string State, string Message);
public sealed record ReportScore(string State, decimal? Value, decimal? Confidence, string? Formula);
public sealed record ReportText(string State, string? Text);
public sealed record ReportNotes(string State, IReadOnlyList<string> Items);
public sealed record ReportQuestion(Guid QuestionId, int Number, string Text, string Topic, int Difficulty,
    DateTimeOffset? AskedAtUtc, string AnswerState, ReportTurn? Answer, IReadOnlyList<ReportTurn> Clarifications,
    ReportAnswerAssessment Assessment);
public sealed record ReportTurn(Guid AnswerId, string State, string? Transcript, string? AssistantMessage,
    DateTimeOffset SubmittedAtUtc, int? DurationSeconds, string? AudioUrl, string? VideoUrl);
public sealed record ReportMedia(string State, ReportRecording? Recording, IReadOnlyList<ReportRecording> Segments);
public sealed record ReportRecording(string? VideoUrl, int DurationSeconds, int? SegmentNumber,
    string TimelineState, IReadOnlyList<ReportOffset> Timeline);
public sealed record ReportOffset(Guid AnswerId, decimal StartSeconds, decimal EndSeconds);
public sealed record ReportSignals(string ConsentState, string DeviceState, IReadOnlyList<ReportSignal> Events);
public sealed record ReportSignal(string Kind, DateTimeOffset? OccurredAtUtc, DateTimeOffset RecordedAtUtc);
