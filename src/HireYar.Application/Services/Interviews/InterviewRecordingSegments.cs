using System.Security.Cryptography;
using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Interviews;

public sealed partial class InterviewReviewService
{
    public const long MaximumSegmentBytes = 40L * 1024 * 1024;
    private Task<InterviewRecordingSegment[]> SegmentRows(InterviewSession s, CancellationToken ct) =>
        db.InterviewRecordingSegments.AsNoTracking().Where(r => r.OrganizationId == s.OrganizationId && r.InterviewSessionId == s.Id)
            .OrderBy(r => r.SegmentNumber).ToArrayAsync(ct);
    private static InterviewRecordingSegmentDto SegmentDto(InterviewRecordingSegment r) => new(r.UploadId, r.SegmentNumber,
        $"/api/interviews/{r.InterviewSessionId}/recording/segments/{r.SegmentNumber}/video", r.DurationSeconds, r.SizeBytes,
        JsonSerializer.Deserialize<InterviewRecordingOffset[]>(r.TimelineJson, Json)!);

    public async Task<InterviewRecordingProgressDto> RecordingProgressAsync(Guid id, bool candidate, CancellationToken ct)
    {
        var s = candidate ? await Candidate(id, ct) : await Owner(id, ct);
        await quota.RequireMediaAsync(s, ct);

        var rows = await SegmentRows(s, ct);
        var legacy = await db.InterviewSessionRecordings.AnyAsync(r => r.OrganizationId == s.OrganizationId && r.InterviewSessionId == id, ct);
        return new(rows.Length + 1, rows.Sum(r => r.SizeBytes), rows.Sum(r => r.DurationSeconds), rows.Select(SegmentDto).ToArray(), legacy);
    }

    public async Task<InterviewRecordingSegmentDto> UploadSegmentAsync(Guid id, int number, Guid uploadId, Stream video,
        string fileName, string mime, long size, int duration, IReadOnlyList<InterviewRecordingOffset> timeline, CancellationToken ct)
    {
        var s = await Candidate(id, ct);
        if (s.StartedAtUtc is null) throw new AppException(409, "interview_not_started", "Start the interview before uploading its recording.");
        if (number is < 1 or > 1000 || uploadId == Guid.Empty || duration is < 1 or > 14400 || timeline is null || timeline.Count > 200 ||
            timeline.Any(t => t is null || t.StartSeconds < 0 || t.EndSeconds <= t.StartSeconds || t.EndSeconds > duration ||
                !s.Answers.Any(a => a.Id == t.AnswerId)) || timeline.Select(t => t.AnswerId).Distinct().Count() != timeline.Count)
            throw AppException.Invalid("recording_timeline_invalid");

        var type = await VideoUploadValidator.ValidateAsync(video, fileName, mime, size, new() { MaxVideoUploadBytes = MaximumSegmentBytes }, ct);
        if (video.Length != size) throw AppException.Invalid("recording_size_mismatch");
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(video, ct)); video.Position = 0;
        var serialized = JsonSerializer.Serialize(timeline.OrderBy(t => t.StartSeconds).ThenBy(t => t.AnswerId), Json);
        bool Replay(InterviewRecordingSegment r) => r.SegmentNumber == number && r.UploadId == uploadId && r.ContentHash == hash &&
            r.ContentType == type && r.SizeBytes == size && r.DurationSeconds == duration &&
            JsonSerializer.Serialize(JsonSerializer.Deserialize<InterviewRecordingOffset[]>(r.TimelineJson, Json), Json) == serialized;
        async Task<InterviewRecordingSegmentDto?> CheckState()
        {
            await db.ReloadInterviewAsync(s, ct); CheckCandidate(s);
            if (await db.InterviewSessionRecordings.AnyAsync(r => r.OrganizationId == s.OrganizationId && r.InterviewSessionId == id, ct))
                throw new AppException(409, "recording_mode_conflict", "This session already has a full recording.");
            var rows = await SegmentRows(s, ct);
            var existing = rows.FirstOrDefault(r => r.SegmentNumber == number || r.UploadId == uploadId);
            if (existing is not null) { if (!Replay(existing)) throw AppException.Conflict(); return SegmentDto(existing); }
            if (number != rows.Length + 1) throw new AppException(409, "recording_segment_order", "Upload the next segment in sequence.");
            if (rows.Sum(r => r.SizeBytes) + size > MaximumRecordingBytes || rows.Sum(r => r.DurationSeconds) + duration > 14400)
                throw new AppException(413, "recording_limit", "The total recording limit was reached.");
            return null;
        }
        var replay = await db.WithOrganizationLockAsync(s.OrganizationId, CheckState, ct);
        if (replay is not null) return replay;
        var saved = await storage.SaveAsync(video, "videos", id, MaximumSegmentBytes, ct);
        var committed = false; var won = false;
        try
        {
            if (saved.SizeBytes != size) throw AppException.Invalid("recording_size_mismatch");
            var result = await db.WithOrganizationLockAsync(s.OrganizationId, async () => {
                var winner = await CheckState(); if (winner is not null) return winner;
                var row = new InterviewRecordingSegment { InterviewSessionId = id, OrganizationId = s.OrganizationId,
                    SegmentNumber = number, UploadId = uploadId, StorageKey = saved.Key, ContentHash = hash, ContentType = type,
                    SizeBytes = size, DurationSeconds = duration, TimelineJson = serialized, UploadedAtUtc = time.GetUtcNow() };
                db.InterviewRecordingSegments.Add(row); won = true; return SegmentDto(row);
            }, ct);
            committed = won; return result;
        }
        finally { if (!committed) await storage.DeleteAsync(saved.Key, CancellationToken.None); }
    }

    public async Task<(Stream Content, string ContentType, string ContentHash)> OpenSegmentAsync(Guid id, int number, CancellationToken ct)
    {
        var s = await Owner(id, ct); await quota.RequireMediaAsync(s, ct);
        var row = await db.InterviewRecordingSegments.AsNoTracking().SingleOrDefaultAsync(r => r.OrganizationId == s.OrganizationId &&
            r.InterviewSessionId == id && r.SegmentNumber == number, ct) ?? throw AppException.NotFound();
        return (await storage.OpenReadAsync(row.StorageKey, ct), row.ContentType, row.ContentHash);
    }
}
