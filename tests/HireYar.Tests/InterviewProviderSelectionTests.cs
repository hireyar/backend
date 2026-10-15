using System.Net;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HireYar.Tests;

public sealed class InterviewProviderSelectionTests
{
    private static ServiceProvider Services(string selected, string? key = "synthetic-selected-key", int lease = 300)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AI:Provider"] = selected,
            [$"AI:{selected}:ApiKey"] = key,
            [$"AI:{selected}:BaseUrl"] = "https://selected.example.test/api/workspace/v1",
            [$"AI:{selected}:SttTimeoutSeconds"] = "120",
            [$"AI:{selected}:LlmTimeoutSeconds"] = "120",
            ["AI:NetArz:Provider"] = "untrusted-provider-label",
            ["InterviewAudio:LeaseSeconds"] = lease.ToString()
        };
        // Unselected invalid settings must not affect startup or selection.
        var other = selected == "NetArz" ? "Liara" : "NetArz";
        settings[$"AI:{other}:BaseUrl"] = "http://invalid";
        settings[$"AI:{other}:ApiKey"] = "";
        settings[$"AI:{other}:SttTimeoutSeconds"] = "999";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInterviews(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("NetArz", typeof(NetArzClient))]
    [InlineData("Liara", typeof(LiaraClient))]
    [InlineData("Disabled", typeof(DisabledInterviewProvider))]
    public void All_capabilities_resolve_selected_implementation(string selected, Type expected)
    {
        using var services = Services(selected);
        Assert.IsType(expected, services.GetRequiredService<ISpeechToTextService>());
        Assert.IsType(expected, services.GetRequiredService<IInterviewDecisionService>());
        Assert.IsType(expected, services.GetRequiredService<ITextToSpeechService>());
        Assert.IsType(expected, services.GetRequiredService<IQuestionSuggestionProvider>());
        Assert.NotNull(services.GetRequiredService<IOptions<InterviewOptions>>().Value);
    }

    [Theory]
    [InlineData("NetArz")]
    [InlineData("Liara")]
    public void Selected_provider_requires_its_own_key_and_lease(string selected)
    {
        using var missing = Services(selected, "");
        Assert.Throws<OptionsValidationException>(() => missing.GetRequiredService<ISpeechToTextService>());
        using var shortLease = Services(selected, lease: 270);
        Assert.Throws<OptionsValidationException>(() => shortLease.GetRequiredService<IOptions<InterviewOptions>>().Value);
    }

    [Fact]
    public void Unknown_provider_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => Services("Unknown"));
    }

    [Fact]
    public async Task Disabled_fails_without_http_or_valid_provider_configuration()
    {
        using var services = Services("Disabled", lease: 60);
        Assert.NotNull(services.GetRequiredService<IOptions<InterviewOptions>>().Value);
        var error = await Assert.ThrowsAsync<InterviewProviderException>(() =>
            services.GetRequiredService<ITextToSpeechService>().GenerateAsync("synthetic", default));
        Assert.Equal("provider_not_configured", error.Code);
        Assert.Equal("provider_not_configured", (await Assert.ThrowsAsync<InterviewProviderException>(() =>
            services.GetRequiredService<IQuestionSuggestionProvider>().SuggestAsync(new("test", "test", 5, null), default))).Code);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("liara.example.test", request.RequestUri!.Host);
            Assert.Equal("synthetic-liara-key", request.Headers.Authorization!.Parameter);
            Paths.Add(request.RequestUri.AbsolutePath);
            var payload = await request.Content!.ReadAsStringAsync(ct);
            Assert.Contains("liara-", payload);
            if (request.RequestUri.AbsolutePath.EndsWith("/audio/transcriptions"))
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"text\":\"synthetic transcript\"}") };
            if (request.RequestUri.AbsolutePath.EndsWith("/audio/speech"))
            {
                using var json = JsonDocument.Parse(payload);
                Assert.Equal("nova", json.RootElement.GetProperty("voice").GetString());
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent("ID3synthetic"u8.ToArray()) };
            }
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}") };
        }
    }

    [Fact]
    public async Task Liara_uses_its_own_credentials_models_paths_and_result_identity()
    {
        var handler = new Handler();
        var client = new LiaraClient(new HttpClient(handler), Options.Create(new LiaraOptions
        {
            BaseUrl = "https://liara.example.test/api/workspace/v1",
            ApiKey = "synthetic-liara-key", SttModel = "liara-stt", LlmModel = "liara-llm", TtsModel = "liara-tts"
        }), NullLogger<LiaraClient>.Instance);
        using var audio = new MemoryStream(new byte[16]);
        var transcription = await client.TranscribeAsync(audio, "test.wav", "audio/wav", default);
        Assert.Equal("Liara", transcription.Provider);
        Assert.Equal("liara-stt", transcription.Model);
        var decision = await client.EvaluateAsync(new("test", Guid.NewGuid(), "ProcessingAnswer", 0, 5, 60,
            new(Guid.NewGuid(), Guid.NewGuid(), "test", "test", 3, true), "test", [], [], [], []), default);
        Assert.Equal("Liara", decision.Provider);
        Assert.Equal("liara-llm", decision.Model);
        Assert.Equal("{}", await client.SuggestAsync(new("test", "test", 5, null), default));
        Assert.Equal("audio/mpeg", (await client.GenerateAsync("test", default)).ContentType);
        Assert.Equal("liara-tts", client.Profile.Model);
        Assert.Equal(new[] { "/api/workspace/v1/audio/transcriptions", "/api/workspace/v1/chat/completions",
            "/api/workspace/v1/chat/completions", "/api/workspace/v1/audio/speech" }, handler.Paths);
    }
}
