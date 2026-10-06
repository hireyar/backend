using System.Diagnostics;
using HireYar.Application.Common;
using HireYar.Application.Billing;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace HireYar.Application.Interviews;

public sealed class CandidateInterviewService(IApplicationDbContext db, ICurrentInterviewAccess access, TimeProvider time,
    InterviewOptions options, InterviewPolicy policy, InterviewDecisionValidator validator, IInterviewAudioStorage storage,
    ISpeechToTextService stt, IInterviewDecisionService decisions, QuestionAudioService audio, ILogger<CandidateInterviewService> logger,
    InterviewBillingService billing)
{
    private async Task<InterviewSession> Load(Guid id, CancellationToken ct)
    {
        if (access.InterviewId != id) throw AppException.NotFound();
        var s = await db.InterviewSessions.Include(x => x.Answers).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        CheckAccess(s); return s;
    }
    private void CheckAccess(InterviewSession s)
    {
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
    public async Task<CandidateInterviewDto> CurrentAsync(Guid id, CancellationToken ct) => await Map(await Load(id, ct), false, ct);
    public async Task<CandidateInterviewDto> SubmitAsync(Guid id, Guid questionId, Stream stream, string fileName,
        string contentType, long length, int? durationSeconds, CancellationToken ct, AnswerCapture? capture = null)
    {
        var s = await Load(id, ct);
        var turnId = capture?.ClientTurnId;
        if (turnId is not null && (string.IsNullOrWhiteSpace(turnId) || turnId.Length > 128 || turnId != turnId.Trim()))
            throw AppException.Invalid("client_turn_id_invalid");
        if (turnId is not null && s.Answers.SingleOrDefault(a => a.ClientTurnId == turnId) is { } existing)
        {
            if (existing.QuestionId != questionId) throw new AppException(409, "client_turn_id_conflict", "The turn identifier belongs to another question.");
            return await Map(s, false, ct, existing);
        }
        await billing.EnsureProcessingAllowedAsync(s, ct);
        if (s.Status != InterviewStatus.WaitingForAnswer) throw AppException.Conflict();
        if (s.CurrentQuestionId != questionId) throw AppException.Invalid("wrong_question");
        if (s.Answers.Any(x => x.QuestionId == questionId)) throw AppException.Conflict();
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
        await billing.EnsureProcessingAllowedAsync(s, ct);
        var lease = s.AcquireLease(answer.Id, time.GetUtcNow(), options.LeaseSeconds);
        answer.SetDiagnostics(null, null, null); await db.SaveChangesAsync(ct);
        return await Process(s, answer, lease, ct);
    }
    private async Task<CandidateInterviewDto> Process(InterviewSession s, InterviewAnswer answer, Guid lease, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var capabilities = await billing.CapabilitiesAsync(s, ct);
            if (answer.Transcript is null)
            {
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
                    await db.ReloadInterviewAsync(s, ct); CheckAccess(s); s.CheckLease(lease, time.GetUtcNow());
                    s.FailAnswer(answer.Id); answer.SetDiagnostics("Transcribing", e is InterviewProviderException p ? p.Code : "audio_unavailable", null);
                    s.ReleaseLease(lease, time.GetUtcNow()); await db.SaveChangesAsync(ct);
                    logger.LogWarning("Interview answer retry available interviewId={InterviewId} answerId={AnswerId} failureCode={FailureCode}", s.Id, answer.Id, answer.FailureCode);
                    return await Map(s, false, ct);
                }
                await db.ReloadInterviewAsync(s, ct); CheckAccess(s); s.CheckLease(lease, time.GetUtcNow());
                s.SetTranscript(answer.Id, transcript.Text, transcript.Provider, transcript.Model); await db.SaveChangesAsync(ct);
            }
            s.CheckLease(lease, time.GetUtcNow()); s.BeginEvaluation(answer.Id); await db.SaveChangesAsync(ct);
            var shortlist = policy.GetAllowedQuestions(s, time.GetUtcNow(), options.ShortlistLimit, adaptDifficulty: capabilities.Contains("adaptive_difficulty"));
            var position = await db.JobPositions.AsNoTracking().SingleAsync(x => x.Id == s.JobPositionId && x.OrganizationId == s.OrganizationId, ct);
            var question = s.Questions.Single(x => x.QuestionId == answer.QuestionId);
            InterviewDecisionResult? decision = null; string? failureCode = null;
            try
            {
                if (capabilities.Contains("answer_analysis") || capabilities.Contains("adaptive_questions"))
                  decision = await decisions.EvaluateAsync(new(s.PositionTitleSnapshot ?? position.Title, question.QuestionId, s.Status.ToString(), s.AnsweredQuestionCount,
                    s.MaxQuestionCount, s.RemainingSeconds(time.GetUtcNow()), question, answer.Transcript!, s.Skills, s.SkillStates, s.CoveredTopics, shortlist,
                    s.PositionDescriptionSnapshot ?? position.Description), ct);
            }
            catch (InterviewProviderException e) { failureCode = e.Code; }
            await db.ReloadInterviewAsync(s, ct); CheckAccess(s); s.CheckLease(lease, time.GetUtcNow());
            var result = validator.ValidateOrFallback(decision?.Json, s, shortlist.Select(x => x.QuestionId).ToArray(), time.GetUtcNow());
            s.FinishAnswer(answer.Id, capabilities.Contains("answer_analysis") ? result.SkillUpdates : [],
                capabilities.Contains("avoid_repeated_topics") ? result.CoveredTopics : [], decision?.Model ?? "backend-fallback", InterviewDecisionPrompt.Version, time.GetUtcNow());
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
        var cached = await audio.ResolveAsync(s.OrganizationId, q.Text, false, ct) ?? throw AppException.NotFound();
        return await storage.OpenReadAsync(cached.AudioStorageKey, ct);
    }
    public async Task<IReadOnlyList<CandidateMessageDto>> MessagesAsync(Guid id, CancellationToken ct)
    {
        var s = await Load(id, ct);
        var messages = new List<CandidateMessageDto>();
        foreach (var q in s.Questions.Where(q => q.AskedAtUtc is not null || s.Answers.Any(a => a.QuestionId == q.QuestionId) || s.CurrentQuestionId == q.QuestionId))
        {
            // Legacy sessions have no persisted question timestamp; expose a server-time fallback.
            var submitted = s.Answers.SingleOrDefault(a => a.QuestionId == q.QuestionId);
            var askedAt = q.AskedAtUtc ?? submitted?.SubmittedAtUtc ?? s.StartedAtUtc ?? s.CreatedAtUtc;
            messages.Add(new($"question:{q.QuestionId}", q.QuestionId, null, "assistant", q.Text, askedAt));
            if (submitted is { } a)
                messages.Add(new($"answer:{a.Id}", q.QuestionId, a.Id, "candidate", a.Transcript, a.SubmittedAtUtc, a.Status.ToString()));
        }
        return messages.OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Role == "assistant" ? 0 : 1).ThenBy(m => m.Id).ToArray();
    }
    private async Task<CandidateInterviewDto> Map(InterviewSession s, bool generateAudio, CancellationToken ct, InterviewAnswer? resultAnswer = null)
    {
        CandidateQuestionDto? q = null;
        if (s.CurrentQuestionId is { } id)
        {
            var snapshot = s.Questions.Single(x => x.QuestionId == id);
            var sound = await audio.ResolveAsync(s.OrganizationId, snapshot.Text, generateAudio, ct);
            if (generateAudio && s.Status == InterviewStatus.WaitingForAnswer && s.QuestionReadyAtUtc is null)
            { s.MarkQuestionReady(time.GetUtcNow()); await db.SaveChangesAsync(ct); }
            q = new(id, snapshot.Text, sound is null ? null : $"/api/candidate/interviews/{s.Id}/questions/{id}/audio", sound is not null);
        }
        var a = resultAnswer ?? s.Answers.SingleOrDefault(x => x.QuestionId == s.CurrentQuestionId)
            ?? s.Answers.OrderByDescending(x => x.SubmittedAtUtc).FirstOrDefault();
        var canRetry = a is not null && a.Status != AnswerStatus.Processed && a.QuestionId == s.CurrentQuestionId && s.Status == InterviewStatus.ProcessingAnswer &&
            (s.ProcessingLeaseId is null || s.ProcessingLeaseExpiresAtUtc <= time.GetUtcNow());
        return new(s.Id, s.Status.ToString(), s.Status == InterviewStatus.WaitingForAnswer && s.RemainingSeconds(time.GetUtcNow()) > 0,
            s.Status == InterviewStatus.ProcessingAnswer && !canRetry, q,
            new(s.AnsweredQuestionCount, s.MaxQuestionCount, s.RemainingSeconds(time.GetUtcNow())),
            a is null ? null : new(a.Id, a.QuestionId, a.Status.ToString(), canRetry, a.Status == AnswerStatus.Failed ? "answer_retry_available" : null,
                a.Transcript, a.ClientTurnId, a.SubmittedAtUtc, a.LastSpeechEndUtc, a.DurationSeconds, a.VideoStorageKey is not null),
            s.CompletionReason?.ToString(), s.WelcomeMessage, s.GoodbyeMessage,
            new(true, options.MaxUploadBytes, options.MaxVideoUploadBytes, options.MaxAnswerSeconds));
    }
}
