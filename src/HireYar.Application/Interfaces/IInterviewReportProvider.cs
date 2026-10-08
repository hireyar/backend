namespace HireYar.Application.Interviews;

public interface IInterviewReportProvider
{
    Task<InterviewDecisionResult> GenerateReportAsync(InterviewReportContext context, CancellationToken ct);
}
