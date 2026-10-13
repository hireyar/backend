using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HireYar.Tests;

public sealed class PostgresFfmpegFactAttribute : FactAttribute
{
    public PostgresFfmpegFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HIREYAR_TEST_POSTGRES")) ||
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HIREYAR_TEST_FFMPEG")))
            Skip = "Set local HIREYAR_TEST_POSTGRES and HIREYAR_TEST_FFMPEG for real database and codec checks.";
    }
}

public sealed partial class PanelWizardTests
{
    private sealed class StageGeminiHandler : HttpMessageHandler
    {
        public List<string> Inputs { get; } = [];
        public bool Fail { get; set; }
        public TaskCompletionSource? Entered { get; set; }
        public TaskCompletionSource? Release { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("https://liara.example.test/v1/audio/speech", request.RequestUri!.ToString());
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var root = payload.RootElement;
            Assert.Equal("google/gemini-3.8-flash-tts", root.GetProperty("model").GetString());
            Assert.Equal("Aoede", root.GetProperty("voice").GetString());
            Assert.Equal("pcm", root.GetProperty("response_format").GetString());
            Inputs.Add(root.GetProperty("input").GetString()!);
            Entered?.TrySetResult();
            if (Release is { } waiting) await waiting.Task.WaitAsync(ct);
            return Fail ? new(HttpStatusCode.ServiceUnavailable) :
                new(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[48000]) };
        }
    }

    [PostgresFfmpegFact]
    public async Task Welcome_and_candidate_introduction_use_Gemini_MP3_and_keep_stage_on_TTS_failure()
    {
        var (hr, job, original) = await Setup();
        var template = Template(job.Id) with { CandidateIntroductionEnabled = true };
        Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Put,
            $"/api/panel-wizard/templates/{original.Id}", new UpdateWizardTemplateRequest(template, original.RowVersion))).StatusCode);
        var invite = (await (await Invite(hr, job.Id, original.Id)).Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation;
        using var encoder = new FfmpegPcmSpeechEncoder(Options.Create(new SpeechEncodingOptions
            { FfmpegPath = Environment.GetEnvironmentVariable("HIREYAR_TEST_FFMPEG")! }));
        using var handler = new StageGeminiHandler();
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
        using var candidate = factory.CreateClient();
        candidate.DefaultRequestHeaders.Authorization = new("Bearer", invite.Token);
        var route = $"/api/candidate/interviews/{invite.InterviewId}";
        var started = await ReadState(await candidate.PostAsync(route + "/start", null));
        Assert.Equal("Welcome", started.Stage); Assert.Null(started.Question); Assert.Empty(handler.Inputs);
        var welcome = await candidate.GetAsync(started.StageAudioUrl);
        Assert.Equal(HttpStatusCode.OK, welcome.StatusCode);
        Assert.Equal("audio/mpeg", welcome.Content.Headers.ContentType!.MediaType);
        Assert.True((await welcome.Content.ReadAsByteArrayAsync()).AsSpan().StartsWith("ID3"u8));
        Assert.Equal(new[] { template.WelcomeMessage }, handler.Inputs);
        Assert.Equal(HttpStatusCode.OK, (await candidate.GetAsync(started.StageAudioUrl)).StatusCode);
        Assert.Single(handler.Inputs);
        var introduction = await ReadState(await candidate.PostAsJsonAsync(route + "/stage/advance", new AdvanceInterviewStageRequest("Welcome")));
        Assert.Equal("CandidateIntroduction", introduction.Stage); Assert.Null(introduction.Question);
        handler.Fail = true;
        var failed = await candidate.GetAsync(introduction.StageAudioUrl);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        var problem = await failed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("stage_audio_unavailable", problem.GetProperty("code").GetString());
        var current = await candidate.GetFromJsonAsync<CandidateInterviewDto>(route + "/current");
        Assert.Equal("CandidateIntroduction", current!.Stage);
        Assert.Equal(template.CandidateIntroductionMessage, current.StageMessage);
        handler.Fail = false;
        var audio = await candidate.GetAsync(current.StageAudioUrl);
        Assert.Equal(HttpStatusCode.OK, audio.StatusCode);
        Assert.Equal("audio/mpeg", audio.Content.Headers.ContentType!.MediaType);
        Assert.True((await audio.Content.ReadAsByteArrayAsync()).AsSpan().StartsWith("ID3"u8));
        Assert.Equal(new[] { template.WelcomeMessage, template.CandidateIntroductionMessage, template.CandidateIntroductionMessage }, handler.Inputs);
        await f.Db(async db =>
        {
            var organizationId = await db.InterviewSessions.Where(x => x.Id == invite.InterviewId).Select(x => x.OrganizationId).SingleAsync();
            var sounds = await db.QuestionAudios.Where(x => x.OrganizationId == organizationId).ToArrayAsync();
            Assert.Equal(2, sounds.Length);
            Assert.All(sounds, sound => { Assert.Equal("google/gemini-3.8-flash-tts", sound.AudioModel); Assert.Equal("Aoede", sound.AudioVoice); });
        });
    }
}
