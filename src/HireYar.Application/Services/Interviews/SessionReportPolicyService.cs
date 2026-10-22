using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Domain.Interviews;
using HireYar.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Interviews;

public sealed class SessionReportPolicyService(IApplicationDbContext db, ICurrentUser user, TimeProvider time, SessionReportService reports)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static bool Valid(ReportPolicyRequest r, IReadOnlyList<InterviewSkill> skills) =>
        r.PassingScore >= 0 && r.StrongScore >= r.PassingScore && r.StrongScore <= 100 && r.MinimumCoverage is >0 and <=1 &&
        Math.Round(r.PassingScore,4)==r.PassingScore && Math.Round(r.StrongScore,4)==r.StrongScore && Math.Round(r.MinimumCoverage,6)==r.MinimumCoverage &&
        r.Weights is not null && r.Weights.Count==skills.Count && r.Weights.Count<=100 &&
        r.Weights.All(w=>w is not null && w.Weight is >=0 and <=1000 && Math.Round(w.Weight,4)==w.Weight && skills.Any(s=>s.SkillId==w.SkillId && (!s.IsRequired || w.Weight>0))) &&
        r.Weights.Select(w=>w.SkillId).Distinct().Count()==skills.Count && (skills.Count==0 || r.Weights.Any(w=>w.Weight>0));
    public static ReportPolicy Read(IReadOnlyList<InterviewSkill> skills, SessionReportPolicy? row, bool canEdit)
    {
        var defaults=skills.OrderBy(s=>s.SkillId).Select(s=>new ReportSkillWeight(s.SkillId,Math.Max(1,s.Priority))).ToArray();
        if(row is null) return new("Available",Guid.Empty,60,80,0.6m,defaults,"SessionSnapshot",canEdit,null);
        try {
            var weights=JsonSerializer.Deserialize<ReportSkillWeight[]>(row.WeightsJson,Json);
            var request=new ReportPolicyRequest(row.Revision,row.PassingScore,row.StrongScore,row.MinimumCoverage,weights!);
            if(!Valid(request,skills)) throw new JsonException();
            return new("Available",row.Revision,row.PassingScore,row.StrongScore,row.MinimumCoverage,weights!.OrderBy(w=>w.SkillId).ToArray(),"OwnerConfigured",canEdit,null);
        } catch(JsonException) {return new("Defaulted",row.Revision,60,80,0.6m,defaults,"SessionSnapshot",canEdit,"report_policy_invalid");}
    }
    public Task<ReportPolicy> UpdateAsync(Guid id, ReportPolicyRequest request, CancellationToken ct) =>
        db.WithOrganizationLockAsync(user.OrganizationId,async()=> {
            if(!user.CanAccess("reports", true)) throw new AppException(403,"forbidden","Only the owner may change report policy.");
            await reports.ReadAsync(id,ct);
            var skills=(await db.InterviewSessions.IgnoreAutoIncludes().Include(s=>s.Skills).SingleAsync(s=>s.Id==id && s.OrganizationId==user.OrganizationId,ct)).Skills;
            if(!Valid(request,skills)) throw AppException.Invalid("report_policy_invalid");
            var row=await db.SessionReportPolicies.SingleOrDefaultAsync(p=>p.OrganizationId==user.OrganizationId && p.InterviewSessionId==id,ct);
            if((row?.Revision??Guid.Empty)!=request.Revision) throw new AppException(409,"report_policy_changed","Reload the report before changing policy.");
            if(row is null) {row=new(){InterviewSessionId=id,OrganizationId=user.OrganizationId};db.SessionReportPolicies.Add(row);}
            row.Revision=Guid.NewGuid();row.PassingScore=request.PassingScore;row.StrongScore=request.StrongScore;row.MinimumCoverage=request.MinimumCoverage;
            row.WeightsJson=JsonSerializer.Serialize(request.Weights.OrderBy(w=>w.SkillId),Json);row.UpdatedAtUtc=time.GetUtcNow();row.UpdatedByUserId=user.UserId;
            return Read(skills,row,true);
        },ct);
}
