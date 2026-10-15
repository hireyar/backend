using HireYar.Application.Common;
using HireYar.Application.Interviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Api.Controllers;

[ApiController, Authorize(Roles = "Owner,Recruiter"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SessionReportsController(SessionReportService reports, IApplicationDbContext db, ICurrentUser user, AnswerAssessmentService assessments) : ControllerBase
{
    [HttpGet("api/interviews/{id:guid}/report")]
    public Task<SessionReportDocument> Read(Guid id, CancellationToken ct) => reports.ReadAsync(id, ct);

    [HttpGet("api/interviews/{id:guid}/report/print")]
    public async Task<IActionResult> Print(Guid id, CancellationToken ct)
    {
        var report = await reports.ReadAsync(id, ct);
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; frame-ancestors 'none'; base-uri 'none'";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Content(SessionReportPage.Render(report), "text/html; charset=utf-8");
    }
    [Authorize(Roles = "Owner")]
    [HttpPost("api/interviews/{id:guid}/report/answers/{answerId:guid}/assessment/retry")]
    public async Task<IActionResult> RetryAssessment(Guid id, Guid answerId, CancellationToken ct)
    {
        await reports.ReadAsync(id, ct); // Same tenant, soft-delete and purchased capability checks as reading.
        await assessments.RetryAsync(user.OrganizationId, id, answerId, ct);
        var report = await reports.ReadAsync(id, ct);
        return Accepted($"/api/interviews/{id}/report", new { interviewId = id, answerId, assessment = report.Questions.Single(q => q.Answer?.AnswerId == answerId).Assessment });
    }
    [HttpGet("api/interviews/{id:guid}/evaluation-report")]
    [HttpGet("api/interviews/{id:guid}/evaluation-report/status")]
    [HttpGet("api/interviews/{id:guid}/evaluation-report/print")]
    public async Task<IActionResult> Retired(Guid id, CancellationToken ct)
    {
        if (!await db.InterviewSessions.IgnoreAutoIncludes().AsNoTracking().AnyAsync(s => s.Id == id && s.OrganizationId == user.OrganizationId && s.DeletedAtUtc == null, ct))
            throw AppException.NotFound();
        throw new AppException(410, "report_api_retired", "Use the new /report endpoint. The previous report system has been removed.");
    }
    [Authorize(Roles = "Owner")]
    [HttpPost("api/interviews/{id:guid}/evaluation-report")]
    [HttpPost("api/interviews/{id:guid}/evaluation-report/regenerate")]
    public Task<IActionResult> RetiredCommand(Guid id, CancellationToken ct) => Retired(id, ct);
}
