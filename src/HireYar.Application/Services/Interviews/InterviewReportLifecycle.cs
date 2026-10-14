using System.Linq.Expressions;
using HireYar.Domain.Interviews;

namespace HireYar.Application.Interviews;

public static class InterviewReportLifecycle
{
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(130);
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(150);

    // Use the same rule for status, retries and background recovery, including malformed legacy work.
    public static Expression<Func<InterviewEvaluationReport, bool>> StaleAt(DateTimeOffset now)
    {
        var oldestStart = now - LeaseDuration;
        return row => (row.GenerationStage == "Generating" || row.GenerationStage == "Validating" || row.GenerationStage == "Saving") &&
            (row.LeaseId == null || row.LeaseExpiresAtUtc == null || row.LeaseExpiresAtUtc <= now || row.StartedAtUtc <= oldestStart);
    }

    public static bool IsStale(InterviewEvaluationReport row, DateTimeOffset now) => StaleAt(now).Compile(preferInterpretation: true)(row);
}
