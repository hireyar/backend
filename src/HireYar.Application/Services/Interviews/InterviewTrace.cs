namespace HireYar.Application.Interviews;

// The execution context flows through awaited provider/EF calls, never through HttpContext.
public static class InterviewTrace
{
    private sealed record Context(IInterviewDiagnostics Sink, Guid OrganizationId, Guid InterviewId, string RequestId, string? Stage);
    private static readonly AsyncLocal<Context?> Current = new();
    public static bool Active => Current.Value is not null;
    public static bool CapturePrompts => Current.Value?.Sink.CapturePrompts == true;
    public static IDisposable Begin(IInterviewDiagnostics sink, Guid organizationId, Guid interviewId, string requestId)
    {
        var previous = Current.Value;
        Current.Value = sink.Enabled ? new(sink, organizationId, interviewId, requestId, null) : null;
        return new Scope(() => Current.Value = previous);
    }
    public static void Stage(string stage, string message)
    {
        if (Current.Value is not { } context) return;
        Current.Value = context with { Stage = stage };
        Emit("stage", stage, "changed", message);
    }
    public static void Emit(string kind, string operation, string status, string message, string? operationId = null,
        string? method = null, string? endpoint = null, string? provider = null, string? model = null,
        double? durationMs = null, int? httpStatusCode = null, string? failureCode = null,
        string[]? tables = null, string[]? columns = null, string? promptJson = null)
    {
        if (Current.Value is not { } c) return;
        c.Sink.Publish(c.OrganizationId, c.InterviewId, new("", default, c.RequestId,
            operationId ?? Guid.NewGuid().ToString("N"), kind, operation, status, message, c.Stage,
            method, endpoint, provider, model, durationMs, httpStatusCode, failureCode, tables, columns, promptJson));
    }
    private sealed class Scope(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
