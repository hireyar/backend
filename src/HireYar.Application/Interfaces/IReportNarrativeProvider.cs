namespace HireYar.Application.Interviews;

public interface IReportNarrativeProvider
{
    Task<AnswerAssessmentResponse> GenerateNarrativeAsync(ReportNarrativeInput input, CancellationToken ct);
}