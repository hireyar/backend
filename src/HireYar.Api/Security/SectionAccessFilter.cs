using HireYar.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace HireYar.Api.Security;

// Covers controller actions before model binding, including multipart uploads. New panel routes fail closed.
public sealed class SectionAccessFilter(ICurrentUser user) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext c, ResourceExecutionDelegate next)
    {
        var path = c.HttpContext.Request.Path.Value?.ToLowerInvariant() ?? "";
        var endpoint = c.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null ||
            path.StartsWith("/api/admin/") || path.StartsWith("/api/sales/") || path.StartsWith("/api/candidate/") ||
            !path.StartsWith("/api/") || c.HttpContext.User.Identity?.IsAuthenticated != true) { await next(); return; }
        // Account, unread-state and session actions remain available even for a disabled access role.
        if (path.StartsWith("/api/auth/") || path.StartsWith("/api/notifications")) { await next(); return; }
        var write = c.HttpContext.Request.Method is not ("GET" or "HEAD" or "OPTIONS");
        // Access codes can act as candidate credentials, so retrieving them also requires write access.
        if (path.StartsWith("/api/interviews/") && path.EndsWith("/access")) write = true;
        var section = Section(path);
        if (section is null || !user.CanAccess(section, write)) {
            c.Result = new ObjectResult(new ProblemDetails { Status = 403, Title = "شما به این بخش دسترسی ندارید", Extensions = { ["code"] = "forbidden" } }) { StatusCode = 403 }; return;
        }
        await next();
    }
    public static string? Section(string path) => path switch {
        var p when p.StartsWith("/api/panel-wizard/positions") => "jobs",
        var p when p.StartsWith("/api/panel-wizard/templates") => "templates",
        var p when p.StartsWith("/api/panel-wizard/question-suggestions") => "questions",
        var p when p.StartsWith("/api/dashboard") => "dashboard",
        var p when p.StartsWith("/api/team") => "team",
        var p when p.StartsWith("/api/organization") => "settings",
        var p when p.StartsWith("/api/subscriptions") || p.StartsWith("/api/subscription-catalog") => "subscriptions",
        var p when p.StartsWith("/api/tickets") => "tickets",
        var p when p.StartsWith("/api/interview-templates") => "templates",
        var p when p.StartsWith("/api/skills") || p.Contains("/question-bank") || p.Contains("/skills") => "questions",
        var p when p.StartsWith("/api/job-positions") && !p.Contains("/applications") => "jobs",
        var p when p.Contains("/report") || p.Contains("/evaluation-report") || p.EndsWith("/decisions") || p.EndsWith("/review") => "reports",
        var p when p.StartsWith("/api/interviews") || p.StartsWith("/api/panel-wizard") || p.Contains("/applications") => "interviews",
        _ => null
    };
}
