using HireYar.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Interviews;

// Server-only orchestration, using the already configured assessment/narrative provider.
// Existing completed interviews have the migration default false and are never auto-submitted.
public sealed class ReportCompletionService(IApplicationDbContext db,SessionReportService reports,
    AnswerAssessmentService assessments,ReportNarrativeService narratives)
{
    public async Task ProcessAsync(Guid org,Guid sessionId,CancellationToken ct)
    {
        if(!await db.InterviewSessions.IgnoreAutoIncludes().AsNoTracking().AnyAsync(s=>s.OrganizationId==org && s.Id==sessionId &&
            s.DeletedAtUtc==null && s.AutomaticReportCompletionEnabled,ct)) return;
        var report=await reports.ReadForOrganizationAsync(org,sessionId,true,ct);
        if(report.Identity.InterviewStatus!="Completed" || report.Questions.Any(q=>q.Assessment.IsPending)) return;
        foreach(var question in report.Questions.Where(q=>q.Answer is not null && q.Assessment.DimensionRecovery is {CanRetry:true}))
        {
            try {await assessments.RepairDimensionsAsync(org,sessionId,question.Answer!.AnswerId,ct);}
            catch(AppException e) when(e.Status==409) { } // A concurrent repair may have completed it.
        }
        report=await reports.ReadForOrganizationAsync(org,sessionId,true,ct);
        if(report.Questions.Any(q=>q.Assessment.IsPending || q.Assessment.DimensionRecovery is {CanRetry:true} or {State:"Processing"})) return;
        await narratives.GenerateAutomaticallyAsync(org,sessionId,ct);
    }
}