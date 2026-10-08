namespace HireYar.Application.Interviews;

public interface IInterviewDiagnostics
{
    bool Enabled { get; }
    bool CapturePrompts { get; }
    void Publish(Guid organizationId, Guid interviewId, InterviewDiagnosticDto diagnostic);
    InterviewDiagnosticBatch Read(Guid organizationId, Guid interviewId, string? cursor, bool includePrompts);
    IDisposable Subscribe(Guid organizationId, Guid interviewId);
}
