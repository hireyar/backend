using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using HireYar.Infrastructure.Persistence;
using Npgsql;
namespace HireYar.Tests;

public sealed class FakeInterviewProvider : ISpeechToTextService, IInterviewDecisionService, ITextToSpeechService
{
    public int SttCalls; public int TtsCalls; public bool FailStt; public bool FailLlm; public bool InvalidLlm; public bool FailTts;
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
        Entered?.TrySetResult(); if (Release is not null) await Release.Task.WaitAsync(ct);
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
    public async Task Expired_token_and_hr_token_cannot_authenticate_candidate()
    {
        var (hr, invite) = await Invite(); using var c = Candidate(invite);
        Assert.Equal(HttpStatusCode.Unauthorized, (await hr.Client.Send(HttpMethod.Get, $"/api/candidate/interviews/{invite.InterviewId}/current")).StatusCode);
        f.Clock.Offset = TimeSpan.FromDays(4);
        try { Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync($"/api/candidate/interviews/{invite.InterviewId}/current")).StatusCode); }
        finally { f.Clock.Offset = TimeSpan.Zero; }
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
