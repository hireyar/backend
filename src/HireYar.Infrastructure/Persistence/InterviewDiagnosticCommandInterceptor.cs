using System.Data.Common;
using System.Text.RegularExpressions;
using HireYar.Application.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HireYar.Infrastructure.Persistence;

public sealed class InterviewDiagnosticCommandInterceptor : DbCommandInterceptor
{
    private static void Emit(DbCommand command, CommandEventData data, string status, double? duration = null)
    {
        if (!InterviewTrace.Active) return;
        // Only schema identifiers from the EF model are emitted. Never SQL, parameters, results or exception messages.
        var entities = data.Context?.Model.GetEntityTypes().ToArray() ?? [];
        var identifiers = Regex.Matches(command.CommandText, "\"([^\"]+)\"").Select(x => x.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var tables = entities.Select(x => x.GetTableName()).OfType<string>().Where(identifiers.Contains).Distinct().Order().ToArray();
        var columns = entities.Where(x => x.GetTableName() is { } name && identifiers.Contains(name))
            .SelectMany(x => x.GetProperties()).Select(x => x.GetColumnName()).Where(identifiers.Contains).Distinct().Order().ToArray();
        var operation = command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ? "read" : "write";
        InterviewTrace.Emit("database", operation, status, operation == "read" ? "خواندن داده از PostgreSQL" : "اجرای دستور تغییر داده در PostgreSQL",
            data.CommandId.ToString("N"), durationMs: duration, failureCode: status == "failed" ? "database_command_failed" : null,
            tables: tables, columns: columns);
    }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
    { Emit(command, data, "started"); return ValueTask.FromResult(result); }
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData data, DbDataReader result, CancellationToken ct = default)
    { Emit(command, data, "succeeded", data.Duration.TotalMilliseconds); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default)
    { Emit(command, data, "started"); return ValueTask.FromResult(result); }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData data, int result, CancellationToken ct = default)
    { Emit(command, data, "succeeded", data.Duration.TotalMilliseconds); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<object> result, CancellationToken ct = default)
    { Emit(command, data, "started"); return ValueTask.FromResult(result); }
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData data, object? result, CancellationToken ct = default)
    { Emit(command, data, "succeeded", data.Duration.TotalMilliseconds); return ValueTask.FromResult(result); }
    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData data, CancellationToken ct = default)
    { Emit(command, data, "failed", data.Duration.TotalMilliseconds); return Task.CompletedTask; }
    public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData data, CancellationToken ct = default)
    { Emit(command, data, "cancelled", data.Duration.TotalMilliseconds); return Task.CompletedTask; }
}
