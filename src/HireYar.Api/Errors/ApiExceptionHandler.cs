using System.Diagnostics;
using HireYar.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Npgsql;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Api.Errors;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var (status, code, message) = exception switch
        {
            AppException e => (e.Status, e.Code, e.Message),
            DbUpdateConcurrencyException => (409, "concurrency_conflict", "The record has changed. Reload before editing."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => (409, "conflict", "The record already exists."),
            PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } => (409, "concurrency_conflict", "Concurrent operation; retry the request."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } } => (409, "concurrency_conflict", "Concurrent operation; retry the request."),
            _ => (500, "unexpected_error", "An unexpected error occurred.")
        };
        if (status == 500)
        {
            var databaseError = exception as PostgresException ?? exception.InnerException as PostgresException;
            logger.LogError(
                "Unhandled request error. ExceptionType={ExceptionType} InnerExceptionType={InnerExceptionType} SqlState={SqlState} TraceId={TraceId} IsHttps={IsHttps} ForwardedProtoPresent={ForwardedProtoPresent} StackTrace={StackTrace}",
                exception.GetType().FullName,
                exception.InnerException?.GetType().FullName,
                databaseError?.SqlState,
                traceId,
                context.Request.IsHttps,
                context.Request.Headers.ContainsKey("X-Forwarded-Proto"),
                exception.StackTrace);
        }
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new() { HttpContext = context, ProblemDetails = new() { Status = status, Title = message, Extensions = { ["code"] = code, ["traceId"] = traceId } } });
    }
}
