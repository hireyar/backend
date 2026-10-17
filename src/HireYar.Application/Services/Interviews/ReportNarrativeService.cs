using HireYar.Application.Common;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Interviews;

public sealed class ReportNarrativeService(IApplicationDbContext db,ICurrentUser user,SessionReportService reports,
    IReportNarrativeProvider provider,TimeProvider time,AnswerAssessmentOptions options)
{
    private sealed record Claim(Guid Token,ReportNarrativeInput Input,string Hash);
    private async Task<SessionReportNarrative?> Row(Guid org,Guid id,string hash,CancellationToken ct)
    {
        var tracked=db.SessionReportNarratives.Local.SingleOrDefault(r=>r.InterviewSessionId==id && r.InputHash==hash);
        if(tracked is not null) db.DetachEntity(tracked);
        return await db.SessionReportNarratives.SingleOrDefaultAsync(r=>r.OrganizationId==org && r.InterviewSessionId==id && r.InputHash==hash,ct);
    }
    private async Task<ReportNarrativeInput> Input(Guid org,Guid id,SessionReportDocument report,CancellationToken ct)
    {
        var session=await db.InterviewSessions.IgnoreAutoIncludes().AsNoTracking().SingleAsync(s=>s.Id==id && s.OrganizationId==org && s.DeletedAtUtc==null,ct);
        return ReportNarrativeProtocol.Input(session,report);
    }
    public Task<ReportNarrative> GenerateAsync(Guid id,string fingerprint,CancellationToken ct)
    {
        if(user.Role!=HireYar.Domain.Organizations.OrganizationRole.Owner) throw new AppException(403,"forbidden","Only the owner may generate report prose.");
        return GenerateCoreAsync(user.OrganizationId,id,fingerprint,ct);
    }

    internal async Task GenerateAutomaticallyAsync(Guid org,Guid id,CancellationToken ct)
    {
        var report=await reports.ReadForOrganizationAsync(org,id,true,ct);
        if(report.Narrative is {CanGenerate:true}) await GenerateCoreAsync(org,id,report.Fingerprint,ct);
    }

    private async Task<ReportNarrative> GenerateCoreAsync(Guid org,Guid id,string fingerprint,CancellationToken ct)
    {
        if(fingerprint is null || fingerprint.Length!=64 || !fingerprint.All(Uri.IsHexDigit)) throw AppException.Invalid("report_fingerprint_invalid");

        var claim=await db.WithOrganizationBillingLockAsync<Claim?>(org,async () => {
            var report=await reports.ReadForOrganizationAsync(org,id,true,ct);
            if(report.Narrative is {State:"Available" or "InsufficientEvidence",GeneratedAtUtc:not null} cached) {
                var previous=await Row(org,id,cached.InputHash,ct);
                if(fingerprint!=report.Fingerprint && fingerprint!=previous?.ReportFingerprint) throw new AppException(409,"report_changed","Read the current report before generating prose.");
                return null;
            }
            if(report.Fingerprint!=fingerprint) throw new AppException(409,"report_changed","Read the current report before generating prose.");
            if(report.Identity.InterviewStatus!="Completed") throw new AppException(409,"narrative_not_ready","The interview must be completed.");
            var input=await Input(org,id,report,ct);
            if(!input.Sources.Any(s=>s.Kind=="answer")) throw new AppException(409,"narrative_evidence_missing","No final answer transcript is available.");
            var hash=ReportNarrativeProtocol.Hash(fingerprint,input);var row=await Row(org,id,hash,ct);var now=time.GetUtcNow();
            if(row?.State=="Succeeded" || row?.State=="Processing" && row.LeaseExpiresAtUtc>now) return null;
            if(row?.Attempts>=3) throw new AppException(409,"narrative_retry_unavailable","The attempt budget is exhausted.");
            if(row is null) {row=new(){OrganizationId=org,InterviewSessionId=id,InputHash=hash,ReportFingerprint=fingerprint,RequestedAtUtc=now};db.SessionReportNarratives.Add(row);}
            row.State="Processing";row.Attempts++;row.AttemptId=Guid.NewGuid();row.LeaseExpiresAtUtc=now.AddSeconds(options.AttemptTimeoutSeconds+15);row.FailureCode=null;row.FinishedAtUtc=null;
            return new(row.AttemptId.Value,input,hash);
        },ct);
        if(claim is null) return (await reports.ReadForOrganizationAsync(org,id,true,ct)).Narrative!;
        string? result=null;string? failure=null;AnswerAssessmentResponse? response=null;Task<AnswerAssessmentResponse>? task=null;
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(options.AttemptTimeoutSeconds),time);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,deadline.Token);
        try {
            task=provider.GenerateNarrativeAsync(claim.Input,linked.Token);response=await task.WaitAsync(linked.Token);
            if(string.IsNullOrWhiteSpace(response.Provider) || response.Provider.Length>100 || string.IsNullOrWhiteSpace(response.Model) || response.Model.Length>100) throw new InterviewProviderException("narrative_invalid_output");
            result=ReportNarrativeProtocol.Validate(response.Json,claim.Input);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) {throw;}
        catch(OperationCanceledException) {failure="narrative_timeout";}
                catch(InterviewProviderException e) {failure=e.Code switch {
            "provider_not_configured"=>"narrative_not_configured", "report_narrative_http_402"=>"narrative_provider_payment_required",
            "report_narrative_http_401"=>"narrative_provider_unauthorized", "report_narrative_http_403"=>"narrative_provider_forbidden",
            "narrative_invalid_output"=>e.Code,_=>"narrative_provider_failed"};}
        catch(Exception) {failure="narrative_internal_error";}
        finally {if(task is not null) _=task.ContinueWith(t=>{_=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);}
        await db.WithOrganizationBillingLockAsync(org,async () => {
            var row=await Row(org,id,claim.Hash,ct);
            if(row?.AttemptId!=claim.Token || row.LeaseExpiresAtUtc<=time.GetUtcNow()) return false;
            var fresh=await reports.ReadForOrganizationAsync(org,id,true,ct);var input=await Input(org,id,fresh,ct);
            if(ReportNarrativeProtocol.Hash(fresh.Fingerprint,input)!=claim.Hash) failure="narrative_input_changed";
            row.State=failure is null?"Succeeded":"Failed";row.FailureCode=failure;row.AttemptId=null;row.LeaseExpiresAtUtc=null;row.FinishedAtUtc=time.GetUtcNow();
            if(failure is null) {row.ResultJson=result!;row.Provider=response!.Provider;row.Model=response.Model;}
            return true;
        },ct);
        return (await reports.ReadForOrganizationAsync(org,id,true,ct)).Narrative!;
    }
}