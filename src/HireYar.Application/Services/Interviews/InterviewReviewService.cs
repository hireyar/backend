using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Interviews;

public sealed partial class InterviewReviewService(IApplicationDbContext db, ICurrentUser user, ICurrentInterviewAccess access,
    TimeProvider time, IInterviewAudioStorage storage, HireYar.Application.Subscriptions.InterviewQuotaService quota)
{
    public const long MaximumRecordingBytes = 512L * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private async Task<InterviewSession> Owner(Guid id, CancellationToken ct) => await db.InterviewSessions
        .SingleOrDefaultAsync(s => s.Id == id && s.OrganizationId == user.OrganizationId && s.DeletedAtUtc == null, ct) ?? throw AppException.NotFound();
    private async Task<InterviewSession> Candidate(Guid id, CancellationToken ct)
    {
        if (access.InterviewId != id) throw AppException.NotFound();
        var session = await db.InterviewSessions.Include(s => s.Answers).SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw AppException.NotFound();
        CheckCandidate(session); return session;
    }
    private void CheckCandidate(InterviewSession s)
    {
        if (s.DeletedAtUtc is not null || s.AccessRevokedAtUtc is not null || s.SupersededByInterviewId is not null ||
            s.ExpiresAtUtc <= time.GetUtcNow() || s.Status == InterviewStatus.Cancelled ||
            s.Status == InterviewStatus.Completed && (s.CompletedAtUtc is null || s.CompletedAtUtc <= time.GetUtcNow().AddMinutes(-10))) throw AppException.NotFound();
    }
    private static InterviewReviewEntryDto Map(InterviewReviewEntry e) => new(e.Id, e.Kind, e.ActorUserId, e.Note, e.ClientOccurredAtUtc, e.RecordedAtUtc);
    private async Task<InterviewReviewEntryDto> Append(InterviewSession s, Guid requestId, string kind, string? note,
        DateTimeOffset? occurredAt, Guid? actor, CancellationToken ct)
    {
        if (requestId == Guid.Empty) throw AppException.Invalid("request_id_required");
        return await db.WithOrganizationLockAsync(s.OrganizationId, async () => {
            await db.ReloadInterviewAsync(s, ct);
            if (actor is null) CheckCandidate(s);
            else if (s.DeletedAtUtc is not null || s.OrganizationId != user.OrganizationId) throw AppException.NotFound();
            else if (s.Status != InterviewStatus.Completed) throw new AppException(409, "interview_not_completed", "Complete the interview before recording a decision.");
            var existing = await db.InterviewReviewEntries.SingleOrDefaultAsync(e => e.OrganizationId == s.OrganizationId && e.InterviewSessionId == s.Id && e.RequestId == requestId, ct);
            if (existing is not null)
            {
                if (existing.Kind != kind || existing.Note != note || existing.ActorUserId != actor || existing.ClientOccurredAtUtc != occurredAt)
                    throw AppException.Conflict();
                return Map(existing);
            }
            if (await db.InterviewReviewEntries.CountAsync(e => e.OrganizationId == s.OrganizationId && e.InterviewSessionId == s.Id &&
                (actor == null ? e.ActorUserId == null : e.ActorUserId != null), ct) >= (actor is null ? 1000 : 100))
                throw new AppException(409, "review_history_limit", "The session history limit was reached.");
            var now = time.GetUtcNow();
            var row = new InterviewReviewEntry { OrganizationId = s.OrganizationId, InterviewSessionId = s.Id, RequestId = requestId,
                Kind = kind, Note = note, ActorUserId = actor, ClientOccurredAtUtc = occurredAt,
                RecordedAtUtc = new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero) };
            db.InterviewReviewEntries.Add(row); return Map(row);
        }, ct);
    }
    public async Task<InterviewReviewEntryDto> RecordEventAsync(Guid id, InterviewEventRequest r, CancellationToken ct)
    {
        var s = await Candidate(id, ct);
        if (r.Kind is not ("consent_accepted" or "consent_revoked" or "microphone_disconnected" or "camera_disconnected" or "page_hidden" or "recording_started" or "recording_stopped") ||
            r.OccurredAtUtc.Offset != TimeSpan.Zero || r.OccurredAtUtc < s.CreatedAtUtc || r.OccurredAtUtc > time.GetUtcNow().AddSeconds(30) ||
            (r.Kind == "consent_accepted" ? string.IsNullOrWhiteSpace(r.ConsentVersion) || r.ConsentVersion.Length > 100 : r.ConsentVersion is not null))
            throw AppException.Invalid("interview_event_invalid");
        // PostgreSQL timestamptz precision keeps replay comparisons stable.
        var occurred = new DateTimeOffset(r.OccurredAtUtc.Ticks - r.OccurredAtUtc.Ticks % 10, TimeSpan.Zero);
        return await Append(s, r.RequestId, r.Kind, r.ConsentVersion, occurred, null, ct);
    }
    public async Task<InterviewReviewEntryDto> DecideAsync(Guid id, InterviewHumanDecisionRequest r, CancellationToken ct)
    {
        var s = await Owner(id, ct);
        if (r.Decision is not ("advance" or "review" or "reject") || r.Note?.Length > 2000) throw AppException.Invalid("human_decision_invalid");
        return await Append(s, r.RequestId, r.Decision, r.Note, null, user.UserId, ct);
    }
    public async Task<InterviewReviewDto> GetAsync(Guid id, CancellationToken ct)
    {
        var s = await Owner(id, ct);
        var mediaAllowed = true;
        try { await quota.RequireMediaAsync(s, ct); } catch (AppException e) when (e.Status == 404) { mediaAllowed = false; }
        var rows = await db.InterviewReviewEntries.AsNoTracking().Where(e => e.OrganizationId == s.OrganizationId && e.InterviewSessionId == id)
            .OrderBy(e => e.RecordedAtUtc).ThenBy(e => e.Id).ToArrayAsync(ct);
        var consent = rows.Where(e => e.Kind.StartsWith("consent_")).OrderBy(e => e.ClientOccurredAtUtc).ThenBy(e => e.RecordedAtUtc).LastOrDefault()?.Kind;
        var recording = await db.InterviewSessionRecordings.AsNoTracking().SingleOrDefaultAsync(r => r.OrganizationId == s.OrganizationId && r.InterviewSessionId == id, ct);
        var segments = await SegmentRows(s, ct);
        return new(rows.Select(Map).ToArray(), consent switch { "consent_accepted" => "accepted", "consent_revoked" => "revoked", _ => "unknown" },
            !mediaAllowed ? "expired" : recording is not null ? "uploaded_unverified" : segments.Length > 0 ? "uploaded_segments_unverified" : "unknown",
            !mediaAllowed || recording is null ? null : Recording(recording), mediaAllowed ? segments.Select(SegmentDto).ToArray() : []);
    }
    private static InterviewRecordingDto Recording(InterviewSessionRecording r) => new($"/api/interviews/{r.InterviewSessionId}/recording/video",
        r.DurationSeconds, JsonSerializer.Deserialize<InterviewRecordingOffset[]>(r.TimelineJson, Json)!);
    public async Task<InterviewRecordingDto> UploadRecordingAsync(Guid id, Guid uploadId, Stream video, string fileName, string mime,
        long size, int duration, IReadOnlyList<InterviewRecordingOffset> timeline, CancellationToken ct)
    {
        var s = await Candidate(id, ct);
        if (s.StartedAtUtc is null) throw new AppException(409, "interview_not_started", "Start the interview before uploading its recording.");
        if (uploadId == Guid.Empty || duration is < 1 or > 14400 || timeline is null || timeline.Count > 200 ||
            timeline.Any(t => t is null || t.StartSeconds < 0 || t.EndSeconds <= t.StartSeconds || t.EndSeconds > duration ||
                !s.Answers.Any(a => a.Id == t.AnswerId)) || timeline.Select(t => t.AnswerId).Distinct().Count() != timeline.Count)
            throw AppException.Invalid("recording_timeline_invalid");
        var serialized = JsonSerializer.Serialize(timeline.OrderBy(t => t.StartSeconds).ThenBy(t => t.AnswerId), Json);
        var existing = await db.InterviewSessionRecordings.SingleOrDefaultAsync(r => r.OrganizationId == s.OrganizationId && r.InterviewSessionId == id, ct);
        if (await db.InterviewRecordingSegments.AnyAsync(r => r.OrganizationId == s.OrganizationId && r.InterviewSessionId == id, ct))
            throw new AppException(409, "recording_mode_conflict", "This session uses segmented recording.");
        // JSONB whitespace is provider-normalized; compare structured timeline for replay.
        bool Replay(InterviewSessionRecording r) => r.UploadId == uploadId && r.DurationSeconds == duration &&
            JsonSerializer.Serialize(JsonSerializer.Deserialize<InterviewRecordingOffset[]>(r.TimelineJson, Json), Json) == serialized;
        if (existing is not null) { if (!Replay(existing)) throw AppException.Conflict(); return Recording(existing); }
        var contentType = await VideoUploadValidator.ValidateAsync(video, fileName, mime, size, new() { MaxVideoUploadBytes = MaximumRecordingBytes }, ct);

        var saved = await storage.SaveAsync(video, "videos", id, MaximumRecordingBytes, ct);
        bool committed = false, won = false;
        try
        {
            if (saved.SizeBytes != size) throw AppException.Invalid("recording_size_mismatch");
            var result = await db.WithOrganizationLockAsync(s.OrganizationId, async () => {
                await db.ReloadInterviewAsync(s, ct); CheckCandidate(s);

                if (await db.InterviewRecordingSegments.AnyAsync(r => r.OrganizationId == s.OrganizationId && r.InterviewSessionId == id, ct))
                    throw new AppException(409, "recording_mode_conflict", "This session uses segmented recording.");
                var winner = await db.InterviewSessionRecordings.SingleOrDefaultAsync(r => r.OrganizationId == s.OrganizationId && r.InterviewSessionId == id, ct);
                if (winner is not null) { if (!Replay(winner)) throw AppException.Conflict(); return Recording(winner); }
                var row = new InterviewSessionRecording { InterviewSessionId = id, OrganizationId = s.OrganizationId, UploadId = uploadId,
                    StorageKey = saved.Key, SizeBytes = saved.SizeBytes, ContentType = contentType, DurationSeconds = duration,
                    TimelineJson = serialized, UploadedAtUtc = time.GetUtcNow() };
                db.InterviewSessionRecordings.Add(row); won = true; return Recording(row);
            }, ct);
            committed = won;
            return result;
        }
        finally { if (!committed) await storage.DeleteAsync(saved.Key, CancellationToken.None); }
    }
    public async Task<(Stream Content, string ContentType)> OpenRecordingAsync(Guid id, CancellationToken ct)
    {
        var s = await Owner(id, ct);
        await quota.RequireMediaAsync(s, ct);
        var row = await db.InterviewSessionRecordings.AsNoTracking().SingleOrDefaultAsync(r => r.OrganizationId == s.OrganizationId && r.InterviewSessionId == id, ct) ?? throw AppException.NotFound();
        return (await storage.OpenReadAsync(row.StorageKey, ct), row.ContentType);
    }
}
