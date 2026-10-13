using System.Diagnostics;
using System.Net;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HireYar.Tests;

public sealed class FfmpegFactAttribute : FactAttribute
{
    public FfmpegFactAttribute() { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HIREYAR_TEST_FFMPEG"))) Skip = "Set HIREYAR_TEST_FFMPEG for real PCM encoding and decoding; no codec substitute is used."; }
}

public sealed class GeminiSpeechTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return action(request, ct); }
    }
    private sealed class Encoder : IPcmSpeechEncoder
    {
        public int Probes; public int Calls; public decimal Speed; public byte[]? Input; public bool FailProbe;
        public Task EnsureAvailableAsync(CancellationToken ct) { Probes++; if (FailProbe) throw new InterviewProviderException("tts_encoder_unavailable"); return Task.CompletedTask; }
        public Task<byte[]> EncodeAsync(byte[] pcm, decimal speed, CancellationToken ct) { Calls++; Input = pcm; Speed = speed; return Task.FromResult("ID3-synthetic-converted"u8.ToArray()); }
    }
    private static LiaraClient Client(Handler handler, string model, IPcmSpeechEncoder? encoder, decimal speed = 1) =>
        new(new HttpClient(handler), Options.Create(new LiaraOptions { BaseUrl = "https://liara.example.test/v1", ApiKey = "synthetic-key",
            TtsModel = model, TtsVoice = "Aoede", TtsSpeed = speed }), NullLogger<LiaraClient>.Instance, encoder);

    [Theory]
    [InlineData("google/gemini-3.8-flash-tts")]
    [InlineData("google/gemini-3.8-flash-lite-tts")]
    [InlineData("google/gemini-3.1-flash-tts-preview")]
    public async Task Liara_Gemini_requests_documented_PCM_payload_and_returns_encoded_MP3(string model)
    {
        var pcm = new byte[] { 0, 1, 2, 3 }; var encoder = new Encoder();
        var handler = new Handler(async (request, ct) => {
            Assert.Equal("/v1/audio/speech", request.RequestUri!.AbsolutePath);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)); var root = json.RootElement;
            Assert.Equal(model, root.GetProperty("model").GetString()); Assert.Equal("Aoede", root.GetProperty("voice").GetString());
            Assert.Equal("pcm", root.GetProperty("response_format").GetString()); Assert.False(root.TryGetProperty("speed", out _));
            Assert.Equal("متن مصنوعی", root.GetProperty("input").GetString());
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(pcm) };
        });
        var client = Client(handler, model, encoder, 1.5m);
        var result = await client.GenerateAsync("متن مصنوعی", default);
        Assert.Equal("audio/mpeg", result.ContentType); Assert.Equal("ID3-synthetic-converted"u8.ToArray(), result.Audio);
        Assert.Equal(1, encoder.Probes); Assert.Equal(pcm, encoder.Input); Assert.Equal(1.5m, encoder.Speed);
        Assert.Equal(1.5m, client.Profile.Speed);
    }

    [Theory] [InlineData("openai/tts-1-hd")] [InlineData("openai/gpt-4o-mini-tts")]
    public async Task Liara_other_models_keep_MP3_payload_and_do_not_need_an_encoder(string model)
    {
        var encoder = new Encoder { FailProbe = true };
        var handler = new Handler(async (request, ct) => {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("mp3", json.RootElement.GetProperty("response_format").GetString());
            Assert.Equal(1.25m, json.RootElement.GetProperty("speed").GetDecimal());
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent("ID3-legacy"u8.ToArray()) };
        });
        Assert.Equal("audio/mpeg", (await Client(handler, model, encoder, 1.25m).GenerateAsync("synthetic", default)).ContentType);
        Assert.Equal(0, encoder.Probes); Assert.Equal(0, encoder.Calls);
    }

    [Fact]
    public async Task Gemini_startup_check_validates_codec_and_fails_on_missing_runtime()
    {
        var encoder = new Encoder(); var startup = new GeminiSpeechStartupCheck(encoder);
        await startup.StartAsync(default); Assert.Equal(1, encoder.Probes);
        encoder.FailProbe = true;
        Assert.Equal("tts_encoder_unavailable", (await Assert.ThrowsAsync<InterviewProviderException>(() => startup.StartAsync(default))).Code);
        await startup.StopAsync(default);
    }

    [Fact]
    public async Task Missing_Gemini_encoder_fails_before_any_provider_call()
    {
        var handler = new Handler((_, _) => throw new InvalidOperationException("A chargeable call must not happen."));
        foreach (var encoder in new IPcmSpeechEncoder?[] { null, new Encoder { FailProbe = true } }) {
            var error = await Assert.ThrowsAsync<InterviewProviderException>(() => Client(handler, "google/gemini-3.8-flash-tts", encoder).GenerateAsync("synthetic", default));
            Assert.Equal("tts_encoder_unavailable", error.Code);
        }
        Assert.Equal(0, handler.Calls);
    }

    [Theory] [InlineData("json")] [InlineData("text")] [InlineData("empty")] [InlineData("odd")]
    public async Task Malformed_Gemini_audio_is_rejected_before_encoding(string kind)
    {
        var encoder = new Encoder();
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = kind switch {
                "json" => new StringContent("{\"error\":\"synthetic confidential detail\"}", System.Text.Encoding.UTF8, "application/json"),
                "text" => new StringContent("synthetic upstream error"),
                "empty" => new ByteArrayContent([]), _ => new ByteArrayContent([0, 1, 2])
            }
        }));
        var error = await Assert.ThrowsAsync<InterviewProviderException>(() => Client(handler, "google/gemini-3.8-flash-tts", encoder).GenerateAsync("synthetic", default));
        Assert.Equal("tts_invalid_response", error.Code); Assert.Equal(0, encoder.Calls); Assert.DoesNotContain("confidential", error.Message);
    }

    [Fact]
    public async Task NetArz_keeps_its_previous_MP3_wire_protocol()
    {
        var handler = new Handler(async (request, ct) => {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("mp3", json.RootElement.GetProperty("response_format").GetString());
            Assert.Equal("nova", json.RootElement.GetProperty("voice").GetString());
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent("ID3-netarz"u8.ToArray()) };
        });
        var client = new NetArzClient(new HttpClient(handler), Options.Create(new NetArzOptions { ApiKey = "synthetic", TtsSpeed = 1.25m }), NullLogger<NetArzClient>.Instance);
        Assert.Equal("audio/mpeg", (await client.GenerateAsync("synthetic", default)).ContentType);
    }

    [Fact]
    public async Task Oversized_Gemini_response_does_not_reach_encoder()
    {
        var encoder = new Encoder();
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[FfmpegPcmSpeechEncoder.MaxBytes + 2]) }));
        var error = await Assert.ThrowsAsync<InterviewProviderException>(() => Client(handler, "google/gemini-3.8-flash-tts", encoder).GenerateAsync("synthetic", default));
        Assert.Equal("tts_response_too_large", error.Code); Assert.Equal(0, encoder.Calls);
    }

    [Fact]
    public async Task Encoder_invalid_input_missing_binary_and_cancellation_are_safe()
    {
        using var encoder = new FfmpegPcmSpeechEncoder(Options.Create(new SpeechEncodingOptions { FfmpegPath = "missing-synthetic-ffmpeg-executable" }));
        foreach (var pcm in new byte[][] { [], [0], new byte[FfmpegPcmSpeechEncoder.MaxBytes + 2] })
            Assert.Equal("tts_invalid_response", (await Assert.ThrowsAsync<InterviewProviderException>(() => encoder.EncodeAsync(pcm, 1, default))).Code);
        Assert.Equal("tts_encoder_unavailable", (await Assert.ThrowsAsync<InterviewProviderException>(() => encoder.EnsureAvailableAsync(default))).Code);
        using var stopped = new CancellationTokenSource(); stopped.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => encoder.EncodeAsync([0, 0], 1, stopped.Token));
    }

    private static byte[] Tone()
    {
        var pcm = new byte[48000];
        for (var sample = 0; sample < 24000; sample++) System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(sample * 2, 2), (short)(4000 * Math.Sin(2 * Math.PI * 440 * sample / 24000)));
        return pcm;
    }

    [FfmpegFact]
    public async Task Real_encoder_output_decodes_to_PCM_at_supported_speeds()
    {
        var executable = Environment.GetEnvironmentVariable("HIREYAR_TEST_FFMPEG")!;
        using var encoder = new FfmpegPcmSpeechEncoder(Options.Create(new SpeechEncodingOptions { FfmpegPath = executable }));
        await encoder.EnsureAvailableAsync(default);
        foreach (var speed in new[] { 0.25m, 1m, 1.5m, 4m }) {
            var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Tone()) }));
            var mp3 = (await Client(handler, "google/gemini-3.8-flash-tts", encoder, speed).GenerateAsync("متن مصنوعی", default)).Audio;
            using var process = new Process { StartInfo = new() { FileName = executable, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true } };
            foreach (var argument in new[] { "-v", "error", "-f", "mp3", "-i", "pipe:0", "-f", "s16le", "-ar", "24000", "-ac", "1", "pipe:1" }) process.StartInfo.ArgumentList.Add(argument);
            Assert.True(process.Start()); using var decoded = new MemoryStream(); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var write = Task.Run(async () => { await process.StandardInput.BaseStream.WriteAsync(mp3, deadline.Token); process.StandardInput.Close(); });
            await Task.WhenAll(write, process.StandardOutput.BaseStream.CopyToAsync(decoded, deadline.Token),
                process.StandardError.BaseStream.CopyToAsync(Stream.Null, deadline.Token), process.WaitForExitAsync(deadline.Token));
            Assert.Equal(0, process.ExitCode);
            // MP3 encoder delay/padding is an absolute number of samples, especially visible on short sped-up speech.
            Assert.InRange(decoded.Length / 48000m, 0.8m / speed, 1.2m / speed + 0.1m);
            Assert.Contains(decoded.ToArray(), value => value != 0);
        }
    }

    [FfmpegFact]
    public async Task Real_encoder_cancellation_stops_work_and_releases_capacity()
    {
        using var encoder = new FfmpegPcmSpeechEncoder(Options.Create(new SpeechEncodingOptions { FfmpegPath = Environment.GetEnvironmentVariable("HIREYAR_TEST_FFMPEG")! }));
        await encoder.EnsureAvailableAsync(default);
        using var stopped = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => encoder.EncodeAsync(new byte[FfmpegPcmSpeechEncoder.MaxBytes], 0.25m, stopped.Token));
        Assert.True((await encoder.EncodeAsync(Tone(), 1, default)).Length > 100);
    }

    [FfmpegFact]
    public async Task Real_encoder_deadline_bounds_long_conversion()
    {
        using var encoder = new FfmpegPcmSpeechEncoder(Options.Create(new SpeechEncodingOptions {
            FfmpegPath = Environment.GetEnvironmentVariable("HIREYAR_TEST_FFMPEG")!, TimeoutSeconds = 1 }));
        var watch = Stopwatch.StartNew();
        var error = await Assert.ThrowsAsync<InterviewProviderException>(() => encoder.EncodeAsync(new byte[FfmpegPcmSpeechEncoder.MaxBytes], 0.25m, default));
        Assert.Contains(error.Code, new[] { "tts_encoding_timeout", "tts_encoded_response_too_large" });
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.True((await encoder.EncodeAsync(Tone(), 1, default)).Length > 100);
    }

    [FfmpegFact]
    public async Task Real_encoder_caps_output_and_can_be_reused_after_process_cleanup()
    {
        using var encoder = new FfmpegPcmSpeechEncoder(Options.Create(new SpeechEncodingOptions { FfmpegPath = Environment.GetEnvironmentVariable("HIREYAR_TEST_FFMPEG")! }));
        var error = await Assert.ThrowsAsync<InterviewProviderException>(() => encoder.EncodeAsync(new byte[FfmpegPcmSpeechEncoder.MaxBytes], 0.25m, default));
        Assert.Equal("tts_encoded_response_too_large", error.Code);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => encoder.EncodeAsync(Tone(), 1, default)));
        Assert.All(results, result => Assert.True(result.Length > 100));
    }
}
