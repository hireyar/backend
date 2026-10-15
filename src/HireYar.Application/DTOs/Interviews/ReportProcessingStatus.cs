namespace HireYar.Application.Interviews;

public sealed record ReportProcessingStatus(string State, bool ShouldPoll, int? PollAfterSeconds,
    int PendingAssessments, int CompletedAssessments, int FailedAssessments, int UnavailableAssessments,
    bool HasUnimplementedSections, string? ReasonCode, string Message)
{
    public static ReportProcessingStatus From(IReadOnlyList<ReportAnswerAssessment> assessments,
        IReadOnlyList<ReportSection> sections, AnswerAssessmentOptions options)
    {
        var pending = assessments.Count(a => a.IsPending);
        var shouldPoll = pending > 0 && options.WorkerEnabled;
        var paused = pending > 0 && !options.WorkerEnabled;
        return new(paused ? "Paused" : shouldPoll ? "Pending" : "Idle", shouldPoll,
            shouldPoll ? Math.Max(3, options.PollSeconds) : null, pending,
            assessments.Count(a => a.State is "Succeeded" or "InsufficientEvidence"),
            assessments.Count(a => a.State == "Failed"), assessments.Count(a => a.State == "Unavailable"),
            sections.Any(s => s.State == "NotImplemented"), paused ? "assessment_worker_disabled" : null,
            paused ? "ارزیابی خودکار پاسخ‌ها فعلاً متوقف است." : shouldPoll ? "ارزیابی بعضی پاسخ‌ها هنوز پایان نیافته است." : "ارزیابی در انتظار پردازش وجود ندارد؛ وضعیت هر بخش را جداگانه ببینید.");
    }
}
