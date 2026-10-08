using System.Diagnostics;
using HireYar.Application.Common;
using HireYar.Application.Billing;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace HireYar.Application.Interviews;

public sealed partial class CandidateInterviewService(IApplicationDbContext db, ICurrentInterviewAccess access, TimeProvider time,
    InterviewOptions options, InterviewPolicy policy, InterviewDecisionValidator validator, IInterviewAudioStorage storage,
    ISpeechToTextService stt, IInterviewDecisionService decisions, QuestionAudioService audio, ILogger<CandidateInterviewService> logger,
    InterviewBillingService billing, IInterviewDiagnostics diagnostics)
{
    private async Task<InterviewSession> Load(Guid id, CancellationToken ct)
    {
        if (access.InterviewId != id) throw AppException.NotFound();
        var s = await db.InterviewSessions.Include(x => x.Answers).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        CheckAccess(s); InterviewTrace.Stage(s.Stage.ToString(), "جلسه و پاسخ‌های ذخیره‌شده دریافت و دسترسی بررسی شد"); return s;
    }
    private void CheckAccess(InterviewSession s)
    {
        if (s.SupersededByInterviewId is not null) throw new AppException(410, "interview_address_changed", "آدرس این مصاحبه تغییر کرده است. لطفاً از دعوت جدید استفاده کنید.");
        if (s.DeletedAtUtc is not null || s.AccessRevokedAtUtc is not null) throw AppException.Unauthorized();
        if (s.ExpiresAtUtc <= time.GetUtcNow()) throw new AppException(410, "interview_expired", "The interview invitation expired.");
    }
    public async Task<CandidateInterviewDto> StartAsync(Guid id, CancellationToken ct)
    {
        var s = await Load(id, ct);
        await billing.StartAsync(s, async () =>
        {
          if (s.Status is InterviewStatus.Completed or InterviewStatus.Cancelled) throw AppException.Conflict();
          if (s.Status == InterviewStatus.Created)
          {
            var first = policy.SelectFallback(s, time.GetUtcNow(), options.ShortlistLimit) ?? throw AppException.Invalid("interview_plan_incomplete");
            s.Start(first.QuestionId, time.GetUtcNow());
            var application = await db.CandidateApplications.SingleAsync(x => x.Id == s.CandidateApplicationId && x.OrganizationId == s.OrganizationId, ct);
            application.Status = CandidateApplicationStatus.Interviewing; application.UpdatedAtUtc = time.GetUtcNow();
            await db.SaveChangesAsync(ct); InterviewMetrics.Started.Add(1);
          }
          return true;
        }, ct);
        return await Map(s, true, ct);
    }
    public async Task<CandidateInterviewDto> CurrentAsync(Guid id, CancellationToken ct)
    {
        var s = await Load(id, ct);
        if (s.Status == InterviewStatus.Completed) throw new AppException(410, "interview_completed", "این مصاحبه به اتمام رسیده است.");
        return await Map(s, false, ct);
    }
    public async Task<CandidateInterviewDto> AdvanceStageAsync(Guid id, AdvanceInterviewStageRequest r, CancellationToken ct)
    {
        if (!Enum.TryParse<InterviewStage>(r.Stage, false, out var stage) || !Enum.IsDefined(stage)) throw AppException.Invalid("invalid_interview_stage");
        var s = await Load(id, ct);
        await db.WithOrganizationBillingLockAsync(s.OrganizationId, async () => {
            await db.ReloadInterviewAsync(s, ct); CheckAccess(s);
            if (s.Status is InterviewStatus.Created or InterviewStatus.Cancelled) throw AppException.Conflict();
            if (stage == InterviewStage.CandidateIntroduction && s.Stage == stage &&
                await db.InterviewIntroductions.AnyAsync(x => x.OrganizationId == s.OrganizationId && x.InterviewSessionId == s.Id, ct))
                throw new AppException(409, "introduction_audio_pending", "Use the introduction audio retry endpoint.");
            s.AdvanceStage(stage, r.Introduction, time.GetUtcNow()); return true;
        }, ct);
        return await Map(s, true, ct);
    }
    public async Task<CandidateInterviewDto> SubmitAsync(Guid id, Guid questionId, Stream stream, string fileName,
        string contentType, long length, int? durationSeconds, CancellationToken ct, AnswerCapture? capture = null)
    {
        var s = await Load(id, ct);
        var turnId = capture?.ClientTurnId;
        if (turnId is not null && (string.IsNullOrWhiteSpace(turnId) || turnId.Length > 128 || turnId != turnId.Trim()))
            throw AppException.Invalid("client_turn_id_invalid");
        if (turnId is not null && await db.InterviewIntroductions.AnyAsync(x => x.OrganizationId == s.OrganizationId && x.InterviewSessionId == s.Id && x.ClientTurnId == turnId, ct))
            throw new AppException(409, "client_turn_id_conflict", "The turn identifier belongs to the introduction.");
        if (turnId is not null && s.Answers.SingleOrDefault(a => a.ClientTurnId == turnId) is { } existing)
        {
            if (existing.QuestionId != questionId) throw new AppException(409, "client_turn_id_conflict", "The turn identifier belongs to another question.");
            return await Map(s, false, ct, existing);
        }
        await billing.EnsureProcessingAllowedAsync(s, ct);
        if (s.Status != InterviewStatus.WaitingForAnswer) throw AppException.Conflict();
        if (s.Stage != InterviewStage.Questions) throw AppException.Conflict();
        if (s.CurrentQuestionId != questionId) throw AppException.Invalid("wrong_question");
        if (s.Answers.Any(x => x.QuestionId == questionId && !x.IsInteraction)) throw AppException.Conflict();
        if (s.RemainingSeconds(time.GetUtcNow()) == 0) throw new AppException(409, "interview_expired", "The interview time limit was reached.");
        var receivedAt = time.GetUtcNow();
        if (capture?.LastSpeechEndUtc is { } ended && (ended.Offset != TimeSpan.Zero || ended > receivedAt.AddSeconds(30) || ended < s.StartedAtUtc?.AddSeconds(-30)))
            throw AppException.Invalid("last_speech_end_invalid");
        await AudioUploadValidator.ValidateAsync(stream, fileName, contentType, length, durationSeconds, options, ct);
        string? videoMime = null;
        if (capture?.Video is { } video)
            videoMime = await VideoUploadValidator.ValidateAsync(video, capture.VideoFileName ?? "", capture.VideoContentType ?? "", capture.VideoLength, options, ct);
        var saved = await storage.SaveAsync(stream, "answers", s.Id, options.MaxUploadBytes, ct);
        StoredAudio? savedVideo = null;
        InterviewAnswer? answer = null; Guid lease = default; bool replay = false;
        try
        {
            if (capture?.Video is { } source)
                savedVideo = await storage.SaveAsync(source, "videos", s.Id, options.MaxVideoUploadBytes, ct);
            await db.WithOrganizationBillingLockAsync(s.OrganizationId, async () =>
            {
                await db.ReloadInterviewAsync(s, ct); CheckAccess(s);
                await db.InterviewAnswers.Where(a => a.InterviewSessionId == s.Id).ToListAsync(ct);
                if (turnId is not null && s.Answers.SingleOrDefault(a => a.ClientTurnId == turnId) is { } committed)
                {
                    if (committed.QuestionId != questionId) throw new AppException(409, "client_turn_id_conflict", "The turn identifier belongs to another question.");
                    answer = committed; replay = true; return true;
                }
                if (s.Status != InterviewStatus.WaitingForAnswer) throw AppException.Conflict();
                await billing.EnsureProcessingAllowedAsync(s, ct);
                answer = s.SubmitAnswer(questionId, saved.Key, contentType.Split(';')[0].Trim().ToLowerInvariant(), saved.SizeBytes, receivedAt);
                answer.SetCaptureMetadata(turnId, capture?.LastSpeechEndUtc, durationSeconds, savedVideo?.Key, videoMime, savedVideo?.SizeBytes);
                lease = s.AcquireLease(answer.Id, time.GetUtcNow(), options.LeaseSeconds);
                return true;
            }, ct);
        }
        catch
        {
            await storage.DeleteAsync(saved.Key, CancellationToken.None);
            if (savedVideo is not null) await storage.DeleteAsync(savedVideo.Key, CancellationToken.None);
            throw;
        }
        if (replay)
        {
            await storage.DeleteAsync(saved.Key, CancellationToken.None);
            if (savedVideo is not null) await storage.DeleteAsync(savedVideo.Key, CancellationToken.None);
            return await Map(s, false, ct, answer);
        }
        return await Process(s, answer!, lease, ct);
    }
    public async Task<CandidateInterviewDto> RetryAsync(Guid id, Guid answerId, CancellationToken ct)
    {
        var s = await Load(id, ct); var answer = s.Answers.SingleOrDefault(x => x.Id == answerId) ?? throw AppException.NotFound();
        if (answer.Status == AnswerStatus.Processed) return await Map(s, false, ct);
        Guid lease = default;
        await db.WithOrganizationBillingLockAsync(s.OrganizationId, async () => {
            await db.ReloadInterviewAsync(s, ct); CheckAccess(s);
            await billing.EnsureProcessingAllowedAsync(s, ct);
            lease = s.AcquireLease(answer.Id, time.GetUtcNow(), options.LeaseSeconds);
            answer.SetDiagnostics(null, null, null); return true;
        }, ct);
        return await Process(s, answer, lease, ct);
    }
    // Called only by the internal worker with an organization/session pair read from persisted state.
    public async Task RecoverAsync(Guid organizationId, Guid id, CancellationToken ct)
    {
        var s = await db.InterviewSessions.Include(x => x.Answers).SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, ct);
        if (s is null) return;
        InterviewAnswer? answer = null; Guid lease = default;
        await db.WithOrganizationBillingLockAsync(organizationId, async () => {
            await db.ReloadInterviewAsync(s, ct);
            var now = time.GetUtcNow();
            if (s.Status != InterviewStatus.ProcessingAnswer || s.DeletedAtUtc is not null || s.AccessRevokedAtUtc is not null || s.ExpiresAtUtc <= now ||
                s.RecoveryAttempts >= options.MaxRecoveryAttempts || s.NextRecoveryAtUtc > now || s.ProcessingLeaseExpiresAtUtc > now) return false;
            if (!await db.InterviewBillings.AnyAsync(x => x.OrganizationId == organizationId && x.InterviewSessionId == id &&
                x.Status == HireYar.Domain.Billing.InterviewBillingStatus.Reserved, ct)) return false;
            await billing.EnsureProcessingAllowedAsync(s, ct);
            answer = s.Answers.SingleOrDefault(a => a.QuestionId == s.CurrentQuestionId && a.Status != AnswerStatus.Processed);
            if (answer is null) return false;
            lease = s.AcquireLease(answer.Id, now, options.LeaseSeconds); s.BeginRecoveryAttempt(now); return true;
        }, ct);
        if (answer is not null) await Process(s, answer, lease, ct);
    }
    private async Task<CandidateInterviewDto> Process(InterviewSession s, InterviewAnswer answer, Guid lease, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var capabilities = await billing.CapabilitiesAsync(s, ct);
            if (answer.Transcript is null)
            {
                InterviewTrace.Stage("Transcribing", "تبدیل صوت پاسخ ذخیره‌شده به متن");
                s.CheckLease(lease, time.GetUtcNow()); s.BeginTranscription(answer.Id); await db.SaveChangesAsync(ct);
                SpeechTranscriptionResult transcript;
                try
                {
                    await using var source = await storage.OpenReadAsync(answer.AudioStorageKey, ct);
                    transcript = await stt.TranscribeAsync(source, "answer" + AudioUploadValidator.Extension(answer.AudioContentType), answer.AudioContentType, ct);
                    if (string.IsNullOrWhiteSpace(transcript.Text) || transcript.Text.Length > 32000) throw new InterviewProviderException("stt_invalid_response");
                }
                catch (Exception e) when (e is InterviewProviderException or IOException or UnauthorizedAccessException)
                {
                    InterviewTrace.Emit("processing", "transcription", "failed", "تبدیل صوت پاسخ به متن ناموفق بود", failureCode: e is InterviewProviderException failure ? failure.Code : "audio_unavailable");
                    await db.ReloadInterviewAsync(s, ct); CheckAccess(s); s.CheckLease(lease, time.GetUtcNow());
                    s.FailAnswer(answer.Id); answer.SetDiagnostics("Transcribing", e is InterviewProviderException p ? p.Code : "audio_unavailable", null);
                    s.ScheduleRecovery(time.GetUtcNow(), 30 * (1 << Math.Min(s.RecoveryAttempts, 5)));
                    s.ReleaseLease(lease, time.GetUtcNow()); await db.SaveChangesAsync(ct);
                    InterviewTrace.Emit("recovery", "answer", "scheduled", "وضعیت تلاش مجدد پاسخ در دیتابیس ذخیره شد");
                    logger.LogWarning("Interview answer retry available interviewId={InterviewId} answerId={AnswerId} failureCode={FailureCode}", s.Id, answer.Id, answer.FailureCode);
                    return await Map(s, false, ct);
                }
                await db.ReloadInterviewAsync(s, ct); CheckAccess(s); s.CheckLease(lease, time.GetUtcNow());
                s.SetTranscript(answer.Id, transcript.Text, transcript.Provider, transcript.Model); await db.SaveChangesAsync(ct);
            }
            else InterviewTrace.Emit("processing", "transcription", "skipped", "متن پاسخ قبلاً ذخیره شده است؛ تماس STT تکرار نمی‌شود");
            InterviewTrace.Stage("PreparingContext", "آماده‌سازی شرح شغل، snapshot رزومه، سؤال‌های مجاز و تاریخچه منتخب برای ارزیابی");
            s.CheckLease(lease, time.GetUtcNow()); s.BeginEvaluation(answer.Id); await db.SaveChangesAsync(ct);
            var shortlist = policy.GetAllowedQuestions(s, time.GetUtcNow(), options.ShortlistLimit, adaptDifficulty: capabilities.Contains("adaptive_difficulty"));
            var position = await db.JobPositions.AsNoTracking().SingleAsync(x => x.Id == s.JobPositionId && x.OrganizationId == s.OrganizationId, ct);
            var question = s.Questions.Single(x => x.QuestionId == answer.QuestionId);
            var history = InterviewHistorySelector.Select(s, answer, options);
            InterviewMetrics.HistoryTotalTurns.Record(history.TotalTurns);
            InterviewMetrics.HistorySelectedTurns.Record(history.Turns.Count);
            InterviewDecisionResult? decision = null; string? failureCode = null;
            try
            {
                InterviewTrace.Stage("Evaluating", "بررسی کفایت پاسخ، ارزیابی مهارت و تصمیم درباره ادامه همین سؤال یا سؤال بعدی");
                if (capabilities.Contains("answer_analysis") || capabilities.Contains("adaptive_questions"))
                  decision = await decisions.EvaluateAsync(new(s.PositionTitleSnapshot ?? position.Title, question.QuestionId, s.Status.ToString(), s.AnsweredQuestionCount,
                    s.MaxQuestionCount, s.RemainingSeconds(time.GetUtcNow()), question, answer.Transcript!, s.Skills, s.SkillStates, s.CoveredTopics, shortlist,
                    s.PositionDescriptionSnapshot ?? position.Description, s.ResumeTextSnapshot, s.CandidateIntroductionTranscript,
                    s.RequiredCapabilityCode == "hr_interview" ? "general" : "specialized", s.Language,
                    shortlist.Append(question).DistinctBy(q => q.QuestionId).ToArray(), history.Turns,
                    s.MasterPromptSnapshot ?? InterviewDecisionPrompt.Master, s.RemainingInteractions, history.TotalTurns, history.IsPartial,
                    s.Evidence.Select(e => new HrEvidenceDto(e.InterviewAnswerId, e.QuestionId, e.SkillId, e.ScoreBefore, e.ScoreAfter,
                        e.Delta, e.Confidence, e.Evidence, e.CreatedAtUtc)).ToArray()), ct);
                else InterviewTrace.Emit("provider", "llm", "skipped", "قابلیت ارزیابی AI در قرارداد این جلسه فعال نیست؛ تصمیم بک‌اند اجرا می‌شود");
            }
            catch (InterviewProviderException e)
            {
                failureCode = e.Code;
                InterviewTrace.Emit("processing", "evaluation", "failed", "ارزیابی پاسخ ناموفق بود", failureCode: e.Code);
                if (options.BackgroundRecoveryEnabled && s.RecoveryAttempts < options.MaxRecoveryAttempts)
                {
                    await db.ReloadInterviewAsync(s, ct); CheckAccess(s); s.CheckLease(lease, time.GetUtcNow());
                    s.FailAnswer(answer.Id); answer.SetDiagnostics("Evaluating", e.Code, null);
                    s.ScheduleRecovery(time.GetUtcNow(), 30 * (1 << Math.Min(s.RecoveryAttempts, 5)));
                    s.ReleaseLease(lease, time.GetUtcNow()); await db.SaveChangesAsync(ct);
                    return await Map(s, false, ct);
                }
            }
            await db.ReloadInterviewAsync(s, ct); CheckAccess(s); s.CheckLease(lease, time.GetUtcNow());
            InterviewTrace.Stage("ValidatingDecision", "اعتبارسنجی خروجی مدل با سیاست سرور و انتخاب fallback در صورت نیاز");
            var result = validator.ValidateOrFallback(decision?.Json, s, shortlist.Select(x => x.QuestionId).ToArray(), time.GetUtcNow());
            InterviewTrace.Emit("decision", "validate", result.UsedFallback ? "fallback" : "succeeded", result.UsedFallback ? "ادامه با تصمیم جایگزین بک‌اند" : "تصمیم مدل معتبر است");
            if (result.Action != "advance")
            {
                s.ContinueQuestion(answer.Id, result.CandidateMessage!, decision!.Model, s.MasterPromptVersion ?? InterviewDecisionPrompt.Version, time.GetUtcNow());
                s.ReleaseLease(lease, time.GetUtcNow()); await db.SaveChangesAsync(ct);
                return await Map(s, true, ct);
            }
            s.FinishAnswer(answer.Id, capabilities.Contains("answer_analysis") ? result.SkillUpdates : [],
                capabilities.Contains("avoid_repeated_topics") ? result.CoveredTopics : [], decision?.Model ?? "backend-fallback", s.MasterPromptVersion ?? InterviewDecisionPrompt.Version, time.GetUtcNow());
            var sourceName = result.UsedFallback ? "Fallback" : "AI";
            answer.SetDiagnostics(result.UsedFallback ? "Evaluating" : null, result.UsedFallback ? failureCode ?? "decision_invalid" : null, sourceName);
            var completion = policy.GetCompletionReason(s, time.GetUtcNow());
            if (completion is { } reason)
            {
                s.Complete(reason, time.GetUtcNow());
                var application = await db.CandidateApplications.SingleAsync(x => x.Id == s.CandidateApplicationId && x.OrganizationId == s.OrganizationId, ct);
                application.Status = CandidateApplicationStatus.InterviewCompleted; application.UpdatedAtUtc = time.GetUtcNow();
            }
            else
            {
                // Evidence may have covered a topic; enforce policy again before advancement.
                var finalAllowed = policy.GetAllowedQuestions(s, time.GetUtcNow(), options.ShortlistLimit, adaptDifficulty: capabilities.Contains("adaptive_difficulty"));
                var proposed = capabilities.Contains("adaptive_questions") ? finalAllowed.FirstOrDefault(x => x.QuestionId == result.NextQuestionId) : null;
                var next = proposed ?? finalAllowed.First();
                if (proposed is null && !result.UsedFallback)
                {
                    sourceName = "Fallback";
                    answer.SetDiagnostics("SelectingNextQuestion", "policy_reselection", sourceName);
                }
                s.AskNext(next.QuestionId, time.GetUtcNow());
            }
            InterviewTrace.Stage("SavingResult", "ذخیره نتیجه پاسخ و وضعیت سؤال بعدی یا پایان مصاحبه");
            s.ReleaseLease(lease, time.GetUtcNow());
            if (s.Status == InterviewStatus.Completed) await billing.FinishAsync(s, false, ct);
            else await db.SaveChangesAsync(ct);
            if (s.Status == InterviewStatus.Completed) InterviewMetrics.Completed.Add(1);
            InterviewMetrics.Selection.Add(1, new KeyValuePair<string, object?>("source", sourceName));
            logger.LogInformation("Interview question selection interviewId={InterviewId} answerId={AnswerId} selectionSource={SelectionSource} failureCode={FailureCode}", s.Id, answer.Id, sourceName, answer.FailureCode);
            return await Map(s, true, ct);
        }
        finally { InterviewMetrics.ProcessingDuration.Record(watch.Elapsed.TotalMilliseconds); }
    }
    public async Task<CandidateInterviewDto> CompleteAsync(Guid id, CancellationToken ct)
    {
        var s = await Load(id, ct);
        await billing.FinishAsync(s, false, async () => {
            await db.ReloadInterviewAsync(s, ct);
            if (s.Status == InterviewStatus.Completed) return true;
            if (s.Status != InterviewStatus.WaitingForAnswer) throw AppException.Conflict();
            if (s.Stage != InterviewStage.Questions) throw AppException.Conflict();
            s.Complete(InterviewCompletionReason.Manual, time.GetUtcNow());
            var application = await db.CandidateApplications.SingleAsync(x => x.Id == s.CandidateApplicationId && x.OrganizationId == s.OrganizationId, ct);
            application.Status = CandidateApplicationStatus.InterviewCompleted; application.UpdatedAtUtc = time.GetUtcNow(); return true;
        }, ct);
        InterviewMetrics.Completed.Add(1); return await Map(s, false, ct);
    }
    public async Task<Stream> OpenQuestionAudioAsync(Guid id, Guid questionId, CancellationToken ct)
    {
        var s = await Load(id, ct);
        await billing.RequireMediaRetentionAsync(s, ct);
        if (s.CurrentQuestionId != questionId && !s.Answers.Any(x => x.QuestionId == questionId)) throw AppException.NotFound();
        var q = s.Questions.SingleOrDefault(x => x.QuestionId == questionId) ?? throw AppException.NotFound();
        var cached = await audio.ResolveAsync(s.OrganizationId, s.CurrentQuestionId == questionId ? s.CurrentInteractionMessage ?? q.Text : q.Text, false, ct) ?? throw AppException.NotFound();
        return await storage.OpenReadAsync(cached.AudioStorageKey, ct);
    }
    public async Task<IReadOnlyList<CandidateMessageDto>> MessagesAsync(Guid id, CancellationToken ct)
    {
        var s = await Load(id, ct);
        var messages = new List<CandidateMessageDto>();
        var introduction = await db.InterviewIntroductions.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == s.OrganizationId && x.InterviewSessionId == id, ct);
        if (introduction is not null)
        {
            messages.Add(new($"introduction-prompt:{s.Id}", null, null, "assistant", s.CandidateIntroductionMessage, introduction.SubmittedAtUtc, Stage: "CandidateIntroduction"));
            messages.Add(new($"introduction:{introduction.Id}", null, null, "candidate", introduction.Transcript, introduction.SubmittedAtUtc, introduction.Status.ToString(), "CandidateIntroduction"));
        }
        else if (s.CandidateIntroductionTranscript is { } legacyIntroduction)
            messages.Add(new($"introduction:{s.Id}", null, null, "candidate", legacyIntroduction, s.StageChangedAtUtc ?? s.StartedAtUtc ?? s.CreatedAtUtc, "Processed", "CandidateIntroduction"));
        foreach (var q in s.Questions.Where(q => q.AskedAtUtc is not null || s.Answers.Any(a => a.QuestionId == q.QuestionId) || s.Stage == InterviewStage.Questions && s.CurrentQuestionId == q.QuestionId))
        {
            // Legacy sessions have no persisted question timestamp; expose a server-time fallback.
            var submitted = s.Answers.Where(a => a.QuestionId == q.QuestionId).OrderBy(a => a.SubmittedAtUtc).FirstOrDefault();
            var askedAt = q.AskedAtUtc ?? submitted?.SubmittedAtUtc ?? s.StartedAtUtc ?? s.CreatedAtUtc;
            messages.Add(new($"question:{q.QuestionId}", q.QuestionId, null, "assistant", q.Text, askedAt));
            foreach (var a in s.Answers.Where(a => a.QuestionId == q.QuestionId))
            {
                messages.Add(new($"answer:{a.Id}", q.QuestionId, a.Id, "candidate", a.Transcript, a.SubmittedAtUtc, a.Status.ToString()));
                if (a.AssistantMessage is { } reply)
                    messages.Add(new($"interaction:{a.Id}", q.QuestionId, a.Id, "assistant", reply, a.ProcessedAtUtc!.Value));
            }
        }
        return messages.OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Role == "assistant" ? 0 : 1).ThenBy(m => m.Id).ToArray();
    }
    private async Task<CandidateInterviewDto> Map(InterviewSession s, bool generateAudio, CancellationToken ct, InterviewAnswer? resultAnswer = null)
    {
        var introduction = await db.InterviewIntroductions.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == s.OrganizationId && x.InterviewSessionId == s.Id, ct);
        var introPending = IntroductionRecoveryPending(s, introduction);
        var introCanRetry = introduction is not null && s.Stage == InterviewStage.CandidateIntroduction && s.RemainingSeconds(time.GetUtcNow()) > 0 &&
            introduction.Status != IntroductionStatus.Processed && !introPending &&
            (introduction.ProcessingLeaseExpiresAtUtc is null || introduction.ProcessingLeaseExpiresAtUtc <= time.GetUtcNow());
        CandidateQuestionDto? q = null;
        if (s.Stage == InterviewStage.Questions && s.CurrentQuestionId is { } id)
        {
            var snapshot = s.Questions.Single(x => x.QuestionId == id);
            var sound = await audio.ResolveAsync(s.OrganizationId, s.CurrentInteractionMessage ?? snapshot.Text, generateAudio, ct);
            if (generateAudio && s.Status == InterviewStatus.WaitingForAnswer && s.QuestionReadyAtUtc is null)
            { s.MarkQuestionReady(time.GetUtcNow()); await db.SaveChangesAsync(ct); }
            q = new(id, snapshot.Text, sound is null ? null : $"/api/candidate/interviews/{s.Id}/questions/{id}/audio", sound is not null, s.CurrentInteractionMessage);
        }
        var a = resultAnswer ?? s.Answers.Where(x => x.QuestionId == s.CurrentQuestionId).OrderByDescending(x => x.SubmittedAtUtc).ThenByDescending(x => x.Id).FirstOrDefault()
            ?? s.Answers.OrderByDescending(x => x.SubmittedAtUtc).FirstOrDefault();
        var processingAllowed = s.Status != InterviewStatus.ProcessingAnswer || await db.InterviewBillings.AsNoTracking().AnyAsync(x =>
            x.OrganizationId == s.OrganizationId && x.InterviewSessionId == s.Id && x.Status == HireYar.Domain.Billing.InterviewBillingStatus.Reserved, ct);
        var leaseActive = s.ProcessingLeaseExpiresAtUtc > time.GetUtcNow();
        var canRetry = processingAllowed && a is not null && a.Status != AnswerStatus.Processed && a.QuestionId == s.CurrentQuestionId && s.Status == InterviewStatus.ProcessingAnswer &&
            (s.ProcessingLeaseId is null || s.ProcessingLeaseExpiresAtUtc <= time.GetUtcNow());
        var answerPending = processingAllowed && s.Status == InterviewStatus.ProcessingAnswer && options.BackgroundRecoveryEnabled &&
            (s.RecoveryAttempts < options.MaxRecoveryAttempts || s.ProcessingLeaseExpiresAtUtc > time.GetUtcNow());
        var stageMessage = StageText(s);
        var processingState = s.Status != InterviewStatus.ProcessingAnswer ? "idle" : !processingAllowed ? "unavailable" :
            leaseActive ? (a?.Status == AnswerStatus.Evaluating ? "evaluating" : "transcribing") : answerPending ? "retryScheduled" : "retryAvailable";
        var processingMessage = processingState switch {
            "unavailable" => "امکان پردازش این مصاحبه دیگر وجود ندارد؛ پاسخ شما ذخیره شده است. برای دعوت مجدد با سازمان تماس بگیرید.",
            "retryScheduled" => "پردازش پاسخ با خطا مواجه شد؛ تلاش مجدد به صورت خودکار انجام می‌شود.",
            "retryAvailable" => "پردازش پاسخ انجام نشد. پاسخ ذخیره شده است؛ لطفاً دوباره تلاش کنید.",
            _ => (string?)null
        };
        return new(s.Id, s.Status.ToString(), (s.Stage == InterviewStage.Questions || s.Stage == InterviewStage.CandidateIntroduction && introduction is null) && s.Status == InterviewStatus.WaitingForAnswer && s.RemainingSeconds(time.GetUtcNow()) > 0,
            processingAllowed && s.Status == InterviewStatus.ProcessingAnswer && (!canRetry || answerPending) || s.Stage == InterviewStage.CandidateIntroduction &&
                (introPending || introduction?.ProcessingLeaseExpiresAtUtc > time.GetUtcNow()), q,
            new(s.AnsweredQuestionCount, s.MaxQuestionCount, s.RemainingSeconds(time.GetUtcNow())),
            a is null ? null : new(a.Id, a.QuestionId, a.Status.ToString(), canRetry, a.Status == AnswerStatus.Failed ? "answer_retry_available" : null,
                a.Transcript, a.ClientTurnId, a.SubmittedAtUtc, a.LastSpeechEndUtc, a.DurationSeconds, a.VideoStorageKey is not null, a.FailureStage, a.FailureCode),
            s.CompletionReason?.ToString(), s.WelcomeMessage, s.Status == InterviewStatus.Completed ? s.GoodbyeMessage : null,
            new(true, options.MaxUploadBytes, options.MaxVideoUploadBytes, options.MaxAnswerSeconds), s.Stage.ToString(),
            stageMessage,
            s.CandidateIntroductionEnabled, s.CompanyIntroductionEnabled, s.Stage == InterviewStage.CandidateIntroduction ? introduction?.RecoveryAttempts ?? 0 : s.RecoveryAttempts,
            introPending || answerPending,
            s.Status is not (InterviewStatus.Created or InterviewStatus.Cancelled) && !string.IsNullOrWhiteSpace(stageMessage) ? $"/api/candidate/interviews/{s.Id}/stages/{s.Stage}/audio" : null,
            introduction is null ? null : new(introduction.Id, introduction.ClientTurnId, introduction.Status.ToString(), introduction.Transcript,
                introCanRetry, introduction.FailureCode, introduction.RecoveryAttempts, introPending, introduction.SubmittedAtUtc,
                introduction.LastSpeechEndUtc, introduction.DurationSeconds, introduction.VideoStorageKey is not null),
            processingState, processingMessage, answerPending ? s.NextRecoveryAtUtc : null,
            diagnostics.Enabled, diagnostics.Enabled ? $"/api/candidate/interviews/{s.Id}/diagnostics" : null);
    }
}
