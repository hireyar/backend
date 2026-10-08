using System.Net;
using System.Text.Json;
using System.Text;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace HireYar.Tests;

public sealed class InterviewProviderTests
{
    private sealed class CaptureLogger : ILogger<NetArzClient>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, error) + (error?.Message ?? ""));
    }
    private sealed class BrokenResponseStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            ValueTask.FromException<int>(new IOException("Synthetic sensitive response stream failure"));
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
    private static InterviewDecisionContext Context()
    {
        var skill = Guid.NewGuid(); var q = new InterviewQuestionSnapshot(Guid.NewGuid(), skill, "سؤال", "indexes", 3, true);
        return new("Backend", q.QuestionId, "ProcessingAnswer", 0, 12, 1200, q, "پاسخ", [new(skill, "SQL", true, 100)], [], [], [q]);
    }
    private static NetArzClient Client(Handler handler, int seconds = 1) => new(new HttpClient(handler), Options.Create(new NetArzOptions {
        ApiKey = "synthetic-test-key", SttTimeoutSeconds = seconds, LlmTimeoutSeconds = seconds, TtsTimeoutSeconds = seconds
    }), NullLogger<NetArzClient>.Instance);
    private static Task Invoke(NetArzClient client, string operation) => operation switch {
        "stt" => client.TranscribeAsync(new MemoryStream(new byte[16]), "answer.wav", "audio/wav", default),
        "llm" => client.EvaluateAsync(Context(), default), _ => client.GenerateAsync("سؤال", default)
    };
    [Theory]
    [InlineData("stt", 401)] [InlineData("stt", 429)] [InlineData("stt", 500)]
    [InlineData("llm", 401)] [InlineData("llm", 429)] [InlineData("llm", 500)]
    [InlineData("tts", 401)] [InlineData("tts", 429)] [InlineData("tts", 500)]
    public async Task Provider_status_is_safe_and_typed(string operation, int status)
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("sensitive upstream details") }));
        var exception = await Assert.ThrowsAsync<InterviewProviderException>(() => Invoke(Client(handler), operation));
        Assert.Equal($"{operation}_http_{status}", exception.Code); Assert.DoesNotContain("sensitive", exception.Message);
    }
    [Theory] [InlineData("stt")] [InlineData("llm")] [InlineData("tts")]
    public async Task Broken_response_stream_is_safe_and_typed(string operation)
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StreamContent(new BrokenResponseStream())
        }));
        var error = await Assert.ThrowsAsync<InterviewProviderException>(() => Invoke(Client(handler), operation));
        Assert.Equal(operation + "_unavailable", error.Code);
        Assert.DoesNotContain("sensitive", error.Message);
    }
    [Theory] [InlineData("stt")] [InlineData("llm")] [InlineData("tts")]
    public async Task Provider_network_failures_are_typed_and_logs_exclude_sensitive_content(string operation)
    {
        var handler = new Handler((_, _) => throw new HttpRequestException("sensitive-provider-body"));
        var logger = new CaptureLogger();
        var client = new NetArzClient(new HttpClient(handler), Options.Create(new NetArzOptions { ApiKey = "synthetic-secret-key" }), logger);
        var error = await Assert.ThrowsAsync<InterviewProviderException>(() => Invoke(client, operation));
        Assert.Equal(operation + "_unavailable", error.Code);
        var log = Assert.Single(logger.Lines);
        foreach (var sensitive in new[] { "synthetic-secret-key", "sensitive-provider-body", "Authorization", "پاسخ", InterviewDecisionPrompt.System })
            Assert.DoesNotContain(sensitive, log);
        Assert.Contains("network_failure", log);
    }
    [Theory] [InlineData("stt")] [InlineData("llm")] [InlineData("tts")]
    public async Task Provider_timeouts_are_typed(string operation)
    {
        var handler = new Handler(async (_, ct) => { await Task.Delay(Timeout.InfiniteTimeSpan, ct); return new(HttpStatusCode.OK); });
        var error = await Assert.ThrowsAsync<InterviewProviderException>(() => Invoke(Client(handler), operation));
        Assert.Equal(operation + "_timeout", error.Code);
    }
    [Theory] [InlineData("stt")] [InlineData("llm")] [InlineData("tts")]
    public async Task Caller_cancellation_is_preserved(string operation)
    {
        var handler = new Handler(async (_, ct) => { await Task.Delay(Timeout.InfiniteTimeSpan, ct); return new(HttpStatusCode.OK); });
        using var ct = new CancellationTokenSource(); ct.Cancel(); var client = Client(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
            if (operation == "stt") await client.TranscribeAsync(new MemoryStream(new byte[16]), "a.wav", "audio/wav", ct.Token);
            else if (operation == "llm") await client.EvaluateAsync(Context(), ct.Token);
            else await client.GenerateAsync("Question", ct.Token);
        });
    }
    [Fact]
    public async Task Stt_uses_farsi_multipart_and_parses_text()
    {
        var handler = new Handler(async (request, ct) => {
            Assert.EndsWith("/audio/transcriptions", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            var body = await request.Content!.ReadAsStringAsync(ct); Assert.Contains("gpt-transcribe", body); Assert.Contains("fa", body);
            Assert.Contains("response_format", body); Assert.Contains("json", body);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"text\":\"پاسخ آزمایشی\"}", Encoding.UTF8, "application/json") };
        });
        var result = await Client(handler).TranscribeAsync(new MemoryStream(new byte[16]), "a.wav", "audio/wav", default);
        Assert.Equal("پاسخ آزمایشی", result.Text);
    }
    [Theory] [InlineData("{}")] [InlineData("{\"text\":null}")] [InlineData("invalid")]
    public async Task Stt_invalid_response_is_rejected(string json)
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }));
        Assert.Equal("stt_invalid_response", (await Assert.ThrowsAsync<InterviewProviderException>(() => Invoke(Client(handler), "stt"))).Code);
    }
    [Theory]
    [InlineData("not-json-sensitive", "invalid_json")]
    [InlineData("{}", "missing_text")]
    [InlineData("{\"text\":null}", "text_not_string")]
    [InlineData("{\"text\":\" \"}", "empty_text")]
    public async Task Stt_shape_diagnostics_exclude_response_content(string body, string reason)
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }));
        var logger = new CaptureLogger();
        var client = new NetArzClient(new HttpClient(handler), Options.Create(new NetArzOptions { ApiKey = "synthetic-secret" }), logger);
        Assert.Equal("stt_invalid_response", (await Assert.ThrowsAsync<InterviewProviderException>(() => Invoke(client, "stt"))).Code);
        Assert.Contains(logger.Lines, x => x.Contains("reason=" + reason));
        Assert.DoesNotContain(logger.Lines, x => x.Contains("synthetic-secret") || x.Contains("not-json-sensitive"));
    }
    [Fact]
    public async Task Llm_extracts_message_content_and_sends_versioned_prompt()
    {
        var handler = new Handler(async (request, ct) => {
            Assert.EndsWith("/chat/completions", request.RequestUri!.AbsolutePath);
            var text = await request.Content!.ReadAsStringAsync(ct); Assert.Contains("availableQuestions", text); Assert.Contains("Never invent main questions", text);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}") };
        });
        Assert.Equal("{}", (await Client(handler).EvaluateAsync(Context(), default)).Json);
    }
    [Fact]
    public async Task Llm_sends_invitation_prompt_as_system_and_resume_job_bank_and_dialogue_as_untrusted_context()
    {
        var context = Context() with { MasterPrompt = "Synthetic immutable invitation master", PositionDescription = "Synthetic job description",
            ResumeText = "Synthetic resume projects", CandidateIntroduction = "Synthetic introduction", InterviewType = "general", Language = "fa-IR",
            QuestionBank = [Context().Question], PreviousAnswers = [new(Context().CurrentQuestionId, "Synthetic previous question", "Synthetic corrected answer", "Synthetic clarification")] };
        var handler = new Handler(async (request, ct) => {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var messages = body.RootElement.GetProperty("messages");
            Assert.Equal("system", messages[0].GetProperty("role").GetString());
            Assert.StartsWith(context.MasterPrompt, messages[0].GetProperty("content").GetString());
            using var user = JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);
            Assert.Equal(context.PositionDescription, user.RootElement.GetProperty("positionDescription").GetString());
            Assert.Equal(context.ResumeText, user.RootElement.GetProperty("resumeText").GetString());
            Assert.Equal(context.CandidateIntroduction, user.RootElement.GetProperty("candidateIntroduction").GetString());
            Assert.Equal("general", user.RootElement.GetProperty("interviewType").GetString());
            Assert.Equal(1, user.RootElement.GetProperty("questionBank").GetArrayLength());
            Assert.Equal(1, user.RootElement.GetProperty("previousAnswers").GetArrayLength());
            Assert.Equal(JsonValueKind.Null, user.RootElement.GetProperty("masterPrompt").ValueKind);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}") };
        });
        await Client(handler).EvaluateAsync(context, default);
    }
    [Theory] [InlineData("{}")] [InlineData("{\"choices\":[]}")] [InlineData("not-json")]
    public async Task Llm_invalid_envelope_is_rejected(string json)
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }));
        Assert.Equal("llm_invalid_response", (await Assert.ThrowsAsync<InterviewProviderException>(() => Invoke(Client(handler), "llm"))).Code);
    }
    [Fact]
    public async Task Tts_returns_audio_and_rejects_json_disguised_as_success()
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("ID3-test-audio"u8.ToArray()) }));
        Assert.Equal("audio/mpeg", (await Client(handler).GenerateAsync("Question", default)).ContentType);
        var bad = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"error\":\"bad\"}") }));
        await Assert.ThrowsAsync<InterviewProviderException>(() => Invoke(Client(bad), "tts"));
    }
    [Fact]
    public void Token_hashes_and_audio_cache_hashes_are_stable_and_change_with_inputs()
    {
        var token = InterviewTokens.Create(); Assert.True(InterviewTokens.Matches(token, InterviewTokens.Hash(token)));
        Assert.False(InterviewTokens.Matches(InterviewTokens.Create(), InterviewTokens.Hash(token)));
        var profile = new SpeechProfile("test-tts", "voice", 1m);
        Assert.Equal(QuestionAudioService.Hash(" text ", profile), QuestionAudioService.Hash("text", profile));
        Assert.NotEqual(QuestionAudioService.Hash("text", profile), QuestionAudioService.Hash("changed", profile));
        Assert.NotEqual(QuestionAudioService.Hash("text", profile), QuestionAudioService.Hash("text", profile with { Speed = 1.1m }));
    }
    [Fact]
    public async Task Disabled_provider_never_calls_http_even_with_a_configured_key()
    {
        var handler = new Handler((_, _) => throw new InvalidOperationException("Disabled provider must not make HTTP requests."));
        var client = new NetArzClient(new HttpClient(handler), Options.Create(new NetArzOptions { Enabled = false, ApiKey = "synthetic-test-key" }), NullLogger<NetArzClient>.Instance);
        Assert.Equal("provider_not_configured", (await Assert.ThrowsAsync<InterviewProviderException>(() => client.GenerateAsync("Question", default))).Code);
    }
    [Fact]
    public async Task Suggestions_send_JD_as_data_and_return_only_inner_JSON_without_logging_it()
    {
        var logger = new CaptureLogger();
        const string result = "{\"topics\":[]}";
        var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal("/v1/chat/completions", request.RequestUri!.AbsolutePath);
            using var body = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("json_object", body.RootElement.GetProperty("response_format").GetProperty("type").GetString());
            var messages = body.RootElement.GetProperty("messages");
            Assert.Equal("system", messages[0].GetProperty("role").GetString());
            Assert.Contains("untrusted", messages[0].GetProperty("content").GetString());
            using var data = System.Text.Json.JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);
            Assert.Equal("Synthetic confidential JD", data.RootElement.GetProperty("positionDescription").GetString());
            Assert.Equal(7, data.RootElement.GetProperty("topicCount").GetInt32());
            return new(HttpStatusCode.OK) { Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = result } } } })) };
        });
        var client = new NetArzClient(new HttpClient(handler), Options.Create(new NetArzOptions { ApiKey = "synthetic-key", BaseUrl = "https://provider.example.test/v1" }), logger);
        Assert.Equal(result, await client.SuggestAsync(new("Synthetic job", "Synthetic confidential JD", 7, null), default));
        Assert.DoesNotContain("Synthetic confidential JD", string.Join("", logger.Lines));
        Assert.DoesNotContain("synthetic-key", string.Join("", logger.Lines));
    }
    [Theory]
    [InlineData("{}")][InlineData("{\"choices\":[]}")][InlineData("not-json")]
    public async Task Suggestions_reject_invalid_upstream_envelopes(string json)
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }));
        Assert.Equal("llm_invalid_response", (await Assert.ThrowsAsync<InterviewProviderException>(() => Client(handler).SuggestAsync(new("Synthetic", "Synthetic JD", 5, null), default))).Code);
    }
}
