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
    private async Task<InterviewSession> Scoped(Guid id, Guid organizationId, CancellationToken ct) => await db.InterviewSessions.Include(s => s.Answers)
        .SingleOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId && s.DeletedAtUtc == null, ct) ?? throw AppException.NotFound();

    public async Task<InterviewReportDto> GetAsync(Guid id, CancellationToken ct)
    {
        await StatusSessionAsync(id, ct);
        var report = await db.InterviewEvaluationReports.AsNoTracking().SingleOrDefaultAsync(r => r.InterviewSessionId == id && r.OrganizationId == user.OrganizationId, ct);
        return report?.ResultJson is not null ? Map(report) : throw new AppException(409, "report_not_ready", "The evaluation report is not ready. Check its generation status.");
    }

    public async Task<InterviewReportGenerationDto> GetStatusAsync(Guid id, CancellationToken ct)
    {
        var session = await StatusSessionAsync(id, ct);
        var available = session.Status == InterviewStatus.Completed && (await billing.CapabilitiesAsync(session, ct)).Contains("interview_report");
        var row = await db.InterviewEvaluationReports.AsNoTracking().SingleOrDefaultAsync(r => r.InterviewSessionId == id && r.OrganizationId == user.OrganizationId, ct);
        return Status(session, row, available);
    }

    private async Task<InterviewSession> StatusSessionAsync(Guid id, CancellationToken ct) =>
        await db.InterviewSessions.IgnoreAutoIncludes().AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id && s.OrganizationId == user.OrganizationId && s.DeletedAtUtc == null, ct) ?? throw AppException.NotFound();

    public async Task<InterviewReportGenerationDto> RetryAsync(Guid id, GenerateInterviewReportRequest? settings, CancellationToken ct)
    {
        var session = await Scoped(id, user.OrganizationId, ct);
        var topics = session.Questions.Where(q => q.AskedAtUtc is not null || session.Answers.Any(a => a.QuestionId == q.QuestionId)).Select(q => q.Topic).Distinct().ToArray();
        if (settings?.ReviewThreshold is < 0 or > 100 || settings?.TopicWeights is { } weights &&
            (weights.Count > topics.Length || weights.Any(w => !topics.Contains(w.Key) || w.Value is <= 0 or > 100)))
            throw AppException.Invalid("report_settings_invalid");
        var row = await db.WithOrganizationBillingLockAsync(user.OrganizationId, async () => {
            await db.ReloadInterviewAsync(session, ct);
            if (session.DeletedAtUtc is not null) throw AppException.NotFound();
            if (session.Status != InterviewStatus.Completed) throw new AppException(409, "interview_not_completed", "Complete the interview before generating its report.");
            if (!(await billing.CapabilitiesAsync(session, ct)).Contains("interview_report"))
                throw new AppException(403, "feature_unavailable", "The interview contract does not include reports.");
            var current = await db.InterviewEvaluationReports.SingleOrDefaultAsync(r => r.InterviewSessionId == id && r.OrganizationId == user.OrganizationId, ct);
            if (current is not null) await db.ReloadReportAsync(current, ct);
            if (current is null) {
                // A historical completed interview without a result has no successful generation.
                current = new() { InterviewSessionId = id, OrganizationId = user.OrganizationId, FailureCode = "report_not_generated" };
                db.InterviewEvaluationReports.Add(current);
            }
            Expire(current);
            if (current.GenerationStage != "Failed") throw new AppException(409, "report_retry_not_allowed", "Retry is allowed only after report generation fails.");
            current.GenerationStage = "Queued"; current.RequestedAtUtc = time.GetUtcNow();
            current.StartedAtUtc = null; current.FinishedAtUtc = null; current.FailureCode = null; current.FailureStage = null;
            current.LeaseId = null; current.LeaseExpiresAtUtc = null;
            if (settings is not null) current.GenerationSettingsJson = JsonSerializer.Serialize(settings, Json);
            return current;
        }, ct);
        return Status(session, row, true);
    }

    // Internal worker entry: organization comes from the persisted queue, never client input.
    public async Task ProcessQueuedAsync(Guid organizationId, Guid id, CancellationToken ct)
    {
        var session = await Scoped(id, organizationId, ct);
        var lease = Guid.NewGuid(); InterviewEvaluationReport? row = null;
        var claimed = await db.WithOrganizationBillingLockAsync(organizationId, async () => {
            await db.ReloadInterviewAsync(session, ct);
            if (session.DeletedAtUtc is not null || session.Status != InterviewStatus.Completed) return false;
            row = await db.InterviewEvaluationReports.SingleOrDefaultAsync(r => r.InterviewSessionId == id && r.OrganizationId == organizationId, ct);
            if (row is null) return false;
            await db.ReloadReportAsync(row, ct);
            if (Expire(row) || row.GenerationStage != "Queued") return false;
            row.LeaseId = lease; row.LeaseExpiresAtUtc = time.GetUtcNow().AddSeconds(150);
            row.GenerationStage = "Generating"; row.GenerationAttempts++; row.StartedAtUtc = time.GetUtcNow();
            row.FinishedAtUtc = null; row.FailureCode = null; row.FailureStage = null;
            return true;
        }, ct);
        if (!claimed) return;
        db.DetachEntity(row!);
        try
        {
            var settings = row!.GenerationSettingsJson is null ? null : JsonSerializer.Deserialize<GenerateInterviewReportRequest>(row.GenerationSettingsJson, Json);
            var position = session.PositionTitleSnapshot is null || session.PositionDescriptionSnapshot is null
                ? await db.JobPositions.AsNoTracking().SingleAsync(p => p.Id == session.JobPositionId && p.OrganizationId == organizationId, ct) : null;
            var context = new InterviewReportContext(session.PositionTitleSnapshot ?? position!.Title, session.PositionDescriptionSnapshot ?? position!.Description,
                session.ResumeTextSnapshot, session.CandidateIntroductionTranscript, session.Language, session.CompletionReason?.ToString(),
                session.Answers.Where(a => a.Status == AnswerStatus.Processed).OrderBy(a => a.SubmittedAtUtc).ThenBy(a => a.ProcessedAtUtc).ThenBy(a => a.Id)
                    .Select(a => new InterviewReportAnswer(a.Id, a.QuestionId, session.Questions.Single(q => q.QuestionId == a.QuestionId).Text, a.Transcript, a.IsInteraction, a.AssistantMessage, a.SelectionSource)).ToArray(),
                session.Evidence.Select(e => new HrEvidenceDto(e.InterviewAnswerId, e.QuestionId, e.SkillId, e.ScoreBefore, e.ScoreAfter, e.Delta, e.Confidence, e.Evidence, e.CreatedAtUtc)).ToArray(),
                session.SkillStates.Select(s => new HrSkillStateDto(s.SkillId, s.CurrentScore, s.EvidenceCount, s.Confidence)).ToArray(),
                session.Questions.Where(q => q.AskedAtUtc is not null || session.Answers.Any(a => a.QuestionId == q.QuestionId)).Select(q => new HrQuestionDto(q.QuestionId, q.SkillId, q.Text, q.Topic, q.Difficulty)).ToArray(),
                session.RequiredCapabilityCode == "hr_interview" ? "general" : "specialized");
            if (context.InterviewType == "general") context = context with { Resume = null, Introduction = null };
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(130));
            var generated = await provider.GenerateReportAsync(context, deadline.Token);
            await SetStageAsync(organizationId, id, lease, "Validating", ct);
            var content = InterviewReportDetailsBuilder.Build(InterviewReportValidator.Validate(generated.Json, context, true, true), session, settings);
            await SetStageAsync(organizationId, id, lease, "Saving", ct);
            await db.WithOrganizationBillingLockAsync(organizationId, async () => {
                await db.ReloadInterviewAsync(session, ct);
                if (session.DeletedAtUtc is not null) throw AppException.NotFound();
                var owner = await db.InterviewEvaluationReports.SingleAsync(r => r.InterviewSessionId == id && r.OrganizationId == organizationId, ct);
                await db.ReloadReportAsync(owner, ct);
                if (owner.LeaseId != lease || owner.LeaseExpiresAtUtc <= time.GetUtcNow()) throw AppException.Conflict();
                owner.ResultJson = JsonSerializer.Serialize(content, Json); owner.Model = generated.Model; owner.Provider = generated.Provider;
                var now = time.GetUtcNow();
                // PostgreSQL timestamptz stores microseconds; keep the first response identical to subsequent reads.
                owner.PromptVersion = InterviewReportPrompt.Version; owner.CreatedAtUtc = new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
                owner.LeaseId = null; owner.LeaseExpiresAtUtc = null;
                owner.GenerationStage = "Succeeded"; owner.FinishedAtUtc = owner.CreatedAtUtc; owner.FailureCode = null; owner.FailureStage = null;
                return true;
            }, ct);
        }
        catch (Exception error)
        {
            // Persist a safe failure reason. Never publish provider bodies, transcripts or credentials.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await db.WithOrganizationBillingLockAsync(organizationId, async () => {
                    var owner = await db.InterviewEvaluationReports.SingleAsync(r => r.InterviewSessionId == id && r.OrganizationId == organizationId, cleanup.Token);
                    await db.ReloadReportAsync(owner, cleanup.Token);
                    if (owner.LeaseId == lease) {
                        owner.FailureStage = owner.GenerationStage;
                        owner.FailureCode = owner.GenerationStage == "Validating" ? "report_validation_failed" :
                            error is OperationCanceledException ? "report_interrupted" :
                            error is InterviewProviderException { Code: "report_timeout" } ? "report_timeout" :
                            error is InterviewProviderException { Code: "report_invalid_response" } ? "report_invalid_response" :
                            error is InterviewProviderException ? "evaluation_report_unavailable" : "report_generation_failed";
                        owner.GenerationStage = "Failed"; owner.FinishedAtUtc = time.GetUtcNow();
                        owner.LeaseId = null; owner.LeaseExpiresAtUtc = null;
                    }
                    return true;
                }, cleanup.Token);
            }
            catch (Exception cleanupError)
            {
                logger.LogWarning("Evaluation report lease cleanup failed errorType={ErrorType}", cleanupError.GetType().Name);
            }
            logger.LogWarning("Evaluation report generation failed interviewId={InterviewId} errorType={ErrorType}", id, error.GetType().Name);
            if (ct.IsCancellationRequested) throw;
        }
    }

    private async Task SetStageAsync(Guid organizationId, Guid id, Guid lease, string stage, CancellationToken ct)
    {
        await db.WithOrganizationBillingLockAsync(organizationId, async () => {
            var current = await db.InterviewEvaluationReports.SingleAsync(r => r.InterviewSessionId == id && r.OrganizationId == organizationId, ct);
            await db.ReloadReportAsync(current, ct);
            if (current.LeaseId != lease || current.LeaseExpiresAtUtc <= time.GetUtcNow()) throw AppException.Conflict();
            current.GenerationStage = stage;
            return true;
        }, ct);
    }

    private bool Expire(InterviewEvaluationReport row)
    {
        if (row.LeaseId is null || row.LeaseExpiresAtUtc > time.GetUtcNow()) return false;
        row.FailureStage = row.GenerationStage;
        row.GenerationStage = "Failed"; row.FailureCode = "report_interrupted"; row.FinishedAtUtc = time.GetUtcNow();
        row.LeaseId = null; row.LeaseExpiresAtUtc = null;
        return true;
    }

    private InterviewReportGenerationDto Status(InterviewSession session, InterviewEvaluationReport? row, bool available)
    {
        var expired = row?.LeaseId is not null && row.LeaseExpiresAtUtc <= time.GetUtcNow();
        var stage = !available ? session.Status == InterviewStatus.Completed ? "Unavailable" : "WaitingForCompletion" :
            expired ? "Failed" : row?.GenerationStage ?? "Failed";
        var status = stage switch { "Queued" => "Queued", "Generating" or "Validating" or "Saving" => "Processing",
            "Succeeded" => "Succeeded", "Failed" => "Failed", _ => "Unavailable" };
        var message = stage switch {
            "Queued" => "گزارش در صف تولید است.", "Generating" => "گزارش در حال تولید و ارزیابی پاسخ‌ها است.",
            "Validating" => "خروجی گزارش در حال بررسی اعتبار و شواهد است.", "Saving" => "گزارش در حال ذخیره شدن است.",
            "Succeeded" => "گزارش آماده است.", "Failed" => (expired ? "report_interrupted" : row?.FailureCode) switch {
                "report_timeout" => "سرویس تولید گزارش در زمان مجاز پاسخ نداد؛ می‌توانید دوباره درخواست دهید.",
                "report_validation_failed" or "report_invalid_response" => "خروجی تولیدشده معتبر نبود؛ می‌توانید دوباره درخواست دهید.",
                "report_interrupted" => "پردازش گزارش قطع شد؛ می‌توانید دوباره درخواست دهید.",
                "report_not_generated" => "این مصاحبه قدیمی گزارش تولیدشده ندارد؛ می‌توانید درخواست تولید دهید.",
                _ => "تولید گزارش ناموفق بود؛ می‌توانید دوباره درخواست دهید." },
            "WaitingForCompletion" => "تولید گزارش پس از پایان مصاحبه شروع می‌شود.", _ => "گزارش در قرارداد این مصاحبه فعال نیست." };
        return new(session.Id, status, stage, message, available && stage == "Failed", row?.GenerationAttempts ?? 0,
            row?.RequestedAtUtc, row?.StartedAtUtc, expired ? row?.LeaseExpiresAtUtc : row?.FinishedAtUtc,
            expired ? "report_interrupted" : available && row is null ? "report_not_generated" : row?.FailureCode,
            expired ? row?.GenerationStage : row?.FailureStage, row?.ResultJson is not null);
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
