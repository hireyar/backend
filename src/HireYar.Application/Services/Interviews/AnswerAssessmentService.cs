using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HireYar.Application.Billing;
using HireYar.Application.Common;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HireYar.Application.Interviews;

public sealed class AnswerAssessmentService(IApplicationDbContext db, InterviewBillingService billing,
    IAnswerAssessmentProvider provider, TimeProvider time, AnswerAssessmentOptions options,
    ILogger<AnswerAssessmentService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Claim(Guid Token, AnswerAssessmentInput Input, string Hash);

    private async Task<(InterviewSession Session, InterviewAnswer Answer)?> LoadAsync(Guid org, Guid answerId, CancellationToken ct)
    {
        var session = await (from s in db.InterviewSessions.IgnoreAutoIncludes().AsNoTracking().Include(s => s.Questions)
            join a in db.InterviewAnswers.AsNoTracking() on s.Id equals a.InterviewSessionId
            where s.OrganizationId == org && s.DeletedAtUtc == null && a.Id == answerId
            select s).SingleOrDefaultAsync(ct);
        if (session is null) return null;
        var answer = await db.InterviewAnswers.AsNoTracking().SingleAsync(a => a.Id == answerId && a.InterviewSessionId == session.Id, ct);
        return (session, answer);
    }
    private async Task<bool> IncludedAsync(InterviewSession session, CancellationToken ct) =>
        !await db.InterviewBillings.AnyAsync(b => b.OrganizationId == session.OrganizationId && b.InterviewSessionId == session.Id, ct) ||
        (await billing.CapabilitiesAsync(session, ct)).Contains("interview_report");
    private async Task<AnswerAssessmentInput> InputAsync(InterviewSession s, InterviewAnswer a, CancellationToken ct)
    {
        var q = s.Questions.Single(q => q.QuestionId == a.QuestionId);
        var title = s.PositionTitleSnapshot ?? await db.JobPositions.Where(p => p.OrganizationId == s.OrganizationId && p.Id == s.JobPositionId).Select(p => p.Title).SingleAsync(ct);
        return new(title, s.RequiredCapabilityCode == "hr_interview" ? "general" : "specialized", q.Text, q.Topic, q.Difficulty,
            AnswerAssessmentProtocol.Excerpts(a.Transcript!));
    }
    private static string Hash(AnswerAssessmentInput input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        AnswerAssessmentOptions.Version + JsonSerializer.Serialize(input, Json))));
    private async Task<AnswerReportAssessment?> RowAsync(Guid org, Guid answerId, CancellationToken ct)
    {
        var row = await db.AnswerAssessments.SingleOrDefaultAsync(r => r.OrganizationId == org && r.AnswerId == answerId, ct);
        if (row is not null) await db.ReloadAnswerAssessmentAsync(row, ct);
        return row;
    }

    public async Task ProcessAsync(Guid org, Guid answerId, CancellationToken ct)
    {
        // Only short DB operations run under the existing organization lock; no model/network I/O.
        var claim = await db.WithOrganizationBillingLockAsync<Claim?>(org, async () => {
            var source = await LoadAsync(org, answerId, ct);
            if (source is not { } data || data.Session.Status != InterviewStatus.Completed || data.Answer.IsInteraction || string.IsNullOrWhiteSpace(data.Answer.Transcript)) return null;
            var now = time.GetUtcNow(); var row = await RowAsync(org, answerId, ct);
            if (row is null) {
                row = new() { AnswerId = answerId, OrganizationId = org, InterviewSessionId = data.Session.Id, RequestedAtUtc = now };
                db.AnswerAssessments.Add(row);
            }
            if (!await IncludedAsync(data.Session, ct)) { row.State = "Unavailable"; row.FailureCode = "feature_unavailable"; return null; }
            if (row.State == "Processing" && row.LeaseExpiresAtUtc <= now) {
                row.AttemptId = null; row.LeaseExpiresAtUtc = null; row.FailureCode = "assessment_lease_expired";
                row.State = row.Attempts < AnswerAssessmentOptions.MaxAttempts ? "Queued" : "Failed";
                row.FinishedAtUtc = now;
            }
            if (row.State != "Queued" || row.NextAttemptAtUtc > now) return null;
            if (row.Attempts >= AnswerAssessmentOptions.MaxAttempts) { row.State = "Failed"; row.FinishedAtUtc = now; return null; }
            var input = await InputAsync(data.Session, data.Answer, ct);
            row.State = "Processing"; row.Attempts++; row.AttemptId = Guid.NewGuid(); row.InputHash = Hash(input);
            row.StartedAtUtc = now; row.FinishedAtUtc = null; row.NextAttemptAtUtc = null; row.FailureCode = null;
            row.LeaseExpiresAtUtc = now.AddSeconds(options.AttemptTimeoutSeconds + 15);
            return new(row.AttemptId.Value, input, row.InputHash);
        }, ct);
        if (claim is null) return;

        AnswerAssessmentResponse? response = null;
        (decimal? Score, decimal? Confidence, string Explanation, AnswerAssessmentEvidence[] Evidence)? parsed = null;
        string? failure = null; var retry = false;
        Task<AnswerAssessmentResponse>? task = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.AttemptTimeoutSeconds), time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try {
            task = provider.AssessAnswerAsync(claim.Input, linked.Token);
            response = await task.WaitAsync(linked.Token);
            if (string.IsNullOrWhiteSpace(response.Provider) || response.Provider.Length > 100 || string.IsNullOrWhiteSpace(response.Model) || response.Model.Length > 100)
                throw new InterviewProviderException("assessment_invalid_output");
            parsed = AnswerAssessmentProtocol.Parse(response.Json, answerId, claim.Input.Excerpts);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { failure = "assessment_timeout"; retry = true; }
        catch (InterviewProviderException e) {
            (failure, retry) = e.Code switch {
                "provider_not_configured" => ("assessment_not_configured", false),
                "answer_assessment_timeout" or "answer_assessment_unavailable" => ("assessment_provider_unavailable", true),
                "answer_assessment_http_429" or "answer_assessment_http_408" => ("assessment_provider_busy", true),
                _ when e.Code.StartsWith("answer_assessment_http_5", StringComparison.Ordinal) => ("assessment_provider_unavailable", true),
                "assessment_invalid_output" or "assessment_invalid_envelope" or "answer_assessment_response_too_large" => ("assessment_invalid_output", false),
                _ => ("assessment_provider_rejected", false)
            };
        }
        catch (Exception) { failure = "assessment_internal_error"; }
        finally {
            // A provider ignoring cancellation cannot hold up the worker or commit a late result.
            if (task is not null) _ = task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        await db.WithOrganizationBillingLockAsync(org, async () => {
            var row = await RowAsync(org, answerId, ct); var now = time.GetUtcNow();
            if (row is null || row.State != "Processing" || row.AttemptId != claim.Token || row.LeaseExpiresAtUtc <= now) return false;
            var source = await LoadAsync(org, answerId, ct);
            if (source is not { } data || data.Session.Status != InterviewStatus.Completed || data.Answer.IsInteraction || string.IsNullOrWhiteSpace(data.Answer.Transcript) ||
                Hash(await InputAsync(data.Session, data.Answer, ct)) != claim.Hash) { failure = "assessment_input_changed"; retry = false; }
            row.AttemptId = null; row.LeaseExpiresAtUtc = null; row.FinishedAtUtc = now;
            if (failure is null && parsed is { } result) {
                row.State = result.Score is null ? "InsufficientEvidence" : "Succeeded";
                row.Score = result.Score is null ? null : Math.Round(result.Score.Value, 4);
                row.Confidence = result.Confidence is null ? null : Math.Round(result.Confidence.Value, 6);
                row.Explanation = result.Explanation; row.EvidenceJson = JsonSerializer.Serialize(result.Evidence, Json);
                row.Provider = response!.Provider; row.Model = response.Model;
            } else {
                row.FailureCode = failure ?? "assessment_internal_error";
                row.State = retry && row.Attempts < AnswerAssessmentOptions.MaxAttempts ? "Queued" : "Failed";
                row.NextAttemptAtUtc = row.State == "Queued" ? now.AddSeconds(row.Attempts == 1 ? 5 : 30) : null;
                logger.LogWarning("Answer assessment failed answerId={AnswerId} code={Code} attempts={Attempts}", answerId, row.FailureCode, row.Attempts);
            }
            return true;
        }, ct);
    }

    public Task<bool> RetryAsync(Guid org, Guid sessionId, Guid answerId, CancellationToken ct) =>
        db.WithOrganizationBillingLockAsync(org, async () => {
            var source = await LoadAsync(org, answerId, ct);
            if (source is not { } data || data.Session.Id != sessionId) throw AppException.NotFound();
            if (!await IncludedAsync(data.Session, ct)) throw new AppException(403, "feature_unavailable", "Reports are not included.");
            if (data.Session.Status != InterviewStatus.Completed || data.Answer.IsInteraction || string.IsNullOrWhiteSpace(data.Answer.Transcript))
                throw new AppException(409, "assessment_not_ready", "This answer is not ready for assessment.");
            var row = await RowAsync(org, answerId, ct);
            if (row is null) { db.AnswerAssessments.Add(new() { AnswerId = answerId, OrganizationId = org, InterviewSessionId = sessionId, RequestedAtUtc = time.GetUtcNow() }); return true; }
            if (row.State == "Processing" && row.LeaseExpiresAtUtc <= time.GetUtcNow()) {
                row.State = "Failed"; row.FailureCode = "assessment_lease_expired"; row.AttemptId = null; row.LeaseExpiresAtUtc = null; row.FinishedAtUtc = time.GetUtcNow();
            }
            if (row.State is "Queued" or "Processing") return true; // Idempotent retries do not grant a second model call.
            if (row.State != "Failed" || row.Attempts >= AnswerAssessmentOptions.MaxAttempts)
                throw new AppException(409, "assessment_retry_unavailable", "The assessment is final or its attempt budget is exhausted.");
            row.State = "Queued"; row.NextAttemptAtUtc = time.GetUtcNow(); return true;
        }, ct);

    public static ReportAnswerAssessment View(InterviewAnswer? answer, bool completed, AnswerReportAssessment? row, DateTimeOffset now)
    {
        string state; string? failure = row?.FailureCode; AnswerAssessmentEvidence[] evidence = [];
        if (answer is null) state = "NotApplicable";
        else if (!completed) state = "NotReady";
        else if (string.IsNullOrWhiteSpace(answer.Transcript)) { state = "Unavailable"; failure = "assessment_transcript_unavailable"; }
        else state = row?.State ?? "Queued";
        if (state == "Processing" && row!.LeaseExpiresAtUtc <= now) { state = row.Attempts < AnswerAssessmentOptions.MaxAttempts ? "RecoveryPending" : "Failed"; failure = "assessment_lease_expired"; }
        if (state == "Succeeded") {
            try {
                evidence = JsonSerializer.Deserialize<AnswerAssessmentEvidence[]>(row!.EvidenceJson, Json) ?? [];
                if (evidence.Length is < 1 or > 5 || evidence.Any(e => e is null || e.AnswerId != answer!.Id || e.Start < 0 || e.Length <= 0 || e.Start > answer.Transcript!.Length - e.Length || answer.Transcript.Substring(e.Start, e.Length) != e.Quote)) throw new JsonException();
            } catch (JsonException) { state = "Unavailable"; failure = "assessment_storage_invalid"; evidence = []; }
        }
        var available = state is "Succeeded" or "InsufficientEvidence";
        return new(state, state == "Succeeded" ? row!.Score : null, state == "Succeeded" ? row!.Confidence : null,
            state == "Succeeded" ? "answer-quality/100/v1" : null, available ? row!.Explanation : null, evidence,
            row?.Attempts ?? 0, state == "Failed" && row!.Attempts < AnswerAssessmentOptions.MaxAttempts, failure,
            row?.RequestedAtUtc, row?.StartedAtUtc, row?.FinishedAtUtc, row?.NextAttemptAtUtc,
            row?.EvaluatorVersion, row?.Provider, row?.Model);
    }
}
