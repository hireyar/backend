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
    public TaskCompletionSource? Entered; public TaskCompletionSource? Release;
    public SpeechProfile Profile => new("fake-tts", "fake-voice", 1m);
    public Task<SpeechTranscriptionResult> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken ct)
    {
        Interlocked.Increment(ref SttCalls);
        if (FailStt) throw new InterviewProviderException("stt_http_500");
        return Task.FromResult(new SpeechTranscriptionResult("Synthetic job-related answer", "Test", "fake-stt"));
    }
    public async Task<InterviewDecisionResult> EvaluateAsync(InterviewDecisionContext context, CancellationToken ct)
    {
        Interlocked.Increment(ref LlmCalls);
        var release = Release;
        Entered?.TrySetResult(); if (release is not null) await release.Task.WaitAsync(ct);
        if (FailLlm) throw new InterviewProviderException("llm_timeout");
        if (InvalidLlm) return new("not-json", "Test", "fake-llm");
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
    public void Reset() { FailStt = FailLlm = InvalidLlm = FailTts = false; Entered = Release = null; }
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
    private async Task<(Account Hr, InterviewInvitation Invite)> Invite()
    {
        f.InterviewAi.Reset(); var hr = await f.RegisterConfirmed();
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
    [PostgresFact]
    public async Task Vertical_flow_persists_snapshots_evidence_audio_and_completion_and_token_boundary()
    {
        var (hr, invite) = await Invite(); using var c = Candidate(invite);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/candidate/interviews/{Guid.NewGuid()}/current")).StatusCode);
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
            Assert.NotEqual(invite.Token, s.AccessTokenHash); Assert.True(InterviewTokens.Matches(invite.Token, s.AccessTokenHash));
            Assert.Equal(3, s.Answers.Count); Assert.Equal(3, s.Questions.Count); Assert.Null(s.ProcessingLeaseId);
        });
        var other = await f.RegisterConfirmed(); Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}")).StatusCode);
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
        try { Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync($"/api/candidate/interviews/{invite.InterviewId}/current")).StatusCode); }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }

    [PostgresFact]
    public async Task Candidate_and_hr_audio_access_are_scoped_to_the_actual_session_and_tenant()
    {
        var (hr, invite) = await Invite(); using var c = Candidate(invite);
        var (_, second) = await Invite(); using var otherCandidate = Candidate(second);
        var started = await Start(c, invite.InterviewId);
        var otherStarted = await Start(otherCandidate, second.InterviewId);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/candidate/interviews/{second.InterviewId}/current")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync($"/api/candidate/interviews/{second.InterviewId}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(otherStarted.Question!.AudioUrl)).StatusCode);
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
        var otherHr = await f.RegisterConfirmed();
        Assert.Equal(HttpStatusCode.NotFound, (await otherHr.Client.Send(HttpMethod.Get, answerUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherHr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/questions/{started.Question.Id}/audio")).StatusCode);
        var spoof = await hr.Client.Send(HttpMethod.Post, $"/api/job-positions/{report.JobPositionId}/applications",
            new { firstName = "Test", lastName = "Candidate", email = "candidate@example.test", organizationId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, spoof.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(started.Question.AudioUrl)).StatusCode);
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
        }
        var outcomes = await Task.WhenAll(SubmitInScope(), SubmitInScope());
        Assert.Single(outcomes, x => x is null); Assert.Single(outcomes, x => x is DbUpdateException);
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
