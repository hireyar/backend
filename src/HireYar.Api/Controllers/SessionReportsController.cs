using HireYar.Application.Common;
using HireYar.Application.Interviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Api.Controllers;

[ApiController, Authorize(Roles = "Owner,Recruiter"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SessionReportsController(SessionReportService reports, IApplicationDbContext db, ICurrentUser user, AnswerAssessmentService assessments, SessionReportDecisionService decisions, ReportNarrativeService narratives) : ControllerBase
{
    [HttpGet("api/interviews/{id:guid}/report")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct) => new JsonResult(SessionReportOutput.Public(await reports.ReadAsync(id, ct)));

    [HttpGet("api/interviews/{id:guid}/report/print")]
    public async Task<IActionResult> Print(Guid id, CancellationToken ct)
    {
        var report = await reports.ReadAsync(id, ct);
        Response.Headers.ContentSecurityPolicy = SessionReportPage.ContentSecurityPolicy;
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Content(SessionReportPage.Render(report), "text/html; charset=utf-8");
    }
    [Authorize]
    [HttpPost("api/interviews/{id:guid}/report/answers/{answerId:guid}/assessment/retry")]
    [ProducesResponseType(typeof(RetryAnswerAssessmentResponse), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<RetryAnswerAssessmentResponse>> RetryAssessment(Guid id, Guid answerId, CancellationToken ct)
    {
        await reports.ReadAsync(id, ct); // Same tenant, soft-delete and purchased capability checks as reading.
        await assessments.RetryAsync(user.OrganizationId, id, answerId, ct);
        var report = await reports.ReadAsync(id, ct);
        return Accepted($"/api/interviews/{id}/report", new RetryAnswerAssessmentResponse(id, answerId, report.Questions.Single(q => q.Answer?.AnswerId == answerId).Assessment));
    }
    [Authorize]
    [HttpPut("api/interviews/{id:guid}/report/policy")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePolicy(Guid id, ReportPolicyRequest request, CancellationToken ct)
    {
        await reports.ReadAsync(id, ct);
        throw AppException.NotFound();
    }

    [Authorize]
    [HttpPost("api/interviews/{id:guid}/report/review")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordDecision(Guid id, ReportDecisionRequest request, CancellationToken ct) => new JsonResult(SessionReportOutput.Public(await decisions.RecordAsync(id, request, ct)));

    [Authorize]
    [HttpPost("api/interviews/{id:guid}/report/answers/{answerId:guid}/dimensions/retry")]
    [ProducesResponseType(typeof(RetryAnswerAssessmentResponse),StatusCodes.Status200OK)]
    public async Task<ActionResult<RetryAnswerAssessmentResponse>> RepairDimensions(Guid id,Guid answerId,CancellationToken ct)
    {
        await reports.ReadAsync(id,ct);
        await assessments.RepairDimensionsAsync(user.OrganizationId,id,answerId,ct);
        var report=await reports.ReadAsync(id,ct);
        return Ok(new RetryAnswerAssessmentResponse(id,answerId,report.Questions.Single(q=>q.Answer?.AnswerId==answerId).Assessment));
    }
    [Authorize]
    [HttpPost("api/interviews/{id:guid}/report/narrative")]
    [ProducesResponseType(typeof(ReportNarrative),StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportNarrative>> GenerateNarrative(Guid id,GenerateReportNarrativeRequest request,CancellationToken ct) =>
        Ok(await narratives.GenerateAsync(id,request.ReportFingerprint,ct));

    [HttpGet("api/interviews/{id:guid}/evaluation-report")]
    [HttpGet("api/interviews/{id:guid}/evaluation-report/status")]
    [HttpGet("api/interviews/{id:guid}/evaluation-report/print")]
    public async Task<IActionResult> Retired(Guid id, CancellationToken ct)
    {
        if (!await db.InterviewSessions.IgnoreAutoIncludes().AsNoTracking().AnyAsync(s => s.Id == id && s.OrganizationId == user.OrganizationId && s.DeletedAtUtc == null, ct))
            throw AppException.NotFound();
        throw new AppException(410, "report_api_retired", "Use the new /report endpoint. The previous report system has been removed.");
    }
    [Authorize]
    [HttpPost("api/interviews/{id:guid}/evaluation-report")]
    [HttpPost("api/interviews/{id:guid}/evaluation-report/regenerate")]
    public Task<IActionResult> RetiredCommand(Guid id, CancellationToken ct) => Retired(id, ct);
}
