using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
namespace HireYar.Tests;

public sealed partial class PanelWizardTests
{
    [PostgresFfmpegFact]
    public async Task Speaker_test_generates_fixed_Gemini_MP3_before_start_and_reuses_private_model_cache()
    {
        var (hr, job, template) = await Setup();
        var invite = (await (await Invite(hr, job.Id, template.Id)).Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        (Guid BillId, decimal Rate, Guid ReservationId, decimal Reserved) before = default;
        await f.Db(async db=> {
            var bill=await db.InterviewBillings.SingleAsync(x=>x.InterviewSessionId==invite.InterviewId);
            var reservation=await db.AiCreditReservations.SingleAsync(x=>x.InterviewSessionId==invite.InterviewId);
            before=(bill.Id,bill.RatePerMinuteRial,reservation.Id,reservation.ReservedAmountRial);
        });
        using var encoder = new FfmpegPcmSpeechEncoder(Options.Create(new SpeechEncodingOptions
            { FfmpegPath = Environment.GetEnvironmentVariable("HIREYAR_TEST_FFMPEG")! }));
        using var handler = new StageGeminiHandler { Fail = true };
        using var http = new HttpClient(handler);
        var speech = new LiaraClient(http, Options.Create(new LiaraOptions
        {
            ApiKey = "synthetic-key", BaseUrl = "https://liara.example.test/v1",
            TtsModel = "google/gemini-3.8-flash-tts", TtsVoice = "Aoede", TtsSpeed = 1
        }), NullLogger<LiaraClient>.Instance, encoder);
        using var factory = f.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<ITextToSpeechService>(); s.AddSingleton<ITextToSpeechService>(speech);
        }));
        using var candidate = factory.CreateClient(); candidate.DefaultRequestHeaders.Authorization = new("Bearer", invite.Token);
        var route = $"/api/candidate/interviews/{invite.InterviewId}/speaker-test/audio";
        var failed = await candidate.GetAsync(route);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal("speaker_test_audio_unavailable", (await failed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        handler.Fail = false;
        var result = await candidate.GetAsync(route + "?text=Wrong");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("audio/mpeg", result.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-store", result.Headers.CacheControl!.ToString());
        var bytes = await result.Content.ReadAsByteArrayAsync(); Assert.True(bytes.AsSpan().StartsWith("ID3"u8));
        Assert.Equal(new[] { "سلام وقت بخیر", "سلام وقت بخیر" }, handler.Inputs);
        Assert.Equal(bytes, await (await candidate.GetAsync(route)).Content.ReadAsByteArrayAsync()); Assert.Equal(2, handler.Inputs.Count);
        await f.Db(async db =>
        {
            var session = await db.InterviewSessions.SingleAsync(s => s.Id == invite.InterviewId);
            Assert.Equal(InterviewStatus.Created, session.Status); Assert.Null(session.StartedAtUtc);
            Assert.False(await db.InterviewAnswers.AnyAsync(a => a.InterviewSessionId == session.Id));
            var bill=await db.InterviewBillings.SingleAsync(x=>x.InterviewSessionId==session.Id);
            var reservation=await db.AiCreditReservations.SingleAsync(x=>x.InterviewSessionId==session.Id);
            Assert.Equal(before.BillId,bill.Id);Assert.Equal(before.Rate,bill.RatePerMinuteRial);
            Assert.Equal(0,bill.BillableSeconds);Assert.Equal(0m,bill.BilledAmountRial);
            Assert.Equal(before.ReservationId,reservation.Id);Assert.Equal(before.Reserved,reservation.ReservedAmountRial);
            var sound = Assert.Single(await db.QuestionAudios.Where(x => x.OrganizationId == session.OrganizationId).ToListAsync());
            Assert.Equal("google/gemini-3.8-flash-tts", sound.AudioModel); Assert.Equal("Aoede", sound.AudioVoice);
            Assert.Equal(QuestionAudioService.Hash("سلام وقت بخیر", speech.Profile), sound.AudioHash);
        });
        using var anonymous = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(route)).StatusCode);
        using var owner = factory.CreateClient(); owner.DefaultRequestHeaders.Authorization = new("Bearer", hr.Client.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await candidate.GetAsync($"/api/candidate/interviews/{Guid.NewGuid()}/speaker-test/audio")).StatusCode);
        var second = (await (await Invite(hr, job.Id, template.Id, phone: "09912345678")).Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        using var sameOrg = factory.CreateClient(); sameOrg.DefaultRequestHeaders.Authorization = new("Bearer", second.Token);
        Assert.Equal(HttpStatusCode.OK, (await sameOrg.GetAsync($"/api/candidate/interviews/{second.InterviewId}/speaker-test/audio")).StatusCode); Assert.Equal(2, handler.Inputs.Count);
        var (otherHr, otherJob, otherTemplate) = await Setup();
        var other = (await (await Invite(otherHr, otherJob.Id, otherTemplate.Id)).Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        using var otherOrg = factory.CreateClient(); otherOrg.DefaultRequestHeaders.Authorization = new("Bearer", other.Token);
        Assert.Equal(HttpStatusCode.OK, (await otherOrg.GetAsync($"/api/candidate/interviews/{other.InterviewId}/speaker-test/audio")).StatusCode); Assert.Equal(3, handler.Inputs.Count);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/start", null)).StatusCode);
        var afterStart = await candidate.GetAsync(route); Assert.Equal(HttpStatusCode.Conflict, afterStart.StatusCode);
        Assert.Equal("speaker_test_unavailable", (await afterStart.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var beforeRevoke = handler.Inputs.Count;
        Assert.Equal(HttpStatusCode.NoContent, (await otherHr.Client.Send(HttpMethod.Post, $"/api/interviews/{other.InterviewId}/revoke")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherOrg.GetAsync($"/api/candidate/interviews/{other.InterviewId}/speaker-test/audio")).StatusCode); Assert.Equal(beforeRevoke, handler.Inputs.Count);
        var doc = await (await hr.Client.Send(HttpMethod.Get, "/openapi/v1.json")).Content.ReadFromJsonAsync<JsonElement>();
        var operation = doc.GetProperty("paths").GetProperty("/api/candidate/interviews/{interviewId}/speaker-test/audio").GetProperty("get");
        Assert.Contains("سلام وقت بخیر", operation.GetProperty("summary").GetString());
        Assert.True(operation.GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty("audio/mpeg", out _));
    }

    [PostgresFfmpegFact]
    public async Task Speaker_test_rechecks_revoked_access_after_TTS_without_returning_generated_audio()
    {
        var (hr, job, template) = await Setup();
        var invite = (await (await Invite(hr, job.Id, template.Id)).Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        using var encoder = new FfmpegPcmSpeechEncoder(Options.Create(new SpeechEncodingOptions
            { FfmpegPath = Environment.GetEnvironmentVariable("HIREYAR_TEST_FFMPEG")! }));
        using var handler = new StageGeminiHandler
        {
            Entered = new(TaskCreationOptions.RunContinuationsAsynchronously), Release = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        using var http = new HttpClient(handler);
        var speech = new LiaraClient(http, Options.Create(new LiaraOptions
        {
            ApiKey = "synthetic-key", BaseUrl = "https://liara.example.test/v1",
            TtsModel = "google/gemini-3.8-flash-tts", TtsVoice = "Aoede", TtsSpeed = 1
        }), NullLogger<LiaraClient>.Instance, encoder);
        using var factory = f.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<ITextToSpeechService>(); s.AddSingleton<ITextToSpeechService>(speech);
        }));
        using var candidate = factory.CreateClient(); candidate.DefaultRequestHeaders.Authorization = new("Bearer", invite.Token);
        var pending = candidate.GetAsync($"/api/candidate/interviews/{invite.InterviewId}/speaker-test/audio");
        try
        {
            await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.NoContent, (await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{invite.InterviewId}/revoke")).StatusCode);
        }
        finally { handler.Release.TrySetResult(); }
        var response = await pending; Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType); Assert.Equal(new[] { "سلام وقت بخیر" }, handler.Inputs);
    }
}
