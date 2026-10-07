using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace HireYar.Tests;

public sealed partial class PanelWizardTests
{
    private async Task<(HttpClient Client, string Route, InterviewInvitation Invite, Account Hr)> VoiceInvite()
    {
        var (hr, job, original) = await Setup();
        var changed = Template(job.Id) with { CandidateIntroductionEnabled = true, CompanyIntroductionEnabled = true, CompanyIntroductionMessage = "Synthetic company conditions" };
        Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Put, $"/api/panel-wizard/templates/{original.Id}", new UpdateWizardTemplateRequest(changed, original.RowVersion))).StatusCode);
        var invitation = (await (await Invite(hr, job.Id, original.Id)).Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        var client = f.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", invitation.Token);
        var route = $"/api/candidate/interviews/{invitation.InterviewId}";
        var started = await ReadState(await client.PostAsync(route + "/start", null));
        Assert.Equal("Welcome", started.Stage); Assert.NotNull(started.StageAudioUrl);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(started.StageAudioUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route + "/stages/CompanyIntroduction/audio")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<CandidateMessageDto[]>(route + "/messages"))!);
        var introduction = await ReadState(await client.PostAsJsonAsync(route + "/stage/advance", new AdvanceInterviewStageRequest("Welcome")));
        Assert.True(introduction.CanRecord); Assert.Null(introduction.Question); Assert.NotNull(introduction.StageAudioUrl);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(introduction.StageAudioUrl)).StatusCode);
        return (client, route, invitation, hr);
    }
    private static async Task<CandidateInterviewDto> ReadState(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CandidateInterviewDto>())!;
    }
    private static MultipartFormDataContent IntroductionForm(string turnId, bool video = false)
    {
        var form = new MultipartFormDataContent();
        var audio = new ByteArrayContent("ID3-synthetic-introduction"u8.ToArray()); audio.Headers.ContentType = new("audio/mpeg");
        form.Add(audio, "audio", "introduction.mp3"); form.Add(new StringContent(turnId), "clientTurnId"); form.Add(new StringContent("10"), "durationSeconds");
        if (video)
        {
            var clip = new ByteArrayContent(new byte[] { 0x1a, 0x45, 0xdf, 0xa3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
            clip.Headers.ContentType = new("video/webm"); form.Add(clip, "video", "introduction.webm");
        }
        return form;
    }
    [PostgresFact]
    public async Task Voice_introduction_is_idempotent_unscored_and_fixed_stage_audio_is_private()
    {
        f.InterviewAi.Reset(); var (client, route, invite, hr) = await VoiceInvite(); using var c = client;
        using var first = IntroductionForm("intro-voice", true);
        var before = f.InterviewAi.SttCalls;
        var state = await ReadState(await c.PostAsync(route + "/introduction", first));
        Assert.Equal("Questions", state.Stage); Assert.Equal(0, state.Progress.AnsweredQuestions);
        Assert.Equal("Processed", state.Introduction!.Status); Assert.True(state.Introduction.VideoAvailable); Assert.False(state.Introduction.CanRetry);
        using var replay = IntroductionForm("intro-voice");
        var repeated = await ReadState(await c.PostAsync(route + "/introduction", replay));
        Assert.Equal(state.Introduction.Id, repeated.Introduction!.Id); Assert.Equal(before + 1, f.InterviewAi.SttCalls);
        using var other = IntroductionForm("different-intro");
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync(route + "/introduction", other)).StatusCode);
        var messages = (await c.GetFromJsonAsync<CandidateMessageDto[]>(route + "/messages"))!;
        var intro = Assert.Single(messages, x => x.Stage == "CandidateIntroduction" && x.Role == "candidate");
        Assert.Null(intro.QuestionId); Assert.Null(intro.AnswerId); Assert.Equal(state.Introduction.Transcript, intro.Text);
        var report = (await (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}")).Content.ReadFromJsonAsync<HrInterviewDto>())!;
        Assert.Equal(state.Introduction.Transcript, report.CandidateIntroductionTranscript); Assert.Empty(report.Answers);
        Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Get, report.Introduction!.AudioUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Get, report.Introduction.VideoUrl!)).StatusCode);
        var otherHr = await f.RegisterOwner();
        Assert.Equal(HttpStatusCode.NotFound, (await otherHr.Client.Send(HttpMethod.Get, report.Introduction.AudioUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherHr.Client.Send(HttpMethod.Get, report.Introduction.VideoUrl!)).StatusCode);
        await f.Db(async db => {
            var s = await db.InterviewSessions.Include(x => x.Answers).SingleAsync(x => x.Id == invite.InterviewId);
            Assert.Empty(s.Answers); Assert.Empty(s.Evidence); Assert.Equal(state.Introduction.Transcript, s.CandidateIntroductionTranscript);
            Assert.Single(await db.InterviewIntroductions.Where(x => x.InterviewSessionId == s.Id).ToArrayAsync()); Assert.False(db.Database.HasPendingModelChanges());
            var rejected = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"InterviewIntroductions\" SET \"Transcript\" = NULL WHERE \"Id\" = {state.Introduction.Id}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, rejected.SqlState);
            var crossOrganization = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"InterviewIntroductions\" SET \"OrganizationId\" = {Guid.NewGuid()} WHERE \"Id\" = {state.Introduction.Id}"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, crossOrganization.SqlState);
        });
        using var answer = IntroductionForm("intro-voice"); answer.Add(new StringContent(state.Question!.Id.ToString()), "questionId");
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync(route + "/answers", answer)).StatusCode);
        var completed = await ReadState(await c.PostAsync(route + "/complete", null));
        Assert.Equal("CompanyIntroduction", completed.Stage); Assert.Equal("Synthetic company conditions", completed.StageMessage);
        f.InterviewAi.FailTts = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.GetAsync(completed.StageAudioUrl)).StatusCode);
        f.InterviewAi.FailTts = false;
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(completed.StageAudioUrl)).StatusCode);
        var tts = f.InterviewAi.TtsCalls;
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(completed.StageAudioUrl)).StatusCode); Assert.Equal(tts, f.InterviewAi.TtsCalls);
        using var unauthenticated = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await unauthenticated.GetAsync(completed.StageAudioUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/candidate/interviews/{Guid.NewGuid()}/stages/CompanyIntroduction/audio")).StatusCode);
        var goodbye = await ReadState(await c.PostAsJsonAsync(route + "/stage/advance", new AdvanceInterviewStageRequest("CompanyIntroduction")));
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(completed.StageAudioUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(goodbye.StageAudioUrl)).StatusCode);
        var finished = await ReadState(await c.PostAsJsonAsync(route + "/stage/advance", new AdvanceInterviewStageRequest("Goodbye")));
        Assert.Null(finished.StageAudioUrl); f.InterviewAi.Reset();
    }
    [PostgresFact]
    public async Task Pending_introduction_replays_while_stt_runs_and_only_one_upload_is_processed()
    {
        f.InterviewAi.Reset(); var (client, route, _, _) = await VoiceInvite(); using var c = client;
        f.InterviewAi.SttEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        f.InterviewAi.SttRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            using var first = IntroductionForm("simultaneous-intro"); var submit = c.PostAsync(route + "/introduction", first);
            await f.InterviewAi.SttEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var current = (await c.GetFromJsonAsync<CandidateInterviewDto>(route + "/current"))!;
            Assert.Equal("CandidateIntroduction", current.Stage); Assert.True(current.IsProcessing); Assert.False(current.CanRecord);
            Assert.Equal("Transcribing", current.Introduction!.Status);
            using var repeat = IntroductionForm("simultaneous-intro");
            var calls = f.InterviewAi.SttCalls; var replay = await ReadState(await c.PostAsync(route + "/introduction", repeat));
            Assert.Equal(current.Introduction.Id, replay.Introduction!.Id); Assert.Equal(calls, f.InterviewAi.SttCalls);
            Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(route + "/stage/advance", new AdvanceInterviewStageRequest("CandidateIntroduction", "Text bypass"))).StatusCode);
            f.InterviewAi.SttRelease.TrySetResult(); Assert.Equal("Questions", (await ReadState(await submit)).Stage);
        }
        finally { f.InterviewAi.SttRelease?.TrySetResult(); f.InterviewAi.Reset(); }
    }
    [PostgresFact]
    public async Task Introduction_worker_recovers_saved_audio_and_pending_stays_true_during_backoff()
    {
        f.InterviewAi.Reset(); var (client, route, invite, _) = await VoiceInvite(); using var c = client;
        var options = f.Services.GetRequiredService<InterviewOptions>(); var old = options.BackgroundRecoveryEnabled;
        options.BackgroundRecoveryEnabled = true;
        try
        {
            f.InterviewAi.FailStt = true; using var form = IntroductionForm("recover-intro");
            var failed = await ReadState(await c.PostAsync(route + "/introduction", form));
            Assert.Equal("CandidateIntroduction", failed.Stage); Assert.True(failed.BackgroundRetryPending);
            Assert.True(failed.Introduction!.BackgroundRetryPending); Assert.False(failed.Introduction.CanRetry); Assert.Equal("stt_http_500", failed.Introduction.ErrorCode);
            var waiting = (await c.GetFromJsonAsync<CandidateInterviewDto>(route + "/current"))!;
            Assert.True(waiting.BackgroundRetryPending); Assert.Null(waiting.Introduction!.Transcript);
            Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync(route + $"/introduction/{failed.Introduction.Id}/retry", null)).StatusCode);
            f.InterviewAi.FailStt = false; f.Clock.Offset = TimeSpan.FromSeconds(40);
            using var worker = new HireYar.Infrastructure.Interviews.InterviewRecoveryWorker(f.Services.GetRequiredService<IServiceScopeFactory>(),
                new InterviewOptions { BackgroundRecoveryEnabled = true, RecoveryPollSeconds = 1 }, f.Clock,
                NullLogger<HireYar.Infrastructure.Interviews.InterviewRecoveryWorker>.Instance);
            await worker.StartAsync(default);
            try
            {
                for (var i = 0; i < 30; i++)
                {
                    var current = (await c.GetFromJsonAsync<CandidateInterviewDto>(route + "/current"))!;
                    if (current.Stage == "Questions") { Assert.False(current.BackgroundRetryPending); Assert.Equal(1, current.Introduction!.RecoveryAttempts); break; }
                    await Task.Delay(200);
                }
                Assert.Equal("Questions", (await c.GetFromJsonAsync<CandidateInterviewDto>(route + "/current"))!.Stage);
                await f.Db(async db => { Assert.Single(await db.InterviewIntroductions.Where(x => x.InterviewSessionId == invite.InterviewId).ToArrayAsync()); });
            }
            finally { await worker.StopAsync(default); }
        }
        finally { options.BackgroundRecoveryEnabled = old; f.Clock.Offset = TimeSpan.Zero; f.InterviewAi.Reset(); }
    }
    [PostgresFact]
    public async Task Blank_transcript_never_advances_and_bounded_recovery_allows_retry_without_upload()
    {
        f.InterviewAi.Reset(); var (client, route, invite, _) = await VoiceInvite(); using var c = client;
        var options = f.Services.GetRequiredService<InterviewOptions>(); var old = options.BackgroundRecoveryEnabled; options.BackgroundRecoveryEnabled = true;
        try
        {
            f.InterviewAi.SttText = " "; using var form = IntroductionForm("blank-intro");
            var failed = await ReadState(await c.PostAsync(route + "/introduction", form));
            Assert.Equal("stt_invalid_response", failed.Introduction!.ErrorCode); Assert.Null(failed.Introduction.Transcript); Assert.Equal("CandidateIntroduction", failed.Stage);
            for (var i = 0; i < options.MaxRecoveryAttempts; i++)
            {
                f.Clock.Offset += TimeSpan.FromSeconds(30 * (1 << i) + 2);
                await using var scope = f.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<CandidateInterviewService>().RecoverIntroductionAsync(
                    await Organization(invite.InterviewId), invite.InterviewId, default);
            }
            var capped = (await c.GetFromJsonAsync<CandidateInterviewDto>(route + "/current"))!;
            Assert.False(capped.BackgroundRetryPending); Assert.True(capped.Introduction!.CanRetry); Assert.False(capped.IsProcessing); Assert.False(capped.CanRecord);
            Assert.Equal(options.MaxRecoveryAttempts, capped.Introduction.RecoveryAttempts);
            f.InterviewAi.SttText = "Synthetic valid introduction";
            var result = await ReadState(await c.PostAsync(route + $"/introduction/{capped.Introduction.Id}/retry", null));
            Assert.Equal("Questions", result.Stage); Assert.Equal("Synthetic valid introduction", result.Introduction!.Transcript);
            Assert.Equal(0, result.Progress.AnsweredQuestions); Assert.Equal(capped.Introduction.Id, result.Introduction.Id);
        }
        finally { options.BackgroundRecoveryEnabled = old; f.Clock.Offset = TimeSpan.Zero; f.InterviewAi.Reset(); }
    }
    private async Task<Guid> Organization(Guid id)
    {
        Guid org = default; await f.Db(async db => org = await db.InterviewSessions.Where(x => x.Id == id).Select(x => x.OrganizationId).SingleAsync()); return org;
    }
    [PostgresFact]
    public async Task Interrupted_upload_processing_recovers_after_lease_expiry_and_competing_claim_is_skipped()
    {
        f.InterviewAi.Reset(); var (client, route, invite, _) = await VoiceInvite(); using var c = client;
        var options = f.Services.GetRequiredService<InterviewOptions>(); var old = options.BackgroundRecoveryEnabled; options.BackgroundRecoveryEnabled = true;
        try
        {
            f.InterviewAi.SttEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            f.InterviewAi.SttRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellation = new CancellationTokenSource(); using var form = IntroductionForm("interrupted-intro");
            var submit = c.PostAsync(route + "/introduction", form, cancellation.Token);
            await f.InterviewAi.SttEntered.Task.WaitAsync(TimeSpan.FromSeconds(10)); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await submit);
            var interrupted = (await c.GetFromJsonAsync<CandidateInterviewDto>(route + "/current"))!;
            Assert.True(interrupted.BackgroundRetryPending); Assert.True(interrupted.IsProcessing); Assert.False(interrupted.Introduction!.CanRetry);
            f.Clock.Offset = TimeSpan.FromSeconds(options.LeaseSeconds + 10);
            f.InterviewAi.SttEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            f.InterviewAi.SttRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var org = await Organization(invite.InterviewId); var calls = f.InterviewAi.SttCalls;
            await using var first = f.Services.CreateAsyncScope(); await using var second = f.Services.CreateAsyncScope();
            var recovery = first.ServiceProvider.GetRequiredService<CandidateInterviewService>().RecoverIntroductionAsync(org, invite.InterviewId, default);
            await f.InterviewAi.SttEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await second.ServiceProvider.GetRequiredService<CandidateInterviewService>().RecoverIntroductionAsync(org, invite.InterviewId, default);
            Assert.Equal(calls + 1, f.InterviewAi.SttCalls);
            f.InterviewAi.SttRelease.TrySetResult(); await recovery;
            var current = (await c.GetFromJsonAsync<CandidateInterviewDto>(route + "/current"))!;
            Assert.Equal("Questions", current.Stage); Assert.False(current.BackgroundRetryPending); Assert.Equal(interrupted.Introduction.Id, current.Introduction!.Id);
            Assert.Equal(1, current.Introduction.RecoveryAttempts); Assert.Equal(0, current.Progress.AnsweredQuestions);
        }
        finally { f.InterviewAi.SttRelease?.TrySetResult(); options.BackgroundRecoveryEnabled = old; f.Clock.Offset = TimeSpan.Zero; f.InterviewAi.Reset(); }
    }
    [PostgresFact]
    public async Task Introduction_openapi_has_binary_multipart_and_no_question_id()
    {
        using var client = f.CreateClient(); var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var operation = document.GetProperty("paths").GetProperty("/api/candidate/interviews/{interviewId}/introduction").GetProperty("post");
        var content = operation.GetProperty("requestBody").GetProperty("content"); Assert.Single(content.EnumerateObject());
        var schema = content.GetProperty("multipart/form-data").GetProperty("schema"); var properties = schema.GetProperty("properties");
        Assert.False(properties.TryGetProperty("questionId", out _)); Assert.Equal("binary", properties.GetProperty("audio").GetProperty("format").GetString());
        Assert.Equal("binary", properties.GetProperty("video").GetProperty("format").GetString());
        Assert.Contains(schema.GetProperty("required").EnumerateArray(), x => x.GetString() == "clientTurnId");
        Assert.Contains(operation.GetProperty("security").EnumerateArray(), x => x.TryGetProperty("InterviewToken", out _));
    }
}
