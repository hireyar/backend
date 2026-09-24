using HireYar.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Api.Errors;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, code, message) = exception switch
        {
            AppException e => (e.Status, e.Code, e.Message),
            DbUpdateConcurrencyException => (409, "concurrency_conflict", "The record has changed. Reload before editing."),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (409, "conflict", "The record already exists."),
            SqlException { Number: 1205 } => (409, "concurrency_conflict", "Concurrent operation; retry the request."),
            _ => (500, "unexpected_error", "An unexpected error occurred.")
        };
        if (status == 500) logger.LogError("Unhandled request error. ExceptionType={ExceptionType} TraceId={TraceId}", exception.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new() { HttpContext = context, ProblemDetails = new() { Status = status, Title = message, Extensions = { ["code"] = code } } });
    }
}
