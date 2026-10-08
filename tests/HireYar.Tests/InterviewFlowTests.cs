using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using HireYar.Infrastructure.Persistence;
using Npgsql;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
namespace HireYar.Tests;

public sealed class FakeInterviewProvider : ISpeechToTextService, IInterviewDecisionService, ITextToSpeechService
{
    public int SttCalls; public int TtsCalls; public bool FailStt; public bool FailLlm; public bool InvalidLlm; public bool FailTts;
    public int LlmCalls;
    public InterviewDecisionContext? LastContext;
    public string? InteractionAction;
    public string? SttText;
    public TaskCompletionSource? SttEntered; public TaskCompletionSource? SttRelease;
    public TaskCompletionSource? Entered; public TaskCompletionSource? Release;
    public SpeechProfile Profile => new("fake-tts", "fake-voice", 1m);
    public async Task<SpeechTranscriptionResult> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken ct)
    {
        Interlocked.Increment(ref SttCalls);
        SttEntered?.TrySetResult(); if (SttRelease is { } waiting) await waiting.Task.WaitAsync(ct);
        if (FailStt) throw new InterviewProviderException("stt_http_500");
        return new SpeechTranscriptionResult(SttText ?? "Synthetic job-related answer", "Test", "fake-stt");
    }
    public async Task<InterviewDecisionResult> EvaluateAsync(InterviewDecisionContext context, CancellationToken ct)
    {
        Interlocked.Increment(ref LlmCalls);
        LastContext = context;
        var release = Release;
        Entered?.TrySetResult(); if (release is not null) await release.Task.WaitAsync(ct);
        if (FailLlm) throw new InterviewProviderException("llm_timeout");
        if (InvalidLlm) return new("not-json", "Test", "fake-llm");
        if (InteractionAction is { } action && context.RemainingInteractions > 0)
            return new(JsonSerializer.Serialize(new InterviewDecision {
                AnswerAssessment = new() { Relevance = 0, Clarity = 0, EvidenceQuality = 0 },
                SkillUpdates = [], CoveredTopics = [], NextQuestionId = null, ShouldComplete = false,
                Reason = "Clarification needed", Action = action, CandidateMessage = "لطفاً درباره همان موضوع بیشتر توضیح بدین."
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), "Test", "fake-llm");
        var result = new InterviewDecision { AnswerAssessment = new() { Relevance = 0.8m, Clarity = 0.8m, EvidenceQuality = 0.8m },
            SkillUpdates = [new() { SkillId = context.Question.SkillId, Delta = 0.1m, Confidence = 0.6m, Evidence = "Synthetic observable evidence" }],
            CoveredTopics = [], NextQuestionId = context.AvailableQuestions.FirstOrDefault()?.QuestionId,
            ShouldComplete = false, Reason = "More evidence needed." };
        return new(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)), "Test", "fake-llm");
    }
    public Task<TextToSpeechResult> GenerateAsync(string text, CancellationToken ct)
    {
        Interlocked.Increment(ref TtsCalls); if (FailTts) throw new InterviewProviderException("tts_http_500");
        return Task.FromResult(new TextToSpeechResult("ID3-synthetic-audio"u8.ToArray(), "audio/mpeg"));
    }
    public void Reset() { FailStt = FailLlm = InvalidLlm = FailTts = false; Entered = Release = SttEntered = SttRelease = null; LastContext = null; InteractionAction = SttText = null; }
}
[Collection("Postgres")]
public sealed class InterviewFlowTests(ApiFixture f)
{
    private sealed class TestAccess(Guid id) : ICurrentInterviewAccess { public Guid InterviewId => id; }
    private sealed class SimultaneousStorage(IInterviewAudioStorage inner) : IInterviewAudioStorage
    {
        private int arrivals;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentBag<string> Saved { get; } = [];
        public ConcurrentBag<string> Deleted { get; } = [];
        public async Task<StoredAudio> SaveAsync(Stream source, string category, Guid owner, long maximum, CancellationToken ct)
        {
            var saved = await inner.SaveAsync(source, category, owner, maximum, ct);
            Saved.Add(saved.Key);
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(ct); return saved;
        }
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => inner.OpenReadAsync(key, ct);
        public async Task DeleteAsync(string key, CancellationToken ct) { await inner.DeleteAsync(key, ct); Deleted.Add(key); }
    }
    private sealed class UnreadableStorage(bool denied) : IInterviewAudioStorage
    {
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => Task.FromException<Stream>(
            denied ? new UnauthorizedAccessException("Synthetic denied cache") : new IOException("Synthetic unavailable cache"));
        public Task<StoredAudio> SaveAsync(Stream stream, string category, Guid owner, long max, CancellationToken ct) =>
            Task.FromException<StoredAudio>(new IOException("Synthetic unavailable storage"));
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class FailingVideoStorage(IInterviewAudioStorage inner) : IInterviewAudioStorage
    {
        public string? AudioKey; public bool Deleted;
        public async Task<StoredAudio> SaveAsync(Stream source, string category, Guid owner, long maximum, CancellationToken ct)
        {
            if (category == "videos") throw new IOException("Synthetic video write failure");
            var saved = await inner.SaveAsync(source, category, owner, maximum, ct); AudioKey = saved.Key; return saved;
        }
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => inner.OpenReadAsync(key, ct);
        public async Task DeleteAsync(string key, CancellationToken ct) { await inner.DeleteAsync(key, ct); Deleted = true; }
    }
    private async Task<(Account Hr, InterviewInvitation Invite)> Invite()
    {
        f.InterviewAi.Reset(); var hr = await f.RegisterOwner();
        await f.FundInterviewsAsync(hr);
        var job = await (await hr.Client.Send(HttpMethod.Post, "/api/job-positions", new { title = "Backend", description = "Synthetic description", seniority = "Senior", status = "Active" })).Content.ReadFromJsonAsync<JsonElement>();
        var id = job.GetProperty("id").GetGuid();
        var template = await (await hr.Client.Send(HttpMethod.Post, "/api/interview-templates", new { jobPositionId = id, title = "Interview", durationMinutes = 30, maxFollowUpQuestionsPerQuestion = 0, language = "fa-IR", isArchived = false,
            questions = new[] { new { text = "Legacy question", order = 1, competency = "SQL", evaluationGuidance = "Observable evidence" } } })).Content.ReadFromJsonAsync<JsonElement>();
        var skill = await (await hr.Client.Send(HttpMethod.Post, "/api/skills", new { name = "SQL" })).Content.ReadFromJsonAsync<JsonElement>();
        var skillId = skill.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await hr.Client.Send(HttpMethod.Put, $"/api/job-positions/{id}/skills/{skillId}", new { isRequired = true, priority = 100, targetDifficulty = 3 })).StatusCode);
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Post, $"/api/job-positions/{id}/question-bank", new { skillId, text = $"Synthetic question {i}", topic = $"topic-{i}", difficulty = 3, isActive = true })).StatusCode);
        var application = await (await hr.Client.Send(HttpMethod.Post, $"/api/job-positions/{id}/applications", new { firstName = "Test", lastName = "Candidate", email = "candidate@example.test" })).Content.ReadFromJsonAsync<JsonElement>();
        var invitation = await hr.Client.Send(HttpMethod.Post, $"/api/job-positions/{id}/applications/{application.GetProperty("id").GetGuid()}/interviews", new { templateId = template.GetProperty("id").GetGuid(), maxQuestionCount = 3, expiresInHours = 72 });
        Assert.Equal(HttpStatusCode.OK, invitation.StatusCode);
        return (hr, (await invitation.Content.ReadFromJsonAsync<InterviewInvitation>())!);
    }
    private HttpClient Candidate(InterviewInvitation invitation)
    {
        var client = f.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invitation.Token); return client;
    }
    private static async Task<HttpResponseMessage> Submit(HttpClient c, Guid id, Guid question)
    {
        using var form = new MultipartFormDataContent(); form.Add(new StringContent(question.ToString()), "questionId");
        var data = new ByteArrayContent(new byte[] { 0x1a, 0x45, 0xdf, 0xa3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        data.Headers.ContentType = new("audio/webm"); form.Add(data, "audio", "answer.webm");
        return await c.PostAsync($"/api/candidate/interviews/{id}/answers", form);
    }
    private static async Task<CandidateInterviewDto> Start(HttpClient c, Guid id)
    {
        var r = await c.PostAsync($"/api/candidate/interviews/{id}/start", null); Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
    }
    private static async Task<HttpResponseMessage> SubmitCaptured(HttpClient c, Guid id, Guid question, string turn,
        DateTimeOffset ended, bool video = true, string videoMime = "video/webm", long? advertisedVideoBytes = null)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(question.ToString()), "questionId");
        form.Add(new StringContent(turn), "clientTurnId");
        form.Add(new StringContent(ended.ToString("O")), "lastSpeechEndUtc");
        form.Add(new StringContent("10"), "durationSeconds");
        var bytes = new byte[] { 0x1a, 0x45, 0xdf, 0xa3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        var audio = new ByteArrayContent(bytes); audio.Headers.ContentType = new("audio/webm"); form.Add(audio, "audio", "answer.webm");
        if (video)
        {
            var movie = new ByteArrayContent(advertisedVideoBytes is { } size ? new byte[checked((int)size)] : bytes);
            movie.Headers.ContentType = new(videoMime); form.Add(movie, "video", "capture.webm");
        }
        return await c.PostAsync($"/api/candidate/interviews/{id}/answers", form);
    }

    [PostgresFact]
    public async Task Evaluation_report_is_scoped_cached_persisted_and_does_not_run_during_live_turns()
    {
        f.ReportAi.Reset(); var (hr, invite) = await Invite(); using var c = Candidate(invite);
        var start = await Start(c, invite.InterviewId); var url = $"/api/interviews/{invite.InterviewId}/evaluation-report";
        Assert.Equal(HttpStatusCode.Conflict, (await hr.Client.Send(HttpMethod.Post, url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Submit(c, invite.InterviewId, start.Question!.Id)).StatusCode);
        Assert.Equal(0, f.ReportAi.Calls);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null)).StatusCode);
        var evidenceBefore = (await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}")).Content.ReadFromJsonAsync<HrInterviewDto>())!.Evidence;
        var response = await hr.Client.Send(HttpMethod.Post, url); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = (await response.Content.ReadFromJsonAsync<InterviewReportDto>())!;
        Assert.Equal("openai/gpt-6-luna", report.Model); Assert.Single(report.Content.Findings); Assert.Single(f.ReportAi.LastContext!.Answers);
        Assert.Equal(3, f.ReportAi.LastContext.Questions.Count);
        var again = (await (await hr.Client.Send(HttpMethod.Post, url)).Content.ReadFromJsonAsync<InterviewReportDto>())!;
        var stored = (await (await hr.Client.Send(HttpMethod.Get, url)).Content.ReadFromJsonAsync<InterviewReportDto>())!;
        Assert.Equal(report.CreatedAtUtc, again.CreatedAtUtc); Assert.Equal(report.CreatedAtUtc, stored.CreatedAtUtc); Assert.Equal(1, f.ReportAi.Calls);
        var stranger = await f.RegisterOwner(); Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Get, url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Post, url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync(url)).StatusCode);
        await f.Db(async db => {
            var row = await db.InterviewEvaluationReports.SingleAsync(r => r.InterviewSessionId == invite.InterviewId);
            Assert.NotNull(row.ResultJson); Assert.Null(row.LeaseId); Assert.Equal(report.CreatedAtUtc, row.CreatedAtUtc);
            Assert.False(db.Database.HasPendingModelChanges());
        });
        var evidenceAfter = (await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}")).Content.ReadFromJsonAsync<HrInterviewDto>())!.Evidence;
        Assert.Equal(evidenceBefore, evidenceAfter);
    }

    [PostgresFact]
    public async Task Evaluation_report_failure_releases_claim_and_retry_does_not_change_interview()
    {
        f.ReportAi.Reset(); var (hr, invite) = await Invite(); using var c = Candidate(invite); await Start(c, invite.InterviewId);
        await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null);
        var url = $"/api/interviews/{invite.InterviewId}/evaluation-report";
        foreach (var malformed in new[] { true, false })
        {
            f.ReportAi.Invalid = malformed; f.ReportAi.Fail = !malformed;
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await hr.Client.Send(HttpMethod.Post, url)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await hr.Client.Send(HttpMethod.Get, url)).StatusCode);
            await f.Db(async db => {
                var row = await db.InterviewEvaluationReports.SingleAsync(r => r.InterviewSessionId == invite.InterviewId);
                Assert.Null(row.ResultJson); Assert.Null(row.LeaseId);
                Assert.Equal(InterviewStatus.Completed, (await db.InterviewSessions.SingleAsync(s => s.Id == invite.InterviewId)).Status);
            });
        }
        f.ReportAi.Invalid = f.ReportAi.Fail = false;
        Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Post, url)).StatusCode);
        Assert.Equal(3, f.ReportAi.Calls);
    }

    [PostgresFact]
    public async Task Evaluation_report_claim_prevents_duplicate_generation_and_deleted_session_cannot_commit()
    {
        f.ReportAi.Reset(); var (hr, invite) = await Invite(); using var c = Candidate(invite); await Start(c, invite.InterviewId);
        await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null);
        var url = $"/api/interviews/{invite.InterviewId}/evaluation-report";
        f.ReportAi.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously); f.ReportAi.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = hr.Client.Send(HttpMethod.Post, url);
        await f.ReportAi.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.Equal(HttpStatusCode.Conflict, (await hr.Client.Send(HttpMethod.Post, url)).StatusCode);
            Assert.Equal(1, f.ReportAi.Calls);
            Assert.Equal(HttpStatusCode.NoContent, (await hr.Client.Send(HttpMethod.Delete, $"/api/interviews/{invite.InterviewId}")).StatusCode);
        }
        finally { f.ReportAi.Release.TrySetResult(); }
        Assert.Equal(HttpStatusCode.NotFound, (await pending).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await hr.Client.Send(HttpMethod.Get, url)).StatusCode);
        await f.Db(async db => Assert.Null((await db.InterviewEvaluationReports.SingleAsync(r => r.InterviewSessionId == invite.InterviewId)).ResultJson));
        f.ReportAi.Reset();
    }

    [PostgresFact]
    public async Task Expired_report_lease_can_be_reclaimed_and_old_generator_cannot_overwrite_result()
    {
        f.ReportAi.Reset(); var (hr, invite) = await Invite(); using var c = Candidate(invite); await Start(c, invite.InterviewId);
        await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete", null);
        var url = $"/api/interviews/{invite.InterviewId}/evaluation-report"; var originalOffset = f.Clock.Offset;
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.ReportAi.Entered = firstEntered; f.ReportAi.Release = firstRelease;
        var old = hr.Client.Send(HttpMethod.Post, url); await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            f.Clock.Offset += TimeSpan.FromSeconds(160); f.ReportAi.Entered = f.ReportAi.Release = null;
            var winner = await hr.Client.Send(HttpMethod.Post, url); Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
            var report = (await winner.Content.ReadFromJsonAsync<InterviewReportDto>())!;
            firstRelease.TrySetResult(); Assert.Equal(HttpStatusCode.Conflict, (await old).StatusCode);
            Assert.Equal(2, f.ReportAi.Calls);
            var stored = (await (await hr.Client.Send(HttpMethod.Get, url)).Content.ReadFromJsonAsync<InterviewReportDto>())!;
            Assert.Equal(report.CreatedAtUtc, stored.CreatedAtUtc);
        }
        finally { firstRelease.TrySetResult(); f.Clock.Offset = originalOffset; f.ReportAi.Reset(); }
    }

    [PostgresFact]
    public async Task Report_table_rejects_cross_organization_session_reference_and_incomplete_metadata()
    {
        var (_, invite) = await Invite(); var other = await f.RegisterOwner();
        await f.Db(async db => {
            var otherOrg = await db.Users.Where(u => u.Id == other.Id).Select(u => u.OrganizationId).SingleAsync();
            db.InterviewEvaluationReports.Add(new() { InterviewSessionId = invite.InterviewId, OrganizationId = otherOrg });
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal("23503", ((PostgresException)error.InnerException!).SqlState);
        });
        await f.Db(async db => {
            var org = await db.InterviewSessions.Where(s => s.Id == invite.InterviewId).Select(s => s.OrganizationId).SingleAsync();
            db.InterviewEvaluationReports.Add(new() { InterviewSessionId = invite.InterviewId, OrganizationId = org, ResultJson = "{}" });
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal("23514", ((PostgresException)error.InnerException!).SqlState);
        });
    }

    [PostgresFact]
    public async Task Captured_turn_persists_video_transcript_history_and_replays_without_processing_again()
    {
        var (hr, invite) = await Invite(); using var c = Candidate(invite); var start = await Start(c, invite.InterviewId);
        Assert.True(start.MediaCapabilities!.VideoAnswers); Assert.Equal(40 * 1024 * 1024, start.MediaCapabilities.MaxVideoBytes);
        var capturedNow = f.Clock.GetUtcNow();
        var ended = new DateTimeOffset(capturedNow.Ticks - capturedNow.Ticks % 10, TimeSpan.Zero);
        var turn = Guid.NewGuid().ToString(); var sttCalls = f.InterviewAi.SttCalls;
        var response = await SubmitCaptured(c, invite.InterviewId, start.Question!.Id, turn, ended);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        Assert.Equal("Synthetic job-related answer", result.Answer!.Transcript); Assert.True(result.Answer.VideoAvailable);
        Assert.Equal(turn, result.Answer.ClientTurnId); Assert.Equal(ended, result.Answer.LastSpeechEndUtc); Assert.Equal(10, result.Answer.DurationSeconds);
        var replay = await SubmitCaptured(c, invite.InterviewId, start.Question.Id, turn, ended, video: false);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(result.Answer.Id, (await replay.Content.ReadFromJsonAsync<CandidateInterviewDto>())!.Answer!.Id);
        Assert.Equal(sttCalls + 1, f.InterviewAi.SttCalls);
        Assert.Equal(HttpStatusCode.Conflict, (await SubmitCaptured(c, invite.InterviewId, result.Question!.Id, turn, ended)).StatusCode);
        var report = (await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}")).Content.ReadFromJsonAsync<HrInterviewDto>())!;
        var videoUrl = Assert.Single(report.Answers).VideoUrl!;
        var movie = await hr.Client.Send(HttpMethod.Get, videoUrl); Assert.Equal(HttpStatusCode.OK, movie.StatusCode);
        Assert.Equal("video/webm", movie.Content.Headers.ContentType!.MediaType);
        Assert.Equal(16, (await movie.Content.ReadAsByteArrayAsync()).Length);
        var stranger = await f.RegisterOwner(); Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.Send(HttpMethod.Get, videoUrl)).StatusCode);
        using var reopened = Candidate(invite);
        var history = (await (await reopened.GetAsync($"/api/candidate/interviews/{invite.InterviewId}/messages")).Content.ReadFromJsonAsync<CandidateMessageDto[]>())!;
        Assert.Equal(3, history.Length); Assert.Equal("assistant", history[0].Role); Assert.Equal("candidate", history[1].Role);
        Assert.Equal(result.Answer.Id, history[1].AnswerId); Assert.Equal(result.Answer.Transcript, history[1].Text);
        Assert.Equal(3, history.Select(m => m.Id).Distinct().Count());
        Assert.Equal(HttpStatusCode.Unauthorized, (await reopened.GetAsync($"/api/candidate/interviews/{Guid.NewGuid()}/messages")).StatusCode);
        await f.Db(async db => {
            var answer = await db.InterviewAnswers.SingleAsync(a => a.Id == result.Answer.Id);
            Assert.NotNull(answer.VideoStorageKey); Assert.Equal(ended, answer.LastSpeechEndUtc);
        });
    }

    [PostgresFact]
    public async Task Captured_turn_validates_video_size_type_and_utc_without_creating_an_answer()
    {
        var (_, invite) = await Invite(); using var c = Candidate(invite); var start = await Start(c, invite.InterviewId);
        var ended = f.Clock.GetUtcNow();
        Assert.Equal(HttpStatusCode.BadRequest, (await SubmitCaptured(c, invite.InterviewId, start.Question!.Id, "bad-video", ended, videoMime: "application/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SubmitCaptured(c, invite.InterviewId, start.Question.Id, "bad-date", ended.ToOffset(TimeSpan.FromHours(3)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SubmitCaptured(c, invite.InterviewId, start.Question.Id, "future", ended.AddHours(1))).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await SubmitCaptured(c, invite.InterviewId, start.Question.Id, "large-video", ended, advertisedVideoBytes: 40 * 1024 * 1024 + 1)).StatusCode);
        await f.Db(async db => Assert.Empty(await db.InterviewAnswers.Where(a => a.InterviewSessionId == invite.InterviewId).ToListAsync()));
        Assert.Equal(HttpStatusCode.OK, (await SubmitCaptured(c, invite.InterviewId, start.Question.Id, "audio-only", ended, video: false)).StatusCode);
    }

    [PostgresFact]
    public async Task Simultaneous_same_turn_returns_one_answer_and_runs_provider_once()
    {
        var (_, invite) = await Invite(); using var c = Candidate(invite); var started = await Start(c, invite.InterviewId);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var ownerScope = f.Services.CreateAsyncScope();
        var storage = new SimultaneousStorage(ownerScope.ServiceProvider.GetRequiredService<IInterviewAudioStorage>());
        var sttCalls = f.InterviewAi.SttCalls; var turn = Guid.NewGuid().ToString();
        async Task<CandidateInterviewDto> SubmitInScope()
        {
            await using var scope = f.Services.CreateAsyncScope();
            var service = ActivatorUtilities.CreateInstance<CandidateInterviewService>(scope.ServiceProvider, new TestAccess(invite.InterviewId), storage);
            using var source = new MemoryStream(new byte[] { 0x1a, 0x45, 0xdf, 0xa3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
            return await service.SubmitAsync(invite.InterviewId, started.Question!.Id, source, "a.webm", "audio/webm", source.Length, 1, timeout.Token, new(turn, f.Clock.GetUtcNow()));
        }
        var results = await Task.WhenAll(SubmitInScope(), SubmitInScope());
        Assert.Equal(results[0].Answer!.Id, results[1].Answer!.Id); Assert.Equal(sttCalls + 1, f.InterviewAi.SttCalls);
        Assert.Equal(2, storage.Saved.Count); Assert.Single(storage.Deleted);
        await f.Db(async db => Assert.Single(await db.InterviewAnswers.Where(a => a.InterviewSessionId == invite.InterviewId).ToListAsync()));
    }

    [PostgresFact]
    public async Task Video_write_failure_removes_audio_and_does_not_persist_the_turn()
    {
        var (_, invite) = await Invite(); using var c = Candidate(invite); var started = await Start(c, invite.InterviewId);
        await using var scope = f.Services.CreateAsyncScope();
        var storage = new FailingVideoStorage(scope.ServiceProvider.GetRequiredService<IInterviewAudioStorage>());
        var service = ActivatorUtilities.CreateInstance<CandidateInterviewService>(scope.ServiceProvider, new TestAccess(invite.InterviewId), storage);
        var bytes = new byte[] { 0x1a, 0x45, 0xdf, 0xa3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        using var audio = new MemoryStream(bytes); using var video = new MemoryStream(bytes);
        await Assert.ThrowsAsync<IOException>(() => service.SubmitAsync(invite.InterviewId, started.Question!.Id, audio, "a.webm", "audio/webm", bytes.Length, 1, default,
            new("failed-video", f.Clock.GetUtcNow(), video, "v.webm", "video/webm", bytes.Length)));
        Assert.True(storage.Deleted); Assert.NotNull(storage.AudioKey);
        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.OpenReadAsync(storage.AudioKey!, default));
        await f.Db(async db => Assert.Empty(await db.InterviewAnswers.Where(a => a.InterviewSessionId == invite.InterviewId).ToListAsync()));
    }

    [PostgresFact]
    public async Task Database_rejects_reusing_client_turn_id_for_another_question()
    {
        var (_, invite) = await Invite(); using var c = Candidate(invite); var started = await Start(c, invite.InterviewId);
        var response = await SubmitCaptured(c, invite.InterviewId, started.Question!.Id, "unique-turn", f.Clock.GetUtcNow(), video: false);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = (await response.Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        await f.Db(async db =>
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "InterviewAnswers" ("Id", "InterviewSessionId", "QuestionId", "AudioStorageKey", "AudioContentType", "AudioSizeBytes", "SubmittedAtUtc", "Status", "BillableSeconds", "ClientTurnId")
                SELECT {Guid.NewGuid()}, "InterviewSessionId", {state.Question!.Id}, "AudioStorageKey", "AudioContentType", "AudioSizeBytes", "SubmittedAtUtc", "Status", "BillableSeconds", "ClientTurnId"
                FROM "InterviewAnswers" WHERE "Id" = {state.Answer!.Id}
                """));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
            Assert.Equal("IX_InterviewAnswers_InterviewSessionId_ClientTurnId", exception.ConstraintName);
        });
    }
    [PostgresFact]
    public async Task Vertical_flow_persists_snapshots_evidence_audio_and_completion_and_token_boundary()
    {
        var (hr, invite) = await Invite(); using var c = Candidate(invite);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync($"/api/candidate/interviews/{Guid.NewGuid()}/current")).StatusCode);
        var start = await Start(c, invite.InterviewId); var initial = start.Question!.Id; var calls = f.InterviewAi.TtsCalls;
        Assert.Equal(initial, (await Start(c, invite.InterviewId)).Question!.Id); Assert.Equal(calls, f.InterviewAi.TtsCalls);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(start.Question.AudioUrl)).StatusCode);
        for (var i = 0; i < 3; i++)
        {
            var response = await Submit(c, invite.InterviewId, start.Question!.Id); Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            start = (await response.Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
        }
        Assert.Equal("Completed", start.Status); Assert.Equal(3, start.Progress.AnsweredQuestions);
        var reportResponse = await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}"); Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);
        var report = await reportResponse.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(3, report.GetProperty("evidence").GetArrayLength());
        await f.Db(async db => {
            var s = await db.InterviewSessions.Include(x => x.Answers).SingleAsync(x => x.Id == invite.InterviewId);
            Assert.NotEqual(invite.Token, s.AccessTokenHash); Assert.NotNull(s.ProtectedAccessCode);
            Assert.Equal(InterviewTokens.Hash(s.ProtectedAccessCode!), s.AccessTokenHash);
            Assert.Equal(3, s.Answers.Count); Assert.Equal(3, s.Questions.Count); Assert.Null(s.ProcessingLeaseId);
        });
        var other = await f.RegisterOwner(); Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}")).StatusCode);
        await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{invite.InterviewId}/revoke");
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync($"/api/candidate/interviews/{invite.InterviewId}/current")).StatusCode);
    }
    [PostgresFact]
    public async Task Stt_failure_retries_original_answer_and_llm_failure_falls_back()
    {
        var (_, invite) = await Invite(); using var c = Candidate(invite); var start = await Start(c, invite.InterviewId);
        f.InterviewAi.FailStt = true;
        var failed = await Submit(c, invite.InterviewId, start.Question!.Id); Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        var state = (await failed.Content.ReadFromJsonAsync<CandidateInterviewDto>())!; Assert.True(state.Answer!.CanRetry); Assert.Equal("Failed", state.Answer.Status);
        f.InterviewAi.FailStt = false; f.InterviewAi.FailLlm = true;
        var retry = await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/answers/{state.Answer.Id}/retry", null); Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal("WaitingForAnswer", (await retry.Content.ReadFromJsonAsync<CandidateInterviewDto>())!.Status);
        await f.Db(async db => {
            var s = await db.InterviewSessions.Include(x => x.Answers).SingleAsync(x => x.Id == invite.InterviewId);
            Assert.Single(s.Answers); Assert.Equal("Fallback", s.Answers[0].SelectionSource); Assert.Empty(s.Evidence);
            Assert.Equal("Synthetic job-related answer", s.Answers[0].Transcript);
        });
        f.InterviewAi.Reset();
    }
    [PostgresFact]
    public async Task Concurrent_submit_conflicts_and_stale_lease_resumes_without_repeating_stt()
    {
        var (_, invite) = await Invite(); using var c = Candidate(invite); var start = await Start(c, invite.InterviewId);
        f.InterviewAi.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously); f.InterviewAi.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Submit(c, invite.InterviewId, start.Question!.Id); await f.InterviewAi.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(HttpStatusCode.Conflict, (await Submit(c, invite.InterviewId, start.Question.Id)).StatusCode);
        var current = (await c.GetFromJsonAsync<CandidateInterviewDto>($"/api/candidate/interviews/{invite.InterviewId}/current"))!;
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/answers/{current.Answer!.Id}/retry", null)).StatusCode);
        f.Clock.Offset = TimeSpan.FromMinutes(6); var calls = f.InterviewAi.SttCalls;
        // Release the old worker after its lease has expired; it must not advance the interview.
        f.InterviewAi.Release.TrySetResult(); Assert.Equal(HttpStatusCode.Conflict, (await first).StatusCode);
        f.InterviewAi.Entered = f.InterviewAi.Release = null;
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/answers/{current.Answer.Id}/retry", null)).StatusCode);
        Assert.Equal(calls, f.InterviewAi.SttCalls);
        f.Clock.Offset = TimeSpan.Zero; f.InterviewAi.Reset();
    }
    [PostgresFact]
    public async Task Tts_failure_still_returns_question_text_and_invalid_llm_uses_fallback()
    {
        var (_, invite) = await Invite(); using var c = Candidate(invite); f.InterviewAi.FailTts = true;
        var start = await Start(c, invite.InterviewId); Assert.False(start.Question!.AudioAvailable); Assert.NotEmpty(start.Question.Text);
        f.InterviewAi.InvalidLlm = true;
        var response = await Submit(c, invite.InterviewId, start.Question.Id); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = (await response.Content.ReadFromJsonAsync<CandidateInterviewDto>())!; Assert.True(state.CanRecord);
        f.InterviewAi.Reset();
    }

    [PostgresFact]
    public async Task Unreadable_cached_question_audio_does_not_stop_the_text_flow()
    {
        var (_, invite) = await Invite(); using var c = Candidate(invite);
        var started = await Start(c, invite.InterviewId);
        await f.Db(async db => {
            foreach (var denied in new[] { false, true })
            {
                var service = new QuestionAudioService(db, new UnreadableStorage(denied), f.InterviewAi,
                    f.Clock, NullLogger<QuestionAudioService>.Instance);
                Assert.Null(await service.ResolveAsync((await db.InterviewSessions.SingleAsync(x => x.Id == invite.InterviewId)).OrganizationId,
                    started.Question!.Text, false, default));
                Assert.Null(await service.ResolveAsync((await db.InterviewSessions.SingleAsync(x => x.Id == invite.InterviewId)).OrganizationId,
                    started.Question.Text, true, default));
            }
        });
    }

    [PostgresFact]
    public async Task Expired_token_and_hr_token_cannot_authenticate_candidate()
    {
        var (hr, invite) = await Invite(); using var c = Candidate(invite);
        Assert.Equal(HttpStatusCode.Unauthorized, (await hr.Client.Send(HttpMethod.Get, $"/api/candidate/interviews/{invite.InterviewId}/current")).StatusCode);
        f.Clock.Offset = TimeSpan.FromDays(4);
        try { Assert.Equal(HttpStatusCode.Gone, (await c.GetAsync($"/api/candidate/interviews/{invite.InterviewId}/current")).StatusCode); }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }

    [PostgresFact]
    public async Task Candidate_and_hr_audio_access_are_scoped_to_the_actual_session_and_tenant()
    {
        var (hr, invite) = await Invite(); using var c = Candidate(invite);
        var (_, second) = await Invite(); using var otherCandidate = Candidate(second);
        var started = await Start(c, invite.InterviewId);
        var otherStarted = await Start(otherCandidate, second.InterviewId);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync($"/api/candidate/interviews/{second.InterviewId}/current")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsync($"/api/candidate/interviews/{second.InterviewId}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync(otherStarted.Question!.AudioUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync($"/api/interviews/{invite.InterviewId}")).StatusCode);
        Guid futureQuestion = Guid.Empty;
        await f.Db(async db => {
            var s = await db.InterviewSessions.SingleAsync(x => x.Id == invite.InterviewId);
            futureQuestion = s.Questions.First(x => x.QuestionId != started.Question!.Id).QuestionId;
        });
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/candidate/interviews/{invite.InterviewId}/questions/{futureQuestion}/audio")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Submit(c, invite.InterviewId, started.Question!.Id)).StatusCode);
        var report = (await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}")).Content.ReadFromJsonAsync<HrInterviewDto>())!;
        var answerUrl = Assert.Single(report.Answers).AudioUrl;
        Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Get, answerUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync(answerUrl)).StatusCode);
        var otherHr = await f.RegisterOwner();
        Assert.Equal(HttpStatusCode.NotFound, (await otherHr.Client.Send(HttpMethod.Get, answerUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherHr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/questions/{started.Question.Id}/audio")).StatusCode);
        var spoof = await hr.Client.Send(HttpMethod.Post, $"/api/job-positions/{report.JobPositionId}/applications",
            new { firstName = "Test", lastName = "Candidate", email = "candidate@example.test", organizationId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, spoof.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(started.Question.AudioUrl)).StatusCode);
    }

    [PostgresFact]
    public async Task Background_worker_recovers_uploaded_answer_after_abandoned_request_without_client_retry()
    {
        f.InterviewAi.Reset(); var (_, invite) = await Invite(); using var c = Candidate(invite); var start = await Start(c, invite.InterviewId);
        string key;
        await using (var scope = f.Services.CreateAsyncScope())
            key = (await scope.ServiceProvider.GetRequiredService<IInterviewAudioStorage>().SaveAsync(new MemoryStream("ID3-synthetic"u8.ToArray()), "answers", invite.InterviewId, 1024, default)).Key;
        await f.Db(async db => { var s = await db.InterviewSessions.Include(s => s.Answers).SingleAsync(s => s.Id == invite.InterviewId);
            var a = s.SubmitAnswer(start.Question!.Id, key, "audio/mpeg", 13, f.Clock.GetUtcNow());
            s.AcquireLease(a.Id, f.Clock.GetUtcNow(), 300); s.BeginTranscription(a.Id); await db.SaveChangesAsync(); });
        f.Clock.Offset = TimeSpan.FromMinutes(6);
        using var worker = new HireYar.Infrastructure.Interviews.InterviewRecoveryWorker(f.Services.GetRequiredService<IServiceScopeFactory>(),
            new InterviewOptions { BackgroundRecoveryEnabled = true, RecoveryPollSeconds = 1 }, f.Clock,
            NullLogger<HireYar.Infrastructure.Interviews.InterviewRecoveryWorker>.Instance);
        try
        {
            await worker.StartAsync(default);
            var recovered = false;
            for (var i = 0; i < 100 && !recovered; i++)
            {
                await Task.Delay(100);
                await f.Db(async db => { recovered = await db.InterviewAnswers.AnyAsync(a => a.InterviewSessionId == invite.InterviewId && a.Status == AnswerStatus.Processed); });
            }
            Assert.True(recovered);
            await f.Db(async db => { var s = await db.InterviewSessions.Include(s => s.Answers).SingleAsync(s => s.Id == invite.InterviewId);
                Assert.Equal(1, s.RecoveryAttempts); Assert.Equal(1, s.AnsweredQuestionCount); Assert.Null(s.ProcessingLeaseId); Assert.Single(s.Evidence); });
        }
        finally { await worker.StopAsync(default); f.Clock.Offset = TimeSpan.Zero; f.InterviewAi.Reset(); }
    }
    [PostgresFact]
    public async Task Automatic_recovery_is_bounded_and_does_not_process_other_organizations()
    {
        f.InterviewAi.Reset(); var (_, invite) = await Invite(); using var c = Candidate(invite); var start = await Start(c, invite.InterviewId);
        f.InterviewAi.FailStt = true;
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await Submit(c, invite.InterviewId, start.Question!.Id)).StatusCode);
            Guid org = default;
            await f.Db(async db => { org = await db.InterviewSessions.Where(s => s.Id == invite.InterviewId).Select(s => s.OrganizationId).SingleAsync(); });
            for (var i = 1; i <= 3; i++)
            {
                f.Clock.Offset = TimeSpan.FromMinutes(i * 5);
                await using var scope = f.Services.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<CandidateInterviewService>();
                await service.RecoverAsync(Guid.NewGuid(), invite.InterviewId, default);
                await service.RecoverAsync(org, invite.InterviewId, default);
            }
            var calls = f.InterviewAi.SttCalls;
            f.Clock.Offset = TimeSpan.FromMinutes(20);
            await using (var scope = f.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<CandidateInterviewService>().RecoverAsync(org, invite.InterviewId, default);
            Assert.Equal(calls, f.InterviewAi.SttCalls);
            var current = (await c.GetFromJsonAsync<CandidateInterviewDto>($"/api/candidate/interviews/{invite.InterviewId}/current"))!;
            Assert.Equal(3, current.RecoveryAttempts); Assert.True(current.Answer!.CanRetry); Assert.False(current.BackgroundRetryPending);
            Assert.Equal("Failed", current.Answer.Status); Assert.Equal(0, current.Progress.AnsweredQuestions);
        }
        finally { f.Clock.Offset = TimeSpan.Zero; f.InterviewAi.Reset(); }
    }
    [PostgresFact]
    public async Task Llm_recovery_reuses_saved_transcript_and_concurrent_claims_apply_evidence_once()
    {
        f.InterviewAi.Reset(); var (_, invite) = await Invite(); using var c = Candidate(invite); var start = await Start(c, invite.InterviewId);
        var options = f.Services.GetRequiredService<InterviewOptions>(); var previous = options.BackgroundRecoveryEnabled;
        options.BackgroundRecoveryEnabled = true; f.InterviewAi.FailLlm = true;
        try
        {
            var failed = (await (await Submit(c, invite.InterviewId, start.Question!.Id)).Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
            Assert.True(failed.BackgroundRetryPending); Assert.Equal("Failed", failed.Answer!.Status); Assert.NotNull(failed.Answer.Transcript);
            var calls = f.InterviewAi.SttCalls; var llm = f.InterviewAi.LlmCalls;
            f.InterviewAi.FailLlm = false; f.Clock.Offset = TimeSpan.FromMinutes(2); Guid org = default;
            await f.Db(async db => { org = await db.InterviewSessions.Where(s => s.Id == invite.InterviewId).Select(s => s.OrganizationId).SingleAsync(); });
            async Task Recover() { await using var scope = f.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<CandidateInterviewService>().RecoverAsync(org, invite.InterviewId, default); }
            await Task.WhenAll(Recover(), Recover());
            Assert.Equal(calls, f.InterviewAi.SttCalls); Assert.Equal(llm + 1, f.InterviewAi.LlmCalls);
            await f.Db(async db => { var s = await db.InterviewSessions.Include(s => s.Answers).SingleAsync(s => s.Id == invite.InterviewId);
                Assert.Equal(1, s.AnsweredQuestionCount); Assert.Single(s.Evidence); Assert.Equal(1, s.RecoveryAttempts); Assert.Null(s.ProcessingLeaseId); });
        }
        finally { options.BackgroundRecoveryEnabled = previous; f.Clock.Offset = TimeSpan.Zero; f.InterviewAi.Reset(); }
    }
    [PostgresFact]
    public async Task Closed_billing_stops_processing_indicator_and_recovery_preserves_failed_audio()
    {
        f.InterviewAi.Reset(); var (_, invite) = await Invite(); using var c = Candidate(invite); var start = await Start(c, invite.InterviewId);
        f.InterviewAi.FailStt = true;
        try
        {
            var failed = (await (await Submit(c, invite.InterviewId, start.Question!.Id)).Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
            Assert.Equal("Transcribing", failed.Answer!.FailureStage); Assert.Equal("stt_http_500", failed.Answer.FailureCode);
            Guid org = default;
            await f.Db(async db => {
                var bill = await db.InterviewBillings.SingleAsync(x => x.InterviewSessionId == invite.InterviewId);
                org = bill.OrganizationId; bill.Status = HireYar.Domain.Billing.InterviewBillingStatus.Settled;
                await db.SaveChangesAsync();
            });
            f.Clock.Offset = TimeSpan.FromMinutes(2); var calls = f.InterviewAi.SttCalls;
            await using (var scope = f.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<CandidateInterviewService>().RecoverAsync(org, invite.InterviewId, default);
            Assert.Equal(calls, f.InterviewAi.SttCalls);
            var current = (await c.GetFromJsonAsync<CandidateInterviewDto>($"/api/candidate/interviews/{invite.InterviewId}/current"))!;
            Assert.False(current.IsProcessing); Assert.False(current.BackgroundRetryPending); Assert.False(current.Answer!.CanRetry);
            Assert.Equal("unavailable", current.ProcessingState); Assert.NotNull(current.ProcessingMessage);
            await f.Db(async db => { var answer = await db.InterviewAnswers.SingleAsync(x => x.Id == failed.Answer.Id);
                Assert.Equal(AnswerStatus.Failed, answer.Status); Assert.NotNull(answer.AudioStorageKey); Assert.Null(answer.Transcript); });
        }
        finally { f.Clock.Offset = TimeSpan.Zero; f.InterviewAi.Reset(); }
    }
    private async Task VerifyRecovery(AnswerStatus stage)
    {
        var (_, invite) = await Invite(); using var c = Candidate(invite); var start = await Start(c, invite.InterviewId);
        Guid answerId = Guid.Empty; string audioKey = "";
        // Persist exactly the state left by a stopped request, using private storage and PostgreSQL.
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<IInterviewAudioStorage>();
            using var source = new MemoryStream("ID3-synthetic-recovery"u8.ToArray());
            audioKey = (await storage.SaveAsync(source, "answers", invite.InterviewId, 1024, default)).Key;
        }
        await f.Db(async db => {
            var s = await db.InterviewSessions.Include(x => x.Answers).SingleAsync(x => x.Id == invite.InterviewId);
            var now = f.Clock.GetUtcNow();
            var answer = s.SubmitAnswer(start.Question!.Id, audioKey, "audio/mpeg", 22, now);
            answerId = answer.Id; s.AcquireLease(answer.Id, now, 300);
            if (stage != AnswerStatus.Uploaded)
            {
                s.BeginTranscription(answer.Id); s.SetTranscript(answer.Id, "Synthetic persisted transcript", "Test", "fake-stt");
                if (stage == AnswerStatus.Evaluating) s.BeginEvaluation(answer.Id);
            }
            await db.SaveChangesAsync();
        });
        var sttCalls = f.InterviewAi.SttCalls; var llmCalls = f.InterviewAi.LlmCalls;
        f.Clock.Offset = TimeSpan.FromMinutes(6);
        try
        {
            var current = (await c.GetFromJsonAsync<CandidateInterviewDto>($"/api/candidate/interviews/{invite.InterviewId}/current"))!;
            Assert.True(current.Answer!.CanRetry); Assert.False(current.IsProcessing);
            Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/answers/{answerId}/retry", null)).StatusCode);
            Assert.Equal(sttCalls + (stage == AnswerStatus.Uploaded ? 1 : 0), f.InterviewAi.SttCalls);
            Assert.Equal(llmCalls + 1, f.InterviewAi.LlmCalls);
            // A repeat of the successful retry must not evaluate or apply anything again.
            Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/answers/{answerId}/retry", null)).StatusCode);
            Assert.Equal(llmCalls + 1, f.InterviewAi.LlmCalls);
            await f.Db(async db => {
                var s = await db.InterviewSessions.Include(x => x.Answers).SingleAsync(x => x.Id == invite.InterviewId);
                Assert.Equal(audioKey, Assert.Single(s.Answers).AudioStorageKey);
                Assert.Equal(answerId, s.Answers[0].Id); Assert.Equal(AnswerStatus.Processed, s.Answers[0].Status);
                Assert.Single(s.Evidence); Assert.Equal(1, s.SkillStates.Single().EvidenceCount);
                Assert.Equal(1, s.AnsweredQuestionCount); Assert.NotEqual(start.Question!.Id, s.CurrentQuestionId);
                Assert.Null(s.ProcessingLeaseId);
            });
        }
        finally { f.Clock.Offset = TimeSpan.Zero; f.InterviewAi.Reset(); }
    }
    [PostgresFact] public Task Saved_audio_before_stt_recovers_after_lease_expiry() => VerifyRecovery(AnswerStatus.Uploaded);
    [PostgresFact] public Task Persisted_transcript_recovers_without_repeating_stt() => VerifyRecovery(AnswerStatus.Transcribed);
    [PostgresFact] public Task Uncommitted_evaluation_can_repeat_without_duplicate_evidence() => VerifyRecovery(AnswerStatus.Evaluating);

    [PostgresFact]
    public async Task New_lease_worker_commits_once_and_old_worker_cannot_overwrite_it()
    {
        var (_, invite) = await Invite(); using var c = Candidate(invite); var start = await Start(c, invite.InterviewId);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.InterviewAi.Entered = entered; f.InterviewAi.Release = release;
        var oldWorker = Submit(c, invite.InterviewId, start.Question!.Id);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var current = (await c.GetFromJsonAsync<CandidateInterviewDto>($"/api/candidate/interviews/{invite.InterviewId}/current"))!;
            f.Clock.Offset = TimeSpan.FromMinutes(6); f.InterviewAi.Entered = f.InterviewAi.Release = null;
            var sttCalls = f.InterviewAi.SttCalls;
            var resumed = await c.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/answers/{current.Answer!.Id}/retry", null);
            Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
            var final = (await resumed.Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
            release.TrySetResult(); Assert.Equal(HttpStatusCode.Conflict, (await oldWorker).StatusCode);
            Assert.Equal(sttCalls, f.InterviewAi.SttCalls);
            await f.Db(async db => {
                var s = await db.InterviewSessions.Include(x => x.Answers).SingleAsync(x => x.Id == invite.InterviewId);
                Assert.Single(s.Answers); Assert.Single(s.Evidence); Assert.Equal(1, s.AnsweredQuestionCount);
                Assert.Equal(final.Question!.Id, s.CurrentQuestionId); Assert.Equal(1, s.SkillStates.Single().EvidenceCount);
            });
        }
        finally { release.TrySetResult(); f.Clock.Offset = TimeSpan.Zero; f.InterviewAi.Reset(); }
    }

    [PostgresFact]
    public async Task Simultaneous_submissions_from_waiting_state_commit_one_answer_and_clean_the_losing_file()
    {
        var (_, invite) = await Invite(); using var c = Candidate(invite); var started = await Start(c, invite.InterviewId);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var ownerScope = f.Services.CreateAsyncScope();
        var storage = new SimultaneousStorage(ownerScope.ServiceProvider.GetRequiredService<IInterviewAudioStorage>());
        var sttCalls = f.InterviewAi.SttCalls;
        async Task<Exception?> SubmitInScope()
        {
            await using var scope = f.Services.CreateAsyncScope();
            var service = ActivatorUtilities.CreateInstance<CandidateInterviewService>(scope.ServiceProvider,
                new TestAccess(invite.InterviewId), storage);
            using var source = new MemoryStream(new byte[] { 0x1a, 0x45, 0xdf, 0xa3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
            try
            {
                var state = await service.SubmitAsync(invite.InterviewId, started.Question!.Id, source, "a.webm", "audio/webm", source.Length, 1, timeout.Token);
                Assert.Equal(1, state.Progress.AnsweredQuestions); return null;
            }
            catch (DbUpdateException e) { return e; }
            catch (HireYar.Application.Common.AppException e) when (e.Status == 409) { return e; }
        }
        var outcomes = await Task.WhenAll(SubmitInScope(), SubmitInScope());
        Assert.Single(outcomes, x => x is null); Assert.Single(outcomes, x => x is DbUpdateException or HireYar.Application.Common.AppException);
        Assert.Equal(2, storage.Saved.Count); Assert.Single(storage.Deleted);
        Assert.Equal(sttCalls + 1, f.InterviewAi.SttCalls);
        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.OpenReadAsync(storage.Deleted.Single(), default));
        await f.Db(async db => {
            var s = await db.InterviewSessions.Include(x => x.Answers).SingleAsync(x => x.Id == invite.InterviewId);
            var answer = Assert.Single(s.Answers); Assert.Single(s.Evidence); Assert.Equal(1, s.AnsweredQuestionCount);
            Assert.Equal(1, s.SkillStates.Single().EvidenceCount); Assert.NotEqual(started.Question!.Id, s.CurrentQuestionId);
            Assert.DoesNotContain(answer.AudioStorageKey, storage.Deleted);
        });
    }

    [PostgresFact]
    public async Task Database_enforces_tenant_fk_unique_answer_and_optimistic_concurrency()
    {
        var (_, invite) = await Invite(); using var c = Candidate(invite); var started = await Start(c, invite.InterviewId);
        var (_, otherInvite) = await Invite();
        await f.Db(async db => {
            var s = await db.InterviewSessions.SingleAsync(x => x.Id == invite.InterviewId);
            var other = await db.InterviewSessions.SingleAsync(x => x.Id == otherInvite.InterviewId);
            db.CandidateApplications.Add(new CandidateApplication { OrganizationId = s.OrganizationId, JobPositionId = other.JobPositionId,
                FirstName = "Test", LastName = "Candidate", Email = "cross@example.test", CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        });
        string connection = ""; await f.Db(db => { connection = db.Database.GetConnectionString()!; return Task.CompletedTask; });
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection, n => n.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)).Options;
        await using var first = new AppDbContext(options); await using var second = new AppDbContext(options);
        var a = await first.InterviewSessions.Include(x => x.Answers).SingleAsync(x => x.Id == invite.InterviewId);
        var b = await second.InterviewSessions.Include(x => x.Answers).SingleAsync(x => x.Id == invite.InterviewId);
        var now = DateTimeOffset.UtcNow;
        var aa = a.SubmitAnswer(started.Question!.Id, "synthetic-key-a", "audio/webm", 100, now); a.AcquireLease(aa.Id, now, 300);
        var ba = b.SubmitAnswer(started.Question.Id, "synthetic-key-b", "audio/webm", 100, now); b.AcquireLease(ba.Id, now, 300);
        async Task<Exception?> Save(AppDbContext db) { try { await db.SaveChangesAsync(); return null; } catch (DbUpdateException ex) { return ex; } }
        var outcomes = await Task.WhenAll(Save(first), Save(second));
        Assert.Single(outcomes, x => x is null); Assert.Single(outcomes, x => x is DbUpdateException);
        await f.Db(async db => {
            Assert.Equal(1, await db.InterviewAnswers.CountAsync(x => x.InterviewSessionId == invite.InterviewId));
            var duplicateId = Guid.NewGuid();
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "InterviewAnswers" ("Id", "InterviewSessionId", "QuestionId", "AudioStorageKey", "AudioContentType", "AudioSizeBytes", "SubmittedAtUtc", "Status")
                SELECT {duplicateId}, "InterviewSessionId", "QuestionId", "AudioStorageKey", "AudioContentType", "AudioSizeBytes", "SubmittedAtUtc", "Status"
                FROM "InterviewAnswers" WHERE "InterviewSessionId" = {invite.InterviewId}
                """));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
            Assert.False(db.Database.HasPendingModelChanges());
        });
    }
}
