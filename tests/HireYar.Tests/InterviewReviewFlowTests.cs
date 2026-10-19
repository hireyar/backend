using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HireYar.Tests;

public sealed partial class InterviewFlowTests
{
    [PostgresFact]
    public async Task Review_migration_refuses_downgrade_that_would_discard_existing_history()
    {
        var isolated = new ApiFixture(); await isolated.InitializeAsync();
        try
        {
            var flow = new InterviewFlowTests(isolated);
            var (_, invite) = await flow.Invite(); using var candidate = flow.Candidate(invite);
            Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsJsonAsync($"/api/candidate/interviews/{invite.InterviewId}/events",
                new InterviewEventRequest(Guid.NewGuid(), "consent_accepted", DateTimeOffset.UtcNow, "consent-v1"))).StatusCode);
            await isolated.Db(async db => {
                var exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261008135241_AddShortInterviewInvitations"));
                Assert.Equal("P0001", exception.SqlState);
                Assert.Contains("pre-removal backup", exception.MessageText);
                Assert.Contains("20261010054552_AddInterviewReportReview", await db.Database.GetAppliedMigrationsAsync());
                Assert.True(await db.InterviewReviewEntries.AnyAsync(e => e.InterviewSessionId == invite.InterviewId));
            });
        }
        finally { await ((IAsyncLifetime)isolated).DisposeAsync(); }
    }
    [PostgresFact]
    public async Task Review_events_and_human_decisions_are_scoped_idempotent_and_preserve_history()
    {
        var (hr, invite) = await Invite(); using var candidate = Candidate(invite);
        var candidateUrl = $"/api/candidate/interviews/{invite.InterviewId}/events";
        var reviewUrl = $"/api/interviews/{invite.InterviewId}/review";
        var decisions = $"/api/interviews/{invite.InterviewId}/decisions";
        var empty = (await (await hr.Client.Send(HttpMethod.Get, reviewUrl)).Content.ReadFromJsonAsync<InterviewReviewDto>())!;
        Assert.Equal("unknown", empty.ConsentStatus); Assert.Equal("unknown", empty.RecordingStatus);
        var consent = new InterviewEventRequest(Guid.NewGuid(), "consent_accepted", DateTimeOffset.UtcNow, "consent-v1");
        var first = await candidate.PostAsJsonAsync(candidateUrl, consent); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var original = await first.Content.ReadFromJsonAsync<InterviewReviewEntryDto>();
        var replay = await candidate.PostAsJsonAsync(candidateUrl, consent);
        Assert.Equal(original, await replay.Content.ReadFromJsonAsync<InterviewReviewEntryDto>());
        Assert.Equal(HttpStatusCode.Conflict, (await candidate.PostAsJsonAsync(candidateUrl, consent with { ConsentVersion = "v2" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await candidate.PostAsJsonAsync(candidateUrl, consent with { RequestId = Guid.NewGuid(), Kind = "advance" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await hr.Client.Send(HttpMethod.Post, decisions, new InterviewHumanDecisionRequest(Guid.NewGuid(), "advance"))).StatusCode);
        var start = await Start(candidate, invite.InterviewId); await Submit(candidate, invite.InterviewId, start.Question!.Id);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null)).StatusCode);
        var decision = new InterviewHumanDecisionRequest(Guid.NewGuid(), "advance", "بررسی انسانی انجام شد");
        Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Post, decisions, decision)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Post, decisions, decision)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Post, decisions, new InterviewHumanDecisionRequest(Guid.NewGuid(), "review"))).StatusCode);
        var review = (await (await hr.Client.Send(HttpMethod.Get, reviewUrl)).Content.ReadFromJsonAsync<InterviewReviewDto>())!;
        Assert.Equal("accepted", review.ConsentStatus); Assert.Equal(3, review.Entries.Count);
        Assert.Equal(hr.Id, review.Entries.Single(e => e.Kind == "advance").ActorUserId);
        var stranger = await f.RegisterOwner();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Get, reviewUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Post, decisions, decision)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await candidate.GetAsync(reviewUrl)).StatusCode);
        await f.Db(async db => {
            db.InterviewReviewEntries.Add(new InterviewReviewEntry { OrganizationId = Guid.NewGuid(), InterviewSessionId = invite.InterviewId,
                RequestId = Guid.NewGuid(), ActorUserId = hr.Id, Kind = "review", RecordedAtUtc = DateTimeOffset.UtcNow });
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal("23503", Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
        });
        var offset = f.Clock.Offset;
        try { f.Clock.Offset += TimeSpan.FromMinutes(11); Assert.Equal(HttpStatusCode.Gone, (await candidate.PostAsJsonAsync(candidateUrl, consent)).StatusCode); }
        finally { f.Clock.Offset = offset; }
    }

    [PostgresFact]
    public async Task Session_recording_timeline_uses_existing_answers_and_replays_without_replacing_private_media()
    {
        var (hr, invite) = await Invite(); using var c = Candidate(invite);
        var start = await Start(c, invite.InterviewId);
        await Submit(c, invite.InterviewId, start.Question!.Id);
        var detail = (await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}")).Content.ReadFromJsonAsync<HrInterviewDto>())!;
        Guid answerId = default;
        await f.Db(async db => answerId = (await db.InterviewAnswers.SingleAsync(a => a.InterviewSessionId == invite.InterviewId)).Id);
        await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null);
        var uploadId = Guid.NewGuid();
        async Task<HttpResponseMessage> Upload(Guid answer, Guid upload)
        {
            using var form = new MultipartFormDataContent();
            var bytes = new ByteArrayContent(Convert.FromHexString("1A45DFA3000000000000000000000000")); bytes.Headers.ContentType = new("video/webm");
            form.Add(bytes, "video", "session.webm"); form.Add(new StringContent(upload.ToString()), "uploadId");
            form.Add(new StringContent("60"), "durationSeconds");
            form.Add(new StringContent(JsonSerializer.Serialize(new[] { new InterviewRecordingOffset(answer, 10, 20) }, new JsonSerializerOptions(JsonSerializerDefaults.Web))), "timeline");
            return await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/recording", form);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(Guid.NewGuid(), uploadId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Upload(answerId, uploadId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Upload(answerId, uploadId)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Upload(answerId, Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await UploadSegment(c, invite.InterviewId, 1, Guid.NewGuid())).StatusCode);
        var review = (await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/review")).Content.ReadFromJsonAsync<InterviewReviewDto>())!;
        Assert.Equal("uploaded_unverified", review.RecordingStatus); Assert.Equal(10m, review.Recording!.Timeline[0].StartSeconds);
        Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Get, review.Recording.VideoUrl)).StatusCode);
        using (var request = new HttpRequestMessage(HttpMethod.Get, review.Recording.VideoUrl))
        {
            request.Headers.Authorization = new("Bearer", hr.Client.AccessToken);
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(4, 9);
            request.Headers.Add("Origin", "http://localhost:4200");
            using var rangeClient = f.CreateClient();
            var range = await rangeClient.SendAsync(request);
            Assert.Equal(HttpStatusCode.PartialContent, range.StatusCode);
            Assert.Equal(new byte[6], await range.Content.ReadAsByteArrayAsync());
            Assert.Equal("bytes 4-9/16", range.Content.Headers.ContentRange!.ToString());
            Assert.Equal("no-store", range.Headers.CacheControl!.ToString());
            Assert.Contains("bytes", range.Headers.AcceptRanges); Assert.NotNull(range.Headers.ETag);
            Assert.Equal("inline", range.Content.Headers.ContentDisposition!.DispositionType);
            Assert.Equal("http://localhost:4200", Assert.Single(range.Headers.GetValues("Access-Control-Allow-Origin")));
            var exposed = string.Join(",", range.Headers.GetValues("Access-Control-Expose-Headers")).Split(',').Select(h => h.Trim()).ToArray();
            foreach (var header in new[] { "Content-Range", "Accept-Ranges", "ETag", "Content-Disposition" }) Assert.Contains(header, exposed, StringComparer.OrdinalIgnoreCase);
            using var anonymousRequest = new HttpRequestMessage(HttpMethod.Get, review.Recording.VideoUrl);
            anonymousRequest.Headers.Add("Origin", "http://localhost:4200");
            var anonymous = await rangeClient.SendAsync(anonymousRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode); Assert.True(anonymous.Headers.Contains("Access-Control-Expose-Headers"));
            using var untrustedRequest = new HttpRequestMessage(HttpMethod.Get, review.Recording.VideoUrl);
            untrustedRequest.Headers.Authorization = new("Bearer", hr.Client.AccessToken);
            untrustedRequest.Headers.Add("Origin", "https://untrusted.example.test");
            var untrusted = await rangeClient.SendAsync(untrustedRequest);
            Assert.False(untrusted.Headers.Contains("Access-Control-Allow-Origin"));
            using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, review.Recording.VideoUrl + "?download=true");
            downloadRequest.Headers.Authorization = new("Bearer", hr.Client.AccessToken);
            var download = await rangeClient.SendAsync(downloadRequest);
            Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
            using var preflight = new HttpRequestMessage(HttpMethod.Options, review.Recording.VideoUrl);
            preflight.Headers.Add("Origin", "http://localhost:4200"); preflight.Headers.Add("Access-Control-Request-Method", "GET");
            preflight.Headers.Add("Access-Control-Request-Headers", "authorization,range,if-range");
            var options = await rangeClient.SendAsync(preflight);
            Assert.Equal(HttpStatusCode.NoContent, options.StatusCode);
            Assert.Equal("http://localhost:4200", Assert.Single(options.Headers.GetValues("Access-Control-Allow-Origin")));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync(review.Recording.VideoUrl)).StatusCode);
        var stranger = await f.RegisterOwner(); Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Get, review.Recording.VideoUrl)).StatusCode);
        await f.Db(async db => {
            Assert.Equal(1, await db.InterviewSessionRecordings.CountAsync(r => r.InterviewSessionId == invite.InterviewId));
            Assert.False(db.Database.HasPendingModelChanges());
        });
    }


}
