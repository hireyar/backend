using System.Diagnostics;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace HireYar.Api.Security;

public sealed class InterviewDiagnosticMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IInterviewDiagnostics diagnostics, ICurrentUser user)
    {
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (!diagnostics.Enabled || context.Request.Path.Value?.EndsWith("/diagnostics", StringComparison.Ordinal) == true ||
            action is null || action.ActionName == "AccessStatus") { await next(context); return; }
        Guid organizationId, interviewId;
        if (action.ControllerName == "CandidateInterviews")
        {
            if (!Guid.TryParse(context.User.FindFirst("interview_organization")?.Value, out organizationId) ||
                !Guid.TryParse(context.User.FindFirst("interview")?.Value, out interviewId)) { await next(context); return; }
        }
        else if (action.ControllerName == "Interviews" && context.User.IsInRole("Owner") &&
            Guid.TryParse(context.Request.RouteValues["id"]?.ToString(), out interviewId)) organizationId = user.OrganizationId;
        else { await next(context); return; }
        using var scope = InterviewTrace.Begin(diagnostics, organizationId, interviewId, context.TraceIdentifier);
        var operationId = Guid.NewGuid().ToString("N");
        // Route templates only: no query string, invitation code, headers or request body.
        var endpoint = (context.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText
            ?? action.AttributeRouteInfo?.Template;
        var watch = Stopwatch.StartNew();
        InterviewTrace.Emit("api", action.ActionName, "started", "شروع اجرای API", operationId, context.Request.Method, endpoint);
        try
        {
            await next(context);
            InterviewTrace.Emit("api", action.ActionName, context.Response.StatusCode < 400 ? "succeeded" : "failed", "پایان اجرای API",
                operationId, context.Request.Method, endpoint, durationMs: watch.Elapsed.TotalMilliseconds, httpStatusCode: context.Response.StatusCode);
        }
        catch (Exception e)
        {
            InterviewTrace.Emit("api", action.ActionName, e is OperationCanceledException ? "cancelled" : "failed", "اجرای API متوقف شد",
                operationId, context.Request.Method, endpoint, durationMs: watch.Elapsed.TotalMilliseconds,
                httpStatusCode: e is AppException app ? app.Status : 500,
                failureCode: e is AppException failure ? failure.Code : "request_failed");
            throw;
        }
    }
}
