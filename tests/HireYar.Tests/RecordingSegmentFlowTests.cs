using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HireYar.Tests;

public sealed partial class InterviewFlowTests
{
    private static async Task<HttpResponseMessage> UploadSegment(HttpClient candidate, Guid id, int number, Guid upload,
        int duration = 15, byte tail = 0, string timeline = "[]")
    {
        using var form = new MultipartFormDataContent();
        var data = Convert.FromHexString("1A45DFA3000000000000000000000000"); data[^1] = tail;
        var bytes = new ByteArrayContent(data); bytes.Headers.ContentType = new("video/webm");
        form.Add(bytes, "video", "segment.webm"); form.Add(new StringContent(upload.ToString()), "uploadId");
        form.Add(new StringContent(number.ToString()), "segmentNumber"); form.Add(new StringContent(duration.ToString()), "durationSeconds");
        form.Add(new StringContent(timeline), "timeline");
        return await candidate.PostAsync($"/api/candidate/interviews/{id}/recording/segments", form);
    }

    [PostgresFact]
    public async Task Recording_segments_resume_after_reload_replay_exact_bytes_and_keep_media_private()
    {
        var (hr, invite) = await Invite(); using var candidate = Candidate(invite); await Start(candidate, invite.InterviewId);
        var route = $"/api/candidate/interviews/{invite.InterviewId}/recording/segments";
        var upload = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await UploadSegment(candidate, invite.InterviewId, 1, upload)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await candidate.GetAsync($"/api/candidate/interviews/{invite.InterviewId}/current")).StatusCode);
        // A new client after refresh obtains the persisted manifest, not in-memory recording state.
        using var refreshed = Candidate(invite);
        var progress = (await refreshed.GetFromJsonAsync<InterviewRecordingProgressDto>(route))!;
        Assert.Equal(2, progress.NextSegmentNumber); Assert.Equal(16, progress.TotalSizeBytes); Assert.Single(progress.Segments);
        Assert.Equal(HttpStatusCode.OK, (await UploadSegment(refreshed, invite.InterviewId, 1, upload)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await UploadSegment(refreshed, invite.InterviewId, 1, upload, tail: 1)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await UploadSegment(refreshed, invite.InterviewId, 1, upload, duration: 16)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await UploadSegment(refreshed, invite.InterviewId, 3, Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await UploadSegment(refreshed, invite.InterviewId, 2, Guid.NewGuid())).StatusCode);
        using (var form = new MultipartFormDataContent()) {
            var video = new ByteArrayContent(Convert.FromHexString("1A45DFA3000000000000000000000000")); video.Headers.ContentType = new("video/webm");
            form.Add(video, "video", "full.webm"); form.Add(new StringContent(Guid.NewGuid().ToString()), "uploadId");
            form.Add(new StringContent("30"), "durationSeconds"); form.Add(new StringContent("[]"), "timeline");
            Assert.Equal(HttpStatusCode.Conflict, (await refreshed.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/recording", form)).StatusCode);
        }
        progress = (await refreshed.GetFromJsonAsync<InterviewRecordingProgressDto>(route))!;
        Assert.Equal(3, progress.NextSegmentNumber); Assert.Equal(30, progress.TotalDurationSeconds); Assert.Equal(32, progress.TotalSizeBytes);
        Assert.Equal(new[] { 1, 2 }, progress.Segments.Select(s => s.SegmentNumber));
        var review = (await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/review")).Content.ReadFromJsonAsync<InterviewReviewDto>())!;
        Assert.Null(review.Recording); Assert.Equal("uploaded_segments_unverified", review.RecordingStatus); Assert.Equal(2, review.RecordingSegments!.Count);
        var stranger = await f.RegisterOwner();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Get, progress.Segments[0].VideoUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await candidate.GetAsync(progress.Segments[0].VideoUrl)).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, progress.Segments[0].VideoUrl);
        request.Headers.Authorization = new("Bearer", hr.Client.AccessToken); request.Headers.Range = new(4, 9);
        request.Headers.Add("Origin", "http://localhost:4200");
        using var client = f.CreateClient(); var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode); Assert.Equal("bytes 4-9/16", response.Content.Headers.ContentRange!.ToString());
        Assert.NotNull(response.Headers.ETag); Assert.False(response.Headers.ETag!.IsWeak);
        Assert.True(response.Headers.Contains("Access-Control-Expose-Headers"));
        var originalOffset = f.Clock.Offset;
        try {
            f.Clock.Offset += TimeSpan.FromMinutes(11);
            Assert.Equal(HttpStatusCode.Gone, (await refreshed.GetAsync(route)).StatusCode);
            Assert.Equal(HttpStatusCode.Gone, (await UploadSegment(refreshed, invite.InterviewId, 3, Guid.NewGuid())).StatusCode);
        }
        finally { f.Clock.Offset = originalOffset; }
        await f.Db(async db => {
            Assert.Equal(2, await db.InterviewRecordingSegments.CountAsync(s => s.InterviewSessionId == invite.InterviewId));
            Assert.False(db.Database.HasPendingModelChanges());
            var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261011120218_ConfigureRegistrationTrial"));
            Assert.Equal("P0001", error.SqlState);
        });
    }

    [PostgresFact]
    public async Task Concurrent_segment_uploads_have_one_winner_and_aggregate_duration_is_enforced()
    {
        var (_, invite) = await Invite(); using var first = Candidate(invite); using var second = Candidate(invite);
        await Start(first, invite.InterviewId);
        var responses = await Task.WhenAll(UploadSegment(first, invite.InterviewId, 1, Guid.NewGuid(), 14400),
            UploadSegment(second, invite.InterviewId, 1, Guid.NewGuid(), 14400));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await UploadSegment(first, invite.InterviewId, 2, Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadSegment(first, invite.InterviewId, 2, Guid.NewGuid(), timeline: "[{\"answerId\":\"00000000-0000-0000-0000-000000000001\",\"startSeconds\":0,\"endSeconds\":1}]")).StatusCode);
        await f.Db(async db => Assert.Equal(1, await db.InterviewRecordingSegments.CountAsync(s => s.InterviewSessionId == invite.InterviewId)));
    }

    [PostgresFact]
    public async Task S3_recording_segments_support_resume_and_authenticated_range()
    {
        var isolated = new ApiFixture { UseS3VideoStorage = true }; await isolated.InitializeAsync();
        try {
            await new InterviewFlowTests(isolated).Recording_segments_resume_after_reload_replay_exact_bytes_and_keep_media_private();
            Assert.Equal(2, isolated.VideoS3.Objects.Count);
        }
        finally { await ((IAsyncLifetime)isolated).DisposeAsync(); }
    }

    [PostgresFact]
    public async Task Segment_aggregate_byte_limit_and_tenant_foreign_key_are_enforced()
    {
        var (_, invite) = await Invite(); using var candidate = Candidate(invite); await Start(candidate, invite.InterviewId);
        await f.Db(async db => {
            var s = await db.InterviewSessions.SingleAsync(s => s.Id == invite.InterviewId);
            for (var number = 1; number <= 13; number++)
                db.InterviewRecordingSegments.Add(new() { InterviewSessionId = s.Id, OrganizationId = s.OrganizationId,
                    SegmentNumber = number, UploadId = Guid.NewGuid(), StorageKey = "synthetic-unused", ContentType = "video/webm",
                    ContentHash = new string('A', 64), SizeBytes = (number == 13 ? 32L : 40L) * 1024 * 1024,
                    DurationSeconds = 1, TimelineJson = "[]", UploadedAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await UploadSegment(candidate, invite.InterviewId, 14, Guid.NewGuid())).StatusCode);
        await f.Db(async db => {
            db.InterviewRecordingSegments.Add(new() { InterviewSessionId = invite.InterviewId, OrganizationId = Guid.NewGuid(),
                SegmentNumber = 14, UploadId = Guid.NewGuid(), StorageKey = "synthetic-unused", ContentType = "video/webm",
                ContentHash = new string('A', 64), SizeBytes = 16, DurationSeconds = 1, UploadedAtUtc = DateTimeOffset.UtcNow });
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal("23503", Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
        });
    }
}
