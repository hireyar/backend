namespace HireYar.Application.Interviews;

public sealed record InterviewDiagnosticDto(string Id, DateTimeOffset AtUtc, string RequestId, string OperationId,
    string Kind, string Operation, string Status, string Message, string? Stage = null,
    string? Method = null, string? Endpoint = null, string? Provider = null, string? Model = null,
    double? DurationMs = null, int? HttpStatusCode = null, string? FailureCode = null,
    string[]? Tables = null, string[]? Columns = null, string? PromptJson = null, bool PromptTruncated = false);
public sealed record InterviewDiagnosticBatch(IReadOnlyList<InterviewDiagnosticDto> Events, bool Reset, string Cursor);

public sealed class InterviewDiagnosticOptions
{
    public bool Enabled { get; set; }
    public bool CapturePrompts { get; set; }
}
