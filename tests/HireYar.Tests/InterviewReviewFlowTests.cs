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
        var (_, invite) = await Invite(); using var candidate = Candidate(invite);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsJsonAsync($"/api/candidate/interviews/{invite.InterviewId}/events",
            new InterviewEventRequest(Guid.NewGuid(), "consent_accepted", DateTimeOffset.UtcNow, "consent-v1"))).StatusCode);
        await f.Db(async db => {
            var exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261008135241_AddShortInterviewInvitations"));
            Assert.Equal("P0001", exception.SqlState);
            Assert.Contains("downgrade is unsafe", exception.MessageText);
            Assert.Contains("20261010054552_AddInterviewReportReview", await db.Database.GetAppliedMigrationsAsync());
            Assert.True(await db.InterviewReviewEntries.AnyAsync(e => e.InterviewSessionId == invite.InterviewId));
        });
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
        var review = (await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/review")).Content.ReadFromJsonAsync<InterviewReviewDto>())!;
        Assert.Equal("uploaded_unverified", review.RecordingStatus); Assert.Equal(10m, review.Recording!.Timeline[0].StartSeconds);
        Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Get, review.Recording.VideoUrl)).StatusCode);
        var stranger = await f.RegisterOwner(); Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Get, review.Recording.VideoUrl)).StatusCode);
        await f.Db(async db => {
            Assert.Equal(1, await db.InterviewSessionRecordings.CountAsync(r => r.InterviewSessionId == invite.InterviewId));
            Assert.False(db.Database.HasPendingModelChanges());
        });
    }

    [PostgresFact]
    public async Task Regeneration_keeps_cached_report_on_failure_and_serializes_competing_generators()
    {
        f.ReportAi.Reset(); var (hr, invite) = await Invite(); using var c = Candidate(invite);
        var start = await Start(c, invite.InterviewId); await Submit(c, invite.InterviewId, start.Question!.Id);
        await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null);
        var url = $"/api/interviews/{invite.InterviewId}/evaluation-report";
        var first = (await (await hr.Client.Send(HttpMethod.Post, url)).Content.ReadFromJsonAsync<InterviewReportDto>())!;
        Assert.Equal(HttpStatusCode.BadRequest, (await hr.Client.Send(HttpMethod.Post, url + "/regenerate", new GenerateInterviewReportRequest(new Dictionary<string, decimal> { ["invented"] = 10 }))).StatusCode);
        f.ReportAi.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously); f.ReportAi.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        f.ReportAi.Fail = true;
        var running = hr.Client.Send(HttpMethod.Post, url + "/regenerate", new GenerateInterviewReportRequest());
        await f.ReportAi.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.Equal(HttpStatusCode.Conflict, (await hr.Client.Send(HttpMethod.Post, url + "/regenerate", new GenerateInterviewReportRequest())).StatusCode);
            Assert.Equal(first.CreatedAtUtc, (await (await hr.Client.Send(HttpMethod.Get, url)).Content.ReadFromJsonAsync<InterviewReportDto>())!.CreatedAtUtc);
        }
        finally { f.ReportAi.Release.TrySetResult(); }
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await running).StatusCode);
        Assert.Equal(first.CreatedAtUtc, (await (await hr.Client.Send(HttpMethod.Get, url)).Content.ReadFromJsonAsync<InterviewReportDto>())!.CreatedAtUtc);
        f.ReportAi.Reset();
        var askedTopic = first.Content.Details!.Questions.First(q => q.Score is not null).Topic;
        var next = await hr.Client.Send(HttpMethod.Post, url + "/regenerate", new GenerateInterviewReportRequest(new Dictionary<string, decimal> { [askedTopic] = 2 }, 70));
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        var latest = (await next.Content.ReadFromJsonAsync<InterviewReportDto>())!;
        Assert.Equal(2, latest.Content.Details!.TopicWeights![askedTopic]); Assert.Equal(70m, latest.Content.Details.ReviewThreshold);
        var print = await hr.Client.Send(HttpMethod.Get, url + "/print"); Assert.Equal(HttpStatusCode.OK, print.StatusCode);
        Assert.Contains("text/html", print.Content.Headers.ContentType!.ToString());
        Assert.Contains("default-src 'none'", print.Headers.GetValues("Content-Security-Policy").Single());
    }
}
