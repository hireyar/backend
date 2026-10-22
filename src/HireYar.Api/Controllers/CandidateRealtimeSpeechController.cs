using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using HireYar.Api.Security;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/candidate/interviews/{interviewId:guid}/speech")]
[Authorize(AuthenticationSchemes = InterviewAuthenticationHandler.SchemeName), EnableRateLimiting("candidate")]
public sealed class CandidateRealtimeSpeechController(CandidateInterviewService interviews, ISpeechToTextService stt,
    IRealtimeSpeechReceipt receipts, InterviewOptions limits, IOptions<SecurityOptions> security) : ControllerBase
{
    private static readonly ConcurrentDictionary<Guid, byte> Active = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Start(string Type, string Purpose, string ClientTurnId, Guid? QuestionId);
    [HttpGet("capabilities"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<SpeechCapabilitiesDto> Capabilities(Guid interviewId, CancellationToken ct) => interviews.SpeechCapabilitiesAsync(interviewId, ct);

    [HttpGet("stream")]
    public async Task Stream(Guid interviewId, CancellationToken ct)
    {
        if (stt is not IRealtimeSpeechToTextService realtime) throw new AppException(409, "realtime_not_supported", "پروایدر فعلی تبدیل زنده را پشتیبانی نمی‌کند.");
        if (!HttpContext.WebSockets.IsWebSocketRequest || !HttpContext.WebSockets.WebSocketRequestedProtocols.Contains("hireyar-stt-v1"))
            throw AppException.Invalid("realtime_websocket_required");
        var expected = new Uri(security.Value.FrontendUrl).GetLeftPart(UriPartial.Authority);
        if (!Uri.TryCreate(Request.Headers.Origin.ToString(), UriKind.Absolute, out var origin) ||
            !string.Equals(origin.GetLeftPart(UriPartial.Authority), expected, StringComparison.OrdinalIgnoreCase))
            throw new AppException(403, "realtime_origin_invalid", "مبدأ اتصال مجاز نیست.");
        await interviews.SpeechCapabilitiesAsync(interviewId, ct);
        if (!Active.TryAdd(interviewId, 0)) throw new AppException(409, "realtime_already_connected", "یک اتصال صوتی دیگر فعال است.");
        try {
            using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync("hireyar-stt-v1");
            try { await Run(socket, interviewId, realtime, ct); }
            catch (Exception e) when (e is AppException or InterviewProviderException or OperationCanceledException or TimeoutException or WebSocketException or JsonException) {
                var code = e is AppException app ? app.Code : e is InterviewProviderException provider ? provider.Code : "realtime_interrupted";
                InterviewTrace.Emit("processing", "realtime", "failed", "اتصال تبدیل زنده کامل نشد", failureCode: code);
                if (socket.State == WebSocketState.Open && !ct.IsCancellationRequested) {
                    using var end = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    try {
                        await Send(socket, new { type = "error", code, fallback = "batch", message = "اتصال زنده کامل نشد؛ فایل ضبط‌شده را حفظ کنید." }, end.Token);
                        await socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, "realtime_interrupted", end.Token);
                    }
                    catch (Exception failure) when (failure is WebSocketException or OperationCanceledException) { }
                }
            }
            finally { if (socket.State is WebSocketState.Open or WebSocketState.Connecting) socket.Abort(); }
        } finally { Active.TryRemove(interviewId, out _); }
    }
    private async Task Run(WebSocket socket, Guid interviewId, IRealtimeSpeechToTextService realtime, CancellationToken ct)
    {
        using var startTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct); startTimeout.CancelAfter(TimeSpan.FromSeconds(10));
        var first = await Read(socket, 4096, startTimeout.Token);
        if (first.Type != WebSocketMessageType.Text) throw AppException.Invalid("realtime_start_required");
        var start = JsonSerializer.Deserialize<Start>(first.Bytes, Json) ?? throw AppException.Invalid("realtime_start_required");
        if (start.Type != "start") throw AppException.Invalid("realtime_start_required");
        var context = await interviews.RealtimeContextAsync(interviewId, start.Purpose, start.ClientTurnId, start.QuestionId, ct);
        var maxSeconds = start.Purpose == "audioCheck" ? 30 : Math.Min(1200, limits.MaxAnswerSeconds);
        var maxBytes = start.Purpose == "audioCheck" ? Math.Min(2 * 1024 * 1024, limits.MaxUploadBytes) : limits.MaxUploadBytes;
        using var session = CancellationTokenSource.CreateLinkedTokenSource(ct); session.CancelAfter(TimeSpan.FromSeconds(maxSeconds + realtime.FinalizationTimeout.TotalSeconds + 10));
        var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(8) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
        using var input = new ChannelAudioStream(channel.Reader);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long bytes = 0; var prefix = new List<byte>(4); var lastCheck = Environment.TickCount64;
        await Send(socket, new { type = "ready", maxSeconds, maxBytes, finalizationTimeoutSeconds = realtime.FinalizationTimeout.TotalSeconds, format = "webm", fallback = "batch" }, session.Token);
        var provider = realtime.TranscribeRealtimeAsync(input, (update, token) => Send(socket,
            new { type = "transcript", finalText = update.FinalText, interimText = update.InterimText }, token), session.Token);
        var started = Environment.TickCount64;
        var receiving = ReceiveAudio();
        try {
            if (await Task.WhenAny(receiving, provider) == provider && !receiving.IsCompletedSuccessfully) {
                await provider; throw AppException.Invalid("realtime_finished_early");
            }
            await receiving;
            var result = await provider.WaitAsync(realtime.FinalizationTimeout, ct);
            if (context != await interviews.RealtimeContextAsync(interviewId, start.Purpose, start.ClientTurnId, start.QuestionId, ct))
                throw AppException.Conflict();
            var receipt = receipts.Issue(context, result, Convert.ToHexString(hash.GetHashAndReset()), bytes);
            await Send(socket, new { type = "completed", finalText = result.Text, receipt, audioBytes = bytes }, ct);
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "completed", ct);
        } finally {
            await session.CancelAsync(); channel.Writer.TryComplete();
            try { await receiving; } catch { }
            try { await provider; } catch { }
        }
        async Task ReceiveAudio()
        {
            try {
                while (true) {
                    using var idle = CancellationTokenSource.CreateLinkedTokenSource(session.Token); idle.CancelAfter(TimeSpan.FromSeconds(15));
                    var packet = await Read(socket, 65536, idle.Token);
                    if (Environment.TickCount64 - started > maxSeconds * 1000L) throw AppException.Invalid("realtime_duration_exceeded");
                    if (Environment.TickCount64 - lastCheck > 5000) {
                        if (context != await interviews.RealtimeContextAsync(interviewId, start.Purpose, start.ClientTurnId, start.QuestionId, session.Token))
                            throw AppException.Conflict();
                        lastCheck = Environment.TickCount64;
                    }
                    if (packet.Type == WebSocketMessageType.Text) {
                        using var command = JsonDocument.Parse(packet.Bytes);
                        if (command.RootElement.ValueKind != JsonValueKind.Object || !command.RootElement.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "finish" || bytes < 12)
                            throw AppException.Invalid("realtime_finish_invalid");
                        return;
                    }
                    if (packet.Type != WebSocketMessageType.Binary || packet.Bytes.Length == 0) throw AppException.Invalid("realtime_audio_invalid");
                    bytes += packet.Bytes.Length;
                    if (bytes > maxBytes) throw new AppException(413, "audio_too_large", "حد مجاز صوت رد شده است.");
                    foreach (var value in packet.Bytes.Take(4 - prefix.Count)) prefix.Add(value);
                    if (prefix.Count == 4 && !prefix.SequenceEqual(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 })) throw AppException.Invalid("audio_invalid");
                    hash.AppendData(packet.Bytes);
                    await channel.Writer.WriteAsync(packet.Bytes, session.Token);
                }
            } finally { channel.Writer.TryComplete(); }
        }
    }
    private static Task Send(WebSocket socket, object value, CancellationToken ct) =>
        socket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(value, Json)), WebSocketMessageType.Text, true, ct);
    private static async Task<(WebSocketMessageType Type, byte[] Bytes)> Read(WebSocket socket, int maxBytes, CancellationToken ct)
    {
        var buffer = new byte[Math.Min(maxBytes, 16384)]; using var data = new MemoryStream(); WebSocketReceiveResult frame;
        WebSocketMessageType? type = null;
        do {
            frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (frame.MessageType == WebSocketMessageType.Close) throw new OperationCanceledException(ct);
            type ??= frame.MessageType;
            if (type != frame.MessageType || data.Length + frame.Count > maxBytes) throw AppException.Invalid("realtime_frame_invalid");
            data.Write(buffer, 0, frame.Count);
        } while (!frame.EndOfMessage);
        return (type.Value, data.ToArray());
    }
    private sealed class ChannelAudioStream(ChannelReader<byte[]> reader) : System.IO.Stream
    {
        private byte[] current = []; private int offset;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) {
            if (buffer.Length == 0) return 0;
            while (offset == current.Length) {
                if (!await reader.WaitToReadAsync(ct)) return 0;
                if (!reader.TryRead(out var next)) continue;
                current = next; offset = 0;
            }
            var count = Math.Min(buffer.Length, current.Length - offset); current.AsMemory(offset, count).CopyTo(buffer); offset += count; return count;
        }
        public override Task<int> ReadAsync(byte[] b, int o, int c, CancellationToken ct) => ReadAsync(b.AsMemory(o,c),ct).AsTask();
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] b,int o,int c) => throw new NotSupportedException(); public override void Flush() => throw new NotSupportedException();
        public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException(); public override void SetLength(long v)=>throw new NotSupportedException();
        public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
    }
}
