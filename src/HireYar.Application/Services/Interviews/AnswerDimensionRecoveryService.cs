using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Domain.Interviews;

namespace HireYar.Application.Interviews;

public sealed partial class AnswerAssessmentService
{
    private static bool IsRepairable(AnswerDimension d) => d.State != "Succeeded" &&
        d.FailureCode is "dimension_not_observed" or "dimension_not_returned" or "dimension_invalid_output" or "dimension_storage_invalid";

    // Owner retry or future-completion worker; the successful primary score and its budget never change.
    public async Task RepairDimensionsAsync(Guid org, Guid sessionId, Guid answerId, CancellationToken ct)
    {
        var claim = await db.WithOrganizationLockAsync<Claim?>(org, async () => {
            var source = await LoadAsync(org, answerId, ct);
            if (source is not { } data || data.Session.Id != sessionId) throw AppException.NotFound();
            var row = await RowAsync(org,answerId,ct);
            if (data.Session.Status != InterviewStatus.Completed || data.Answer.IsInteraction || string.IsNullOrWhiteSpace(data.Answer.Transcript) || row?.State != "Succeeded")
                throw new AppException(409,"dimensions_not_ready","A successful final answer assessment is required.");
            var now = time.GetUtcNow();
            if (row.DimensionAttemptId is not null && row.DimensionLeaseExpiresAtUtc > now) return null;
            var input = await InputAsync(data.Session,data.Answer,ct);
            if(View(data.Answer,true,row,now,input.InterviewType,input.PositionDescription).State!="Succeeded")
                throw new AppException(409,"dimensions_not_ready","The primary assessment evidence is not valid.");
            var missing = AnswerDimensionProtocol.Read(row.DimensionsJson,input.InterviewType,answerId,data.Answer.Transcript).Where(IsRepairable)
                .Where(d => d.Code is not ("job_alignment" or "work_alignment") || !string.IsNullOrWhiteSpace(input.PositionDescription)).Select(d => d.Code).ToHashSet();
            if (missing.Count == 0 || row.DimensionAttempts >= AnswerAssessmentOptions.MaxAttempts)
                throw new AppException(409,"dimensions_retry_unavailable","No recoverable dimensions or the attempt budget is exhausted.");
            input = input with { Dimensions = input.Dimensions!.Where(d => missing.Contains(d.Code)).ToArray() };
            row.DimensionAttempts++; row.DimensionAttemptId=Guid.NewGuid(); row.DimensionLeaseExpiresAtUtc=now.AddSeconds(options.AttemptTimeoutSeconds+15); row.DimensionFailureCode=null;
            return new(row.DimensionAttemptId.Value,input,Hash(await InputAsync(data.Session,data.Answer,ct)));
        },ct);
        if (claim is null) return;
        IReadOnlyList<AnswerDimension>? recovered = null;
        string? failure = null;
        Task<AnswerAssessmentResponse>? task = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.AttemptTimeoutSeconds),time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct,deadline.Token);
        try {
            task = provider.AssessAnswerAsync(claim.Input,linked.Token);
            var response = await task.WaitAsync(linked.Token);
            recovered = AnswerDimensionProtocol.Parse(response.Json,answerId,claim.Input);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
        catch(OperationCanceledException) { failure="dimensions_timeout"; }
                catch(InterviewProviderException e) { failure=e.Code switch {
            "provider_not_configured"=>"dimensions_not_configured", "answer_assessment_http_402"=>"dimensions_provider_payment_required",
            "answer_assessment_http_401"=>"dimensions_provider_unauthorized", "answer_assessment_http_403"=>"dimensions_provider_forbidden",
            _=>"dimensions_provider_failed"}; }
        catch(Exception) { failure="dimensions_internal_error"; }
        finally { if(task is not null) _=task.ContinueWith(t=>{_=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default); }
        await db.WithOrganizationLockAsync(org,async () => {
            var row=await RowAsync(org,answerId,ct); var source=await LoadAsync(org,answerId,ct);
            if(row?.DimensionAttemptId!=claim.Token || row.DimensionLeaseExpiresAtUtc<=time.GetUtcNow()) return false;
            row.DimensionAttemptId=null;row.DimensionLeaseExpiresAtUtc=null;
            if(source is not { } data || data.Session.Status!=InterviewStatus.Completed || string.IsNullOrWhiteSpace(data.Answer.Transcript) ||
                Hash(await InputAsync(data.Session,data.Answer,ct))!=claim.Hash) { row.DimensionFailureCode="dimensions_input_changed"; return false; }
            if(failure is null && recovered is not null) {
                var allowed=claim.Input.Dimensions!.Select(d=>d.Code).ToHashSet();
                var existing=AnswerDimensionProtocol.Read(row.DimensionsJson,claim.Input.InterviewType,answerId,data.Answer.Transcript);
                var merged=existing.Select(d=> IsRepairable(d) && allowed.Contains(d.Code)
                    ? recovered.SingleOrDefault(r=>r.Code==d.Code && (r.State=="Succeeded" || r.FailureCode=="dimension_insufficient_evidence")) ?? d : d).ToArray();
                row.DimensionsJson=JsonSerializer.Serialize(merged,Json);
                row.DimensionFailureCode=merged.Any(d=>IsRepairable(d) && allowed.Contains(d.Code)) ? "dimensions_incomplete_output" : null;
            } else row.DimensionFailureCode=failure;
            return true;
        },ct);
    }
}