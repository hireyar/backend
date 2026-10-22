using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HireYar.Tests;

public sealed class AiCapabilityRoutingTests
{
    private static Dictionary<string, string?> Settings() => new() {
        ["AI:Providers:Liara:BaseUrl"] = "https://liara.example.test/v1", ["AI:Providers:Liara:ApiKey"] = "synthetic-liara",
        ["AI:Providers:NetArz:BaseUrl"] = "https://netarz.example.test/v1", ["AI:Providers:NetArz:ApiKey"] = "synthetic-netarz",
        ["AI:Providers:Cloudflare:AccountId"] = new('a', 32), ["AI:Providers:Cloudflare:ApiToken"] = "synthetic-cloudflare",
        ["AI:Providers:Soniox:ApiKey"] = "synthetic-soniox",
        ["AI:SpeechToText:Provider"] = "Soniox", ["AI:SpeechToText:Model"] = "stt-rt-v5",
        ["AI:InterviewDecision:Provider"] = "NetArz", ["AI:InterviewDecision:Model"] = "gemini-test",
        ["AI:QuestionSuggestion:Provider"] = "Liara", ["AI:QuestionSuggestion:Model"] = "suggest-test",
        ["AI:AnswerAssessment:Provider"] = "Liara", ["AI:AnswerAssessment:Model"] = "assess-test",
        ["AI:ReportNarrative:Provider"] = "Cloudflare", ["AI:ReportNarrative:Model"] = "@cf/test",
        ["AI:TextToSpeech:Provider"] = "Liara", ["AI:TextToSpeech:Model"] = "tts-test"
    };

    private static ServiceProvider Services(Dictionary<string, string?> settings, HttpMessageHandler? handler = null)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddInterviews(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        if (handler is not null) services.AddHttpClient("InterviewAI").ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    private sealed class Encoder : IPcmSpeechEncoder
    {
        public Task EnsureAvailableAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<byte[]> EncodeAsync(byte[] pcm, decimal speed, CancellationToken ct) => Task.FromResult("ID3test"u8.ToArray());
    }

    private sealed class WireHandler : HttpMessageHandler
    {
        public string? Payload; public string? Path;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Payload = await request.Content!.ReadAsStringAsync(ct); Path = request.RequestUri!.AbsolutePath;
            var boundary = request.Content.Headers.ContentType?.Parameters.FirstOrDefault(p => p.Name == "boundary")?.Value?.Trim('"');
            if (boundary is not null) Payload = Payload.Replace(boundary, "boundary");
            return new(HttpStatusCode.OK) { Content = Path.EndsWith("/audio/speech") ? new ByteArrayContent([0, 1, 2, 3]) :
                new StringContent(Path.EndsWith("/audio/transcriptions") ? "{\"text\":\"test\"}" : "{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}") };
        }
    }

    [Theory]
    [InlineData("SpeechToText")]
    [InlineData("InterviewDecision")]
    [InlineData("QuestionSuggestion")]
    [InlineData("AnswerAssessment")]
    [InlineData("ReportNarrative")]
    [InlineData("TextToSpeech")]
    public async Task New_routing_preserves_existing_Liara_wire_requests(string capability)
    {
        var legacyHandler = new WireHandler(); var routedHandler = new WireHandler();
        var options = new LiaraOptions { BaseUrl = "https://liara.example.test/v1", ApiKey = "synthetic-liara",
            SttModel = "openai/gpt-transcribe", TtsModel = "google/gemini-3.8-flash-tts", TtsVoice = "Aoede" };
        var old = new LiaraClient(new HttpClient(legacyHandler), Options.Create(options), Microsoft.Extensions.Logging.Abstractions.NullLogger<LiaraClient>.Instance, new Encoder());
        var settings = Settings();
        foreach (var name in AiRoutingOptions.Capabilities) {
            settings[$"AI:{name}:Provider"] = "Liara";
            settings[$"AI:{name}:Model"] = name switch { "SpeechToText" => options.SttModel, "TextToSpeech" => options.TtsModel, _ => options.LlmModel };
        }
        settings["AI:TextToSpeech:Options:Voice"] = options.TtsVoice;
        var collection = new ServiceCollection(); collection.AddLogging();
        collection.AddInterviews(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        collection.AddSingleton<IPcmSpeechEncoder>(new Encoder());
        collection.AddHttpClient("InterviewAI").ConfigurePrimaryHttpMessageHandler(() => routedHandler);
        using var services = collection.BuildServiceProvider();
        var question = new HireYar.Domain.Interviews.InterviewQuestionSnapshot(Guid.NewGuid(), Guid.NewGuid(), "test", "test", 3, true);
        var context = new InterviewDecisionContext("test", question.QuestionId, "ProcessingAnswer", 0, 5, 60, question, "test", [], [], [], []);
        async Task Invoke(object client) {
            switch (capability) {
                case "SpeechToText": await ((ISpeechToTextService)client).TranscribeAsync(new MemoryStream([1,2,3]), "test.wav", "audio/wav", default); break;
                case "InterviewDecision": await ((IInterviewDecisionService)client).EvaluateAsync(context, default); break;
                case "QuestionSuggestion": await ((IQuestionSuggestionProvider)client).SuggestAsync(new("test", "test", 5, null), default); break;
                case "AnswerAssessment": await ((IAnswerAssessmentProvider)client).AssessAnswerAsync(new("test", "specialized", "test", "test", 3, []), default); break;
                case "ReportNarrative": await ((IReportNarrativeProvider)client).GenerateNarrativeAsync(new("test", null, [], []), default); break;
                case "TextToSpeech": await ((ITextToSpeechService)client).GenerateAsync("test", default); break;
            }
        }
        object routed = capability switch {
            "SpeechToText" => services.GetRequiredService<ISpeechToTextService>(), "InterviewDecision" => services.GetRequiredService<IInterviewDecisionService>(),
            "QuestionSuggestion" => services.GetRequiredService<IQuestionSuggestionProvider>(), "AnswerAssessment" => services.GetRequiredService<IAnswerAssessmentProvider>(),
            "ReportNarrative" => services.GetRequiredService<IReportNarrativeProvider>(), _ => services.GetRequiredService<ITextToSpeechService>()
        };
        await Invoke(old); await Invoke(routed);
        Assert.Equal(legacyHandler.Path, routedHandler.Path); Assert.Equal(legacyHandler.Payload, routedHandler.Payload);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public List<(string Host, string Path, string? Key, string? Model)> Calls { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Calls.Add((request.RequestUri!.Host, request.RequestUri.AbsolutePath, request.Headers.Authorization?.Parameter, body.RootElement.GetProperty("model").GetString()));
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}") };
        }
    }

    [Fact]
    public async Task Capabilities_use_independent_connections_models_and_result_identity()
    {
        var handler = new Handler(); using var services = Services(Settings(), handler);
        Assert.IsType<SonioxSpeechToTextService>(services.GetRequiredService<ISpeechToTextService>());
        var decision = await services.GetRequiredService<IInterviewDecisionService>().EvaluateAsync(new("test", Guid.NewGuid(), "ProcessingAnswer", 0, 5, 60,
            new(Guid.NewGuid(), Guid.NewGuid(), "test", "test", 3, true), "test", [], [], [], []), default);
        await services.GetRequiredService<IQuestionSuggestionProvider>().SuggestAsync(new("test", "test", 5, null), default);
        var assessment = await services.GetRequiredService<IAnswerAssessmentProvider>().AssessAnswerAsync(new("test", "specialized", "question", "topic", 3, []), default);
        Assert.Equal("NetArz", decision.Provider); Assert.Equal("gemini-test", decision.Model);
        Assert.Equal("Liara", assessment.Provider); Assert.Equal("assess-test", assessment.Model);
        Assert.Equal(new SpeechProfile("tts-test", "nova", 1), services.GetRequiredService<ITextToSpeechService>().Profile);
        var narrative = await services.GetRequiredService<IReportNarrativeProvider>().GenerateNarrativeAsync(new("test", null, [], []), default);
        Assert.Equal("Cloudflare", narrative.Provider); Assert.Equal("@cf/test", narrative.Model);
        Assert.Equal(new[] { "netarz.example.test", "liara.example.test", "liara.example.test", "api.cloudflare.com" }, handler.Calls.Select(c => c.Host));
        Assert.Equal(new[] { "gemini-test", "suggest-test", "assess-test", "@cf/test" }, handler.Calls.Select(c => c.Model));
        Assert.Equal("synthetic-cloudflare", handler.Calls[3].Key);
        Assert.Equal($"/client/v4/accounts/{new string('a',32)}/ai/v1/chat/completions", handler.Calls[3].Path);
    }

    [Theory]
    [InlineData("SpeechToText", "Cloudflare")]
    [InlineData("TextToSpeech", "Soniox")]
    [InlineData("InterviewDecision", "Soniox")]
    [InlineData("ReportNarrative", "Unknown")]
    public void Unsupported_combinations_are_rejected(string capability, string provider)
    {
        var settings = Settings(); settings[$"AI:{capability}:Provider"] = provider;
        using var services = Services(settings);
        var error = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<AiRoutingOptions>>().Value);
        Assert.Contains($"AI:{capability}", error.Message);
    }

    [Fact]
    public void Selected_credentials_and_combined_lease_are_validated()
    {
        var settings = Settings(); settings["AI:Providers:Soniox:ApiKey"] = "";
        using var missing = Services(settings);
        Assert.Throws<OptionsValidationException>(() => missing.GetRequiredService<ISpeechToTextService>());
        settings = Settings(); settings["InterviewAudio:LeaseSeconds"] = "150";
        using var shortLease = Services(settings);
        Assert.Throws<OptionsValidationException>(() => shortLease.GetRequiredService<IOptions<InterviewOptions>>().Value);
    }

    [Fact]
    public async Task Explicit_routing_ignores_legacy_selection_and_disables_each_capability_independently()
    {
        var settings = Settings(); settings["AI:Provider"] = "NetArz";
        settings["AI:TextToSpeech:Provider"] = "Disabled";
        settings["AI:Providers:Unused:BaseUrl"] = "http://invalid";
        using var services = Services(settings);
        Assert.IsType<SonioxSpeechToTextService>(services.GetRequiredService<ISpeechToTextService>());
        var error = await Assert.ThrowsAsync<InterviewProviderException>(() => services.GetRequiredService<ITextToSpeechService>().GenerateAsync("test", default));
        Assert.Equal("provider_not_configured", error.Code);
    }

    private sealed class Socket(params string[] messages) : WebSocket
    {
        private readonly Queue<byte[]> incoming = new(messages.Select(Encoding.UTF8.GetBytes));
        public List<(WebSocketMessageType Type, byte[] Bytes)> Sent { get; } = [];
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => WebSocketState.Open;
        public override string? SubProtocol => null;
        public override void Abort() { }
        public override void Dispose() { }
        public override Task CloseAsync(WebSocketCloseStatus status, string? description, CancellationToken ct) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken ct) => Task.CompletedTask;
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool endOfMessage, CancellationToken ct)
        { Sent.Add((type, buffer.ToArray())); return Task.CompletedTask; }
        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken ct)
        {
            if (incoming.Count == 0) { await Task.Delay(Timeout.Infinite, ct); throw new InvalidOperationException(); }
            var bytes = incoming.Dequeue(); bytes.AsSpan().CopyTo(buffer.AsSpan()); return new(bytes.Length, WebSocketMessageType.Text, true);
        }
    }

    [Fact]
    public async Task Soniox_sends_configuration_binary_audio_and_empty_text_and_collects_only_final_tokens()
    {
        using var socket = new Socket("""{"tokens":[{"text":"موقت","is_final":false},{"text":"سلام","is_final":true}]}""",
            """{"tokens":[{"text":" دنیا","is_final":true},{"text":"<end>","is_final":true}],"finished":true}""");
        using var audio = new MemoryStream([1, 2, 3]);
        var result = await SonioxSpeechToTextService.TranscribeSessionAsync(socket, audio, new() { Model = "stt-test" }, default);
        Assert.Equal(new SpeechTranscriptionResult("سلام دنیا", "Soniox", "stt-test"), result);
        Assert.Equal(WebSocketMessageType.Text, socket.Sent[0].Type);
        using var config = JsonDocument.Parse(socket.Sent[0].Bytes);
        Assert.Equal("auto", config.RootElement.GetProperty("audio_format").GetString());
        Assert.Equal("fa", config.RootElement.GetProperty("language_hints")[0].GetString());
        Assert.False(config.RootElement.TryGetProperty("api_key", out _));
        Assert.Equal(WebSocketMessageType.Binary, socket.Sent[1].Type);
        Assert.Equal(new byte[] { 1, 2, 3 }, socket.Sent[1].Bytes);
        Assert.Equal(WebSocketMessageType.Text, socket.Sent[^1].Type); Assert.Empty(socket.Sent[^1].Bytes);
    }

    [Theory]
    [InlineData("{\"error_code\":429,\"error_message\":\"sensitive\"}", "stt_http_429")]
    [InlineData("{\"tokens\":{}}", "stt_invalid_response")]
    public async Task Soniox_rejects_errors_empty_and_malformed_results(string message, string code)
    {
        using var socket = new Socket(message); using var audio = new MemoryStream([1]);
        var error = await Assert.ThrowsAsync<InterviewProviderException>(() => SonioxSpeechToTextService.TranscribeSessionAsync(socket, audio, new(), default));
        Assert.Equal(code, error.Code); Assert.DoesNotContain("sensitive", error.Message);
    }

    [Theory]
    [InlineData("{\"error_code\":400,\"error_message\":\"Language hints must be unique.\"}", "language_hints_duplicate")]
    [InlineData("{\"error_code\":400,\"error_message\":\"Audio decode error: sensitive\"}", "audio_decode_error")]
    [InlineData("{\"error_code\":400,\"error_type\":\"model_not_available\",\"error_message\":\"sensitive\"}", "model_not_available")]
    [InlineData("{\"error_code\":400,\"error_message\":\"Audio data sample rate must be specified for PCM formats\"}", "audio_sample_rate_required")]
    [InlineData("{\"error_code\":400,\"error_message\":\"Audio data channels must be specified for PCM formats\"}", "audio_channels_required")]
    [InlineData("{\"error_code\":400,\"error_message\":\"Audio is too long.\"}", "audio_too_long")]
    [InlineData("{\"error_code\":400,\"error_message\":\"sensitive token and user content\"}", "provider_request_rejected")]
    [InlineData("{\"error_code\":400,\"error_message\":{\"unexpected\":\"sensitive\"}}", "provider_request_rejected")]
    public async Task Soniox_bad_request_keeps_only_safe_fixed_diagnostic(string message, string reason)
    {
        using var socket = new Socket(message); using var audio = new MemoryStream([1]);
        var error = await Assert.ThrowsAsync<InterviewProviderException>(() => SonioxSpeechToTextService.TranscribeSessionAsync(socket, audio, new(), default));
        Assert.Equal("stt_http_400", error.Code); Assert.Equal(reason, error.Reason);
        Assert.DoesNotContain("sensitive", error.ToString()); Assert.DoesNotContain("sensitive", error.Reason!);
    }

    [Fact]
    public async Task Soniox_completed_silence_is_valid_empty_transcription_for_microphone_check()
    {
        using var socket = new Socket("""{"tokens":[],"finished":true}""");
        using var audio = new MemoryStream([1]);
        var result = await SonioxSpeechToTextService.TranscribeSessionAsync(socket, audio, new() { Model = "stt-test" }, default);
        Assert.Equal(string.Empty, result.Text);
        Assert.Equal("Soniox", result.Provider);
    }

    [Fact]
    public async Task Soniox_configuration_binding_does_not_send_duplicate_default_language_hint()
    {
        var settings = Settings(); settings["AI:SpeechToText:Options:LanguageHints:0"] = "fa";
        using var services = Services(settings);
        var route = services.GetRequiredService<IOptions<AiRoutingOptions>>().Value.SpeechToText;
        using var socket = new Socket("""{"tokens":[],"finished":true}""");
        using var audio = new MemoryStream([1]);
        await SonioxSpeechToTextService.TranscribeSessionAsync(socket, audio, route, default);
        using var config = JsonDocument.Parse(socket.Sent[0].Bytes);
        Assert.Equal(new[] { "fa" }, config.RootElement.GetProperty("language_hints").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Soniox_realtime_emits_interim_and_final_separately_without_persisting_interim_as_result()
    {
        using var socket=new Socket("""{"tokens":[{"text":"موقت","is_final":false}]}""",
            """{"tokens":[{"text":"قطعی","is_final":true},{"text":" ادامه","is_final":false}]}""",
            """{"tokens":[{"text":" نهایی","is_final":true}],"finished":true}""");
        var updates=new List<SpeechTranscriptUpdate>();using var audio=new MemoryStream([1,2]);
        var result=await SonioxSpeechToTextService.TranscribeSessionAsync(socket,audio,new(),default,(value,_)=>{updates.Add(value);return Task.CompletedTask;});
        Assert.Equal("قطعی نهایی",result.Text);Assert.Equal(new SpeechTranscriptUpdate("","موقت"),updates[0]);
        Assert.Equal(new SpeechTranscriptUpdate("قطعی"," ادامه"),updates[1]);Assert.Equal(new SpeechTranscriptUpdate("قطعی نهایی",""),updates[2]);
    }

    [Fact]
    public async Task Soniox_waiting_for_finished_obeys_cancellation()
    {
        using var socket = new Socket(); using var audio = new MemoryStream([1]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SonioxSpeechToTextService.TranscribeSessionAsync(socket, audio, new(), timeout.Token));
    }
}
