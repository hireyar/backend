using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Domain.Interviews;
using HireYar.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Interviews;

public sealed class SessionReportDecisionService(IApplicationDbContext db, ICurrentUser user, TimeProvider time, SessionReportService reports)
{
    public async Task<ReportHumanReview> RecordAsync(Guid id, ReportDecisionRequest request, CancellationToken ct)
    {
        if(!user.CanAccess("reports", true)) throw new AppException(403,"forbidden","Only the owner may record a decision.");
        if(request.RequestId==Guid.Empty || request.Decision is not ("advance" or "review" or "reject") || request.Note?.Length>2000 ||
            request.ReportFingerprint is null || request.ReportFingerprint.Length!=64 || !request.ReportFingerprint.All(Uri.IsHexDigit)) throw AppException.Invalid("report_decision_invalid");
        await db.WithOrganizationLockAsync(user.OrganizationId,async()=> {
            var report=await reports.ReadAsync(id,ct);
            if(report.Identity.InterviewStatus!="Completed") throw new AppException(409,"interview_not_completed","Complete the interview before recording a decision.");
            var existing=await db.InterviewReviewEntries.SingleOrDefaultAsync(e=>e.OrganizationId==user.OrganizationId && e.InterviewSessionId==id && e.RequestId==request.RequestId,ct);
            if(existing is not null) {
                if(existing.ActorUserId!=user.UserId || existing.Kind!=request.Decision || existing.Note!=request.Note || existing.ReportFingerprint!=request.ReportFingerprint) throw AppException.Conflict();
                return true;
            }
            if(report.Fingerprint!=request.ReportFingerprint) throw new AppException(409,"report_changed","Reload the report before recording a decision.");
            if(await db.InterviewReviewEntries.CountAsync(e=>e.OrganizationId==user.OrganizationId && e.InterviewSessionId==id && e.ActorUserId!=null,ct)>=100)
                throw new AppException(409,"review_history_limit","The session history limit was reached.");
            var now=time.GetUtcNow();
            db.InterviewReviewEntries.Add(new(){OrganizationId=user.OrganizationId,InterviewSessionId=id,RequestId=request.RequestId,
                ActorUserId=user.UserId,Kind=request.Decision,Note=request.Note,ReportFingerprint=report.Fingerprint,
                ReportSnapshotJson=JsonSerializer.Serialize(new ReportReviewSnapshot("comprehensive-report/1",report.OverallScore,report.Coverage,report.PositionFit,report.Policy with {CanEdit=false},report.Narrative)),
                RecordedAtUtc=new DateTimeOffset(now.Ticks-now.Ticks%10,TimeSpan.Zero)});
            return true;
        },ct);
        return (await reports.ReadAsync(id,ct)).HumanReview;
    }
}
