using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HireYar.Application.Interviews;
using Microsoft.Extensions.Logging;

namespace HireYar.Infrastructure.Interviews;

public sealed class SonioxSpeechToTextService(AiConnectionOptions connection, AiCapabilityOptions route, ILogger<SonioxSpeechToTextService> logger) : ISpeechToTextService, IRealtimeSpeechToTextService
{
    public TimeSpan FinalizationTimeout => TimeSpan.FromSeconds(route.TimeoutSeconds);
    public Task<SpeechTranscriptionResult> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken ct) =>
        TranscribeCoreAsync(audio, contentType, null, ct);
    public Task<SpeechTranscriptionResult> TranscribeRealtimeAsync(Stream audio,
        Func<SpeechTranscriptUpdate, CancellationToken, Task> update, CancellationToken ct) => TranscribeCoreAsync(audio, "audio/webm", update, ct);
    private async Task<SpeechTranscriptionResult> TranscribeCoreAsync(Stream audio, string contentType,
        Func<SpeechTranscriptUpdate, CancellationToken, Task>? update, CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", "Bearer " + connection.ApiKey);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (update is null) timeout.CancelAfter(TimeSpan.FromSeconds(route.TimeoutSeconds));
        var watch = Stopwatch.StartNew(); var outcome = "success"; string? failureCode = null; string? failureReason = null;
        var operationId = Guid.NewGuid().ToString("N");
        InterviewTrace.Emit("provider", "stt", "started", "تبدیل فایل صوتی به متن", operationId, "WebSocket", "transcribe-websocket", "Soniox", route.Model);
        try
        {
            using (var connect = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token)) {
                connect.CancelAfter(TimeSpan.FromSeconds(Math.Min(10, route.TimeoutSeconds)));
                await socket.ConnectAsync(new Uri(connection.WebSocketUrl), connect.Token);
            }
            return await TranscribeSessionAsync(socket, audio, route, timeout.Token, update);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { outcome = "timeout"; failureCode = "stt_timeout"; throw new InterviewProviderException(failureCode); }
        catch (OperationCanceledException) { outcome = "cancelled"; failureCode = "stt_cancelled"; throw; }
        catch (Exception e) when (e is WebSocketException or HttpRequestException or IOException) { outcome = "network_failure"; failureCode = "stt_unavailable"; throw new InterviewProviderException(failureCode); }
        catch (JsonException) { outcome = "failure"; failureCode = "stt_invalid_response"; throw new InterviewProviderException(failureCode); }
        catch (InterviewProviderException e) { outcome = "failure"; failureCode = e.Code; failureReason = e.Reason; throw; }
        finally
        {
            socket.Abort();
            InterviewMetrics.SttDuration.Record(watch.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("provider", "Soniox"));
            if (outcome != "success") InterviewMetrics.SttFailure.Add(1);
            InterviewTrace.Emit("provider", "stt", outcome, "تبدیل فایل صوتی به متن", operationId, "WebSocket", "transcribe-websocket", "Soniox", route.Model,
                watch.Elapsed.TotalMilliseconds, null, failureCode);
            logger.LogInformation("Interview provider operation=stt provider=Soniox model={Model} status={Status} failureCode={FailureCode} failureReason={FailureReason} audioFormat={AudioFormat} traceId={TraceId} durationMs={DurationMs}", route.Model, outcome, failureCode, failureReason, AudioFormat(contentType), Activity.Current?.Id, watch.Elapsed.TotalMilliseconds);
        }
    }

    internal static async Task<SpeechTranscriptionResult> TranscribeSessionAsync(WebSocket socket, Stream audio, AiCapabilityOptions route, CancellationToken ct, Func<SpeechTranscriptUpdate, CancellationToken, Task>? update = null)
    {
        using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
        // Configuration binding appends configured hints to the initialized default ["fa"].
        // Soniox rejects duplicates with HTTP 400 ("Language hints must be unique.").
        var config = JsonSerializer.SerializeToUtf8Bytes(new { model = route.Model, audio_format = "auto", language_hints = route.Options.LanguageHints.Distinct(StringComparer.Ordinal).ToArray() });
        await socket.SendAsync(config.AsMemory(), WebSocketMessageType.Text, true, session.Token);
        var send = SendAudioAsync(socket, audio, session.Token);
        var receive = ReceiveTranscriptAsync(socket, session.Token, update);
        try
        {
            if (await Task.WhenAny(send, receive) == send) await send;
            var result = await receive;
            await send;
            return new(result, "Soniox", route.Model);
        }
        finally
        {
            await session.CancelAsync();
            try { await send; } catch { /* Preserve the receive error; always observe and stop the sender. */ }
            try { await receive; } catch { /* Observe both tasks without masking the original failure. */ }
        }
    }

    private static string AudioFormat(string contentType) => contentType.Split(';')[0].Trim().ToLowerInvariant() switch {
        "audio/webm" or "video/webm" => "webm", "audio/wav" or "audio/x-wav" => "wav",
        "audio/mpeg" or "audio/mp3" => "mp3", "audio/mp4" or "audio/x-m4a" => "m4a",
        "audio/ogg" or "application/ogg" => "ogg", _ => "unknown"
    };

    private static string ErrorReason(JsonElement root)
    {
        // Never log provider text: it can echo keys, user content or arbitrary configuration.
        // Match documented diagnostics to a fixed vocabulary; unknown messages stay unclassified.
        if (root.TryGetProperty("error_type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "model_not_available")
            return "model_not_available";
        if (root.TryGetProperty("error_message", out var message) && message.ValueKind == JsonValueKind.String) {
            var value = message.GetString() ?? "";
            if (value.StartsWith("Language hints must be unique", StringComparison.OrdinalIgnoreCase)) return "language_hints_duplicate";
            if (value.StartsWith("Audio decode error", StringComparison.OrdinalIgnoreCase)) return "audio_decode_error";
            if (value.StartsWith("Audio data channels must be specified", StringComparison.OrdinalIgnoreCase)) return "audio_channels_required";
            if (value.StartsWith("Audio data sample rate must be specified", StringComparison.OrdinalIgnoreCase)) return "audio_sample_rate_required";
            if (value.StartsWith("Audio frame is not valid base64", StringComparison.OrdinalIgnoreCase)) return "audio_frame_invalid";
            if (value.StartsWith("Audio is too long", StringComparison.OrdinalIgnoreCase)) return "audio_too_long";
        }
        return "provider_request_rejected";
    }

    private static async Task SendAudioAsync(WebSocket socket, Stream audio, CancellationToken ct)
    {
        var buffer = new byte[16384]; int read;
        while ((read = await audio.ReadAsync(buffer, ct)) > 0)
            await socket.SendAsync(buffer.AsMemory(0, read), WebSocketMessageType.Binary, true, ct);
        // Soniox ends input with an empty TEXT message, not an empty binary chunk.
        await socket.SendAsync(ReadOnlyMemory<byte>.Empty, WebSocketMessageType.Text, true, ct);
    }

    private static async Task<string> ReceiveTranscriptAsync(WebSocket socket, CancellationToken ct, Func<SpeechTranscriptUpdate, CancellationToken, Task>? update)
    {
        var transcript = new StringBuilder(); var buffer = new byte[8192];
        while (true)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult frame;
            do
            {
                frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (frame.MessageType != WebSocketMessageType.Text) throw new InterviewProviderException("stt_invalid_response");
                if (message.Length + frame.Count > 128000) throw new InterviewProviderException("stt_response_too_large");
                message.Write(buffer, 0, frame.Count);
            } while (!frame.EndOfMessage);
            using var json = JsonDocument.Parse(message.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new InterviewProviderException("stt_invalid_response");
            if (root.TryGetProperty("error_code", out var error))
                throw new InterviewProviderException(error.ValueKind == JsonValueKind.Number && error.TryGetInt32(out var status) && status is >= 400 and <= 599 ? $"stt_http_{status}" : "stt_invalid_response", ErrorReason(root));
            var interim = new StringBuilder(); var changed = false;
            if (root.TryGetProperty("tokens", out var tokens))
            {
                if (tokens.ValueKind != JsonValueKind.Array) throw new InterviewProviderException("stt_invalid_response");
                foreach (var token in tokens.EnumerateArray())
                {
                    if (token.ValueKind != JsonValueKind.Object || !token.TryGetProperty("is_final", out var final) || final.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                        throw new InterviewProviderException("stt_invalid_response");
                    if (!token.TryGetProperty("text", out var value) || value.ValueKind != JsonValueKind.String) throw new InterviewProviderException("stt_invalid_response");
                    var text = value.GetString();
                    if (text is "<end>" or "<fin>") continue;
                    changed = true;
                    if (final.GetBoolean()) transcript.Append(text); else interim.Append(text);
                    if (transcript.Length > 32000 || interim.Length > 32000) throw new InterviewProviderException("stt_response_too_large");
                }
            }
            if (changed && update is not null) await update(new(transcript.ToString(), interim.ToString()), ct);
            if (root.TryGetProperty("finished", out var finished) && finished.ValueKind == JsonValueKind.True)
            {
                // A valid completed session may contain no speech (for example a muted microphone).
                // Audio-check returns its existing no-speech result; answer processing validates text separately.
                return transcript.ToString();
            }
        }
    }
}
