using HireYar.Application.Common;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Interviews;

public sealed partial class CandidateInterviewService
{
    private static string? StageText(InterviewSession s) => s.Stage switch {
        InterviewStage.Welcome => s.WelcomeMessage,
        InterviewStage.CandidateIntroduction => s.CandidateIntroductionMessage,
        InterviewStage.CompanyIntroduction => s.CompanyIntroductionMessage,
        InterviewStage.Goodbye => s.GoodbyeMessage,
        _ => null
    };
    private bool IntroductionRecoveryPending(InterviewSession s, InterviewIntroduction? introduction) =>
        s.Stage == InterviewStage.CandidateIntroduction && introduction is not null && introduction.Status != IntroductionStatus.Processed &&
        s.RemainingSeconds(time.GetUtcNow()) > 0 && options.BackgroundRecoveryEnabled &&
        (introduction.RecoveryAttempts < options.MaxRecoveryAttempts || introduction.ProcessingLeaseExpiresAtUtc > time.GetUtcNow());

    public async Task<Stream> OpenStageAudioAsync(Guid id, string stageName, CancellationToken ct)
    {
        var s = await Load(id, ct);

        // Only the current stage is readable; company conditions cannot be fetched early.
        if (s.Status is InterviewStatus.Created or InterviewStatus.Cancelled || s.Stage.ToString() != stageName ||
            StageText(s) is not { Length: > 0 } text) throw AppException.NotFound();
        var sound = await audio.ResolveAsync(s.OrganizationId, text, true, ct);
        await db.ReloadInterviewAsync(s, ct); CheckAccess(s);
        if (s.Stage.ToString() != stageName || s.Status == InterviewStatus.Cancelled) throw AppException.NotFound();
        if (sound is null) throw new AppException(503, "stage_audio_unavailable", "Stage audio is temporarily unavailable.");
        return await storage.OpenReadAsync(sound.AudioStorageKey, ct);
    }

    public async Task<CandidateInterviewDto> SubmitIntroductionAsync(Guid id, Stream source, string filename, string mime,
        long length, int? durationSeconds, AnswerCapture capture, CancellationToken ct)
    {
        var s = await Load(id, ct);
        var turnId = capture.ClientTurnId;
        if (string.IsNullOrWhiteSpace(turnId) || turnId.Length > 128 || turnId != turnId.Trim()) throw AppException.Invalid("client_turn_id_invalid");
        var existing = await db.InterviewIntroductions.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == s.OrganizationId && x.InterviewSessionId == id, ct);
        if (existing is not null)
        {
            if (existing.ClientTurnId != turnId) throw new AppException(409, "introduction_already_submitted", "Retry the stored introduction without uploading again.");
            return await Map(s, false, ct);
        }
        RequireIntroductionStage(s);

        var receivedAt = time.GetUtcNow();
        if (capture.LastSpeechEndUtc is { } ended && (ended.Offset != TimeSpan.Zero || ended > receivedAt.AddSeconds(30) || ended < s.StageChangedAtUtc?.AddSeconds(-30)))
            throw AppException.Invalid("last_speech_end_invalid");
        await AudioUploadValidator.ValidateAsync(source, filename, mime, length, durationSeconds, options, ct);
        string? videoMime = null;
        if (capture.Video is { } video)
            videoMime = await VideoUploadValidator.ValidateAsync(video, capture.VideoFileName ?? "", capture.VideoContentType ?? "", capture.VideoLength, options, ct);
        var saved = await storage.SaveAsync(source, "answers", id, options.MaxUploadBytes, ct);
        StoredAudio? savedVideo = null;
        InterviewIntroduction? introduction = null; Guid lease = default; bool replay = false;
        try
        {
            if (capture.Video is { } videoSource) savedVideo = await storage.SaveAsync(videoSource, "videos", id, options.MaxVideoUploadBytes, ct);
            await db.WithOrganizationLockAsync(s.OrganizationId, async () => {
                await db.ReloadInterviewAsync(s, ct); CheckAccess(s);
                introduction = await db.InterviewIntroductions.SingleOrDefaultAsync(x => x.OrganizationId == s.OrganizationId && x.InterviewSessionId == id, ct);
                if (introduction is not null)
                {
                    if (introduction.ClientTurnId != turnId) throw new AppException(409, "introduction_already_submitted", "Retry the stored introduction.");
                    replay = true; return true;
                }
                RequireIntroductionStage(s);
                if (await db.InterviewAnswers.AnyAsync(x => x.InterviewSessionId == id && x.ClientTurnId == turnId, ct))
                    throw new AppException(409, "client_turn_id_conflict", "The turn identifier belongs to a question.");
                introduction = new(s.OrganizationId, id, turnId, saved.Key, mime.Split(';')[0].Trim().ToLowerInvariant(), saved.SizeBytes,
                    receivedAt, durationSeconds, capture.LastSpeechEndUtc, savedVideo?.Key, videoMime, savedVideo?.SizeBytes);
                db.InterviewIntroductions.Add(introduction);
                lease = introduction.Claim(time.GetUtcNow(), options.LeaseSeconds, false);
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
            return await Map(s, false, ct);
        }
        await ProcessIntroduction(s, introduction!, lease, ct);
        return await Map(s, true, ct);
    }

    private void RequireIntroductionStage(InterviewSession s)
    {
        if (s.Stage != InterviewStage.CandidateIntroduction || s.Status != InterviewStatus.WaitingForAnswer || !s.CandidateIntroductionEnabled)
            throw AppException.Conflict();
        if (s.RemainingSeconds(time.GetUtcNow()) == 0) throw new AppException(409, "interview_expired", "The interview time limit was reached.");
    }

    public async Task<CandidateInterviewDto> RetryIntroductionAsync(Guid id, Guid introductionId, CancellationToken ct)
    {
        var s = await Load(id, ct);
        var introduction = await db.InterviewIntroductions.SingleOrDefaultAsync(x => x.OrganizationId == s.OrganizationId && x.InterviewSessionId == id && x.Id == introductionId, ct)
            ?? throw AppException.NotFound();
        Guid lease = default;
        await db.WithOrganizationLockAsync(s.OrganizationId, async () => {
            await db.ReloadInterviewAsync(s, ct); await db.ReloadIntroductionAsync(introduction, ct); CheckAccess(s);
            if (introduction.Status == IntroductionStatus.Processed) return false;
            RequireIntroductionStage(s);
            if (IntroductionRecoveryPending(s, introduction)) throw new AppException(409, "background_retry_pending", "Background recovery is pending.");
            lease = introduction.Claim(time.GetUtcNow(), options.LeaseSeconds, false); return true;
        }, ct);
        if (lease != Guid.Empty) await ProcessIntroduction(s, introduction, lease, ct);
        return await Map(s, lease != Guid.Empty, ct);
    }

    // The worker supplies the organization/session pair from persisted state, never HttpContext.
    public async Task RecoverIntroductionAsync(Guid organizationId, Guid id, CancellationToken ct)
    {
        var s = await db.InterviewSessions.Include(x => x.Answers).SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, ct);
        if (s is null) return;
        var introduction = await db.InterviewIntroductions.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.InterviewSessionId == id, ct);
        if (introduction is null) return;
        Guid lease = default;
        await db.WithOrganizationLockAsync(organizationId, async () => {
            await db.ReloadInterviewAsync(s, ct); await db.ReloadIntroductionAsync(introduction, ct);
            var now = time.GetUtcNow();
            if (s.Stage != InterviewStage.CandidateIntroduction || s.Status != InterviewStatus.WaitingForAnswer || s.DeletedAtUtc is not null ||
                s.AccessRevokedAtUtc is not null || s.ExpiresAtUtc <= now || s.RemainingSeconds(now) == 0 ||
                introduction.Status == IntroductionStatus.Processed || introduction.RecoveryAttempts >= options.MaxRecoveryAttempts ||
                introduction.NextRecoveryAtUtc > now || introduction.ProcessingLeaseExpiresAtUtc > now) return false;

            lease = introduction.Claim(now, options.LeaseSeconds, true); return true;
        }, ct);
        if (lease != Guid.Empty)
        {
            await ProcessIntroduction(s, introduction, lease, ct);
            await Map(s, true, ct);
        }
    }

    private async Task ProcessIntroduction(InterviewSession s, InterviewIntroduction introduction, Guid lease, CancellationToken ct)
    {
        InterviewTrace.Stage("TranscribingIntroduction", "تبدیل معرفی صوتی کارجو به متن؛ بدون ارزیابی مهارت یا LLM");
        SpeechTranscriptionResult? transcript = null; string? failure = null;
        try
        {
            await using var file = await storage.OpenReadAsync(introduction.AudioStorageKey, ct);
            transcript = await stt.TranscribeAsync(file, "introduction" + AudioUploadValidator.Extension(introduction.AudioContentType), introduction.AudioContentType, ct);
            if (string.IsNullOrWhiteSpace(transcript.Text) || transcript.Text.Length > 32000) throw new InterviewProviderException("stt_invalid_response");
        }
        catch (Exception e) when (e is InterviewProviderException or IOException or UnauthorizedAccessException)
        { failure = e is InterviewProviderException p ? p.Code : "audio_unavailable"; }
        await db.WithOrganizationLockAsync(s.OrganizationId, async () => {
            await db.ReloadInterviewAsync(s, ct); await db.ReloadIntroductionAsync(introduction, ct); CheckAccess(s);

            if (s.Stage != InterviewStage.CandidateIntroduction || s.Status != InterviewStatus.WaitingForAnswer) throw AppException.Conflict();
            introduction.CheckLease(lease, time.GetUtcNow());
            if (failure is not null)
            {
                InterviewTrace.Emit("processing", "introduction", "failed", "تبدیل معرفی به متن ناموفق بود؛ وضعیت retry ذخیره می‌شود", failureCode: failure);
                introduction.Fail(lease, failure, time.GetUtcNow());
            }
            else
            {
                introduction.Complete(lease, transcript!.Text, transcript.Provider, transcript.Model, time.GetUtcNow());
                s.AdvanceStage(InterviewStage.CandidateIntroduction, transcript.Text, time.GetUtcNow());
            }
            return true;
        }, ct);
    }
}
