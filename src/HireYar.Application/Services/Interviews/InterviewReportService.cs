using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HireYar.Application.Billing;

namespace HireYar.Application.Interviews;

public sealed class InterviewReportService(IApplicationDbContext db, ICurrentUser user, TimeProvider time,
    IInterviewReportProvider provider, InterviewBillingService billing, ILogger<InterviewReportService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private async Task<InterviewSession> Scoped(Guid id, CancellationToken ct) => await db.InterviewSessions.Include(s => s.Answers)
        .SingleOrDefaultAsync(s => s.Id == id && s.OrganizationId == user.OrganizationId && s.DeletedAtUtc == null, ct) ?? throw AppException.NotFound();

    public async Task<InterviewReportDto> GetAsync(Guid id, CancellationToken ct)
    {
        await Scoped(id, ct);
        var report = await db.InterviewEvaluationReports.AsNoTracking().SingleOrDefaultAsync(r => r.InterviewSessionId == id && r.OrganizationId == user.OrganizationId, ct);
        return report?.ResultJson is not null ? Map(report) : throw AppException.NotFound();
    }

    public async Task<InterviewReportDto> GenerateAsync(Guid id, CancellationToken ct, bool regenerate = false, GenerateInterviewReportRequest? settings = null)
    {
        var session = await Scoped(id, ct);
        var topics = session.Questions.Where(q => q.AskedAtUtc is not null || session.Answers.Any(a => a.QuestionId == q.QuestionId)).Select(q => q.Topic).Distinct().ToArray();
        if (settings?.ReviewThreshold is < 0 or > 100 || settings?.TopicWeights is { } weights &&
            (weights.Count > topics.Length || weights.Any(w => !topics.Contains(w.Key) || w.Value is <= 0 or > 100)))
            throw AppException.Invalid("report_settings_invalid");
        var lease = Guid.NewGuid(); InterviewEvaluationReport? row = null;
        var cached = await db.WithOrganizationBillingLockAsync(user.OrganizationId, async () => {
            await db.ReloadInterviewAsync(session, ct);
            if (session.DeletedAtUtc is not null) throw AppException.NotFound();
            if (session.Status != InterviewStatus.Completed) throw new AppException(409, "interview_not_completed", "Complete the interview before generating its report.");
            if (!(await billing.CapabilitiesAsync(session, ct)).Contains("interview_report"))
                throw new AppException(403, "feature_unavailable", "The interview contract does not include reports.");
            row = await db.InterviewEvaluationReports.SingleOrDefaultAsync(r => r.InterviewSessionId == id && r.OrganizationId == user.OrganizationId, ct);
            if (row?.ResultJson is not null && !regenerate) return Map(row);
            if (row?.LeaseExpiresAtUtc > time.GetUtcNow()) throw new AppException(409, "report_processing", "The evaluation report is being generated.");
            if (row is null) { row = new() { InterviewSessionId = id, OrganizationId = user.OrganizationId }; db.InterviewEvaluationReports.Add(row); }
            // Provider report timeout is limited to 120 seconds. Expiry allows recovery after process interruption.
            row.LeaseId = lease; row.LeaseExpiresAtUtc = time.GetUtcNow().AddSeconds(150);
            return (InterviewReportDto?)null;
        }, ct);
        if (cached is not null) return cached;
        db.DetachEntity(row!);
        try
        {
            var position = session.PositionTitleSnapshot is null || session.PositionDescriptionSnapshot is null
                ? await db.JobPositions.AsNoTracking().SingleAsync(p => p.Id == session.JobPositionId && p.OrganizationId == user.OrganizationId, ct) : null;
            var context = new InterviewReportContext(session.PositionTitleSnapshot ?? position!.Title, session.PositionDescriptionSnapshot ?? position!.Description,
                session.ResumeTextSnapshot, session.CandidateIntroductionTranscript, session.Language, session.CompletionReason?.ToString(),
                session.Answers.Where(a => a.Status == AnswerStatus.Processed).OrderBy(a => a.SubmittedAtUtc).ThenBy(a => a.ProcessedAtUtc).ThenBy(a => a.Id)
                    .Select(a => new InterviewReportAnswer(a.Id, a.QuestionId, session.Questions.Single(q => q.QuestionId == a.QuestionId).Text, a.Transcript, a.IsInteraction, a.AssistantMessage, a.SelectionSource)).ToArray(),
                session.Evidence.Select(e => new HrEvidenceDto(e.InterviewAnswerId, e.QuestionId, e.SkillId, e.ScoreBefore, e.ScoreAfter, e.Delta, e.Confidence, e.Evidence, e.CreatedAtUtc)).ToArray(),
                session.SkillStates.Select(s => new HrSkillStateDto(s.SkillId, s.CurrentScore, s.EvidenceCount, s.Confidence)).ToArray(),
                session.Questions.Where(q => q.AskedAtUtc is not null || session.Answers.Any(a => a.QuestionId == q.QuestionId)).Select(q => new HrQuestionDto(q.QuestionId, q.SkillId, q.Text, q.Topic, q.Difficulty)).ToArray(),
                session.RequiredCapabilityCode == "hr_interview" ? "general" : "specialized");
            if (context.InterviewType == "general") context = context with { Resume = null, Introduction = null };
            var generated = await provider.GenerateReportAsync(context, ct);
            var content = InterviewReportDetailsBuilder.Build(InterviewReportValidator.Validate(generated.Json, context, true, true), session, settings);
            return await db.WithOrganizationBillingLockAsync(user.OrganizationId, async () => {
                await db.ReloadInterviewAsync(session, ct);
                if (session.DeletedAtUtc is not null) throw AppException.NotFound();
                var owner = await db.InterviewEvaluationReports.SingleAsync(r => r.InterviewSessionId == id && r.OrganizationId == user.OrganizationId, ct);
                if (owner.LeaseId != lease || owner.LeaseExpiresAtUtc <= time.GetUtcNow()) throw AppException.Conflict();
                owner.ResultJson = JsonSerializer.Serialize(content, Json); owner.Model = generated.Model; owner.Provider = generated.Provider;
                var now = time.GetUtcNow();
                // PostgreSQL timestamptz stores microseconds; keep the first response identical to subsequent reads.
                owner.PromptVersion = InterviewReportPrompt.Version; owner.CreatedAtUtc = new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
                owner.LeaseId = null; owner.LeaseExpiresAtUtc = null;
                return Map(owner);
            }, ct);
        }
        catch (Exception error)
        {
            // Release only our lease. Process death instead recovers through expiry, without changing interview state.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await db.WithOrganizationBillingLockAsync(user.OrganizationId, async () => {
                    var owner = await db.InterviewEvaluationReports.SingleAsync(r => r.InterviewSessionId == id && r.OrganizationId == user.OrganizationId, cleanup.Token);
                    if (owner.LeaseId == lease) { owner.LeaseId = null; owner.LeaseExpiresAtUtc = null; }
                    return true;
                }, cleanup.Token);
            }
            catch (Exception cleanupError)
            {
                logger.LogWarning("Evaluation report lease cleanup failed errorType={ErrorType}", cleanupError.GetType().Name);
            }
            if (error is InterviewProviderException) throw new AppException(503, "evaluation_report_unavailable", "The evaluation report could not be generated. Retry later.");
            throw;
        }
    }

    private static InterviewReportDto Map(InterviewEvaluationReport row)
    {
        var content = JsonSerializer.Deserialize<InterviewReportContent>(row.ResultJson!, Json)!;
        // JSONB normalizes object-key order. Keep the initial and cached API representations stable.
        if (content.Details is { TopicWeights: { } weights } details)
            content = content with { Details = details with { TopicWeights = weights.OrderBy(w => w.Key, StringComparer.Ordinal).ToDictionary(w => w.Key, w => w.Value) } };
        return new(row.InterviewSessionId, row.Model!, row.Provider!, row.PromptVersion!, row.CreatedAtUtc!.Value, content);
    }
}
