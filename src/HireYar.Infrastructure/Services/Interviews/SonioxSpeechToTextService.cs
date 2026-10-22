using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HireYar.Application.Interviews;
using Microsoft.Extensions.Logging;

namespace HireYar.Infrastructure.Interviews;

public sealed class SonioxSpeechToTextService(AiConnectionOptions connection, AiCapabilityOptions route, ILogger<SonioxSpeechToTextService> logger) : ISpeechToTextService
{
    public async Task<SpeechTranscriptionResult> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", "Bearer " + connection.ApiKey);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(route.TimeoutSeconds));
        var watch = Stopwatch.StartNew(); var outcome = "success"; string? failureCode = null;
        var operationId = Guid.NewGuid().ToString("N");
        InterviewTrace.Emit("provider", "stt", "started", "تبدیل فایل صوتی به متن", operationId, "WebSocket", "transcribe-websocket", "Soniox", route.Model);
        try
        {
            await socket.ConnectAsync(new Uri(connection.WebSocketUrl), timeout.Token);
            return await TranscribeSessionAsync(socket, audio, route, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { outcome = "timeout"; failureCode = "stt_timeout"; throw new InterviewProviderException(failureCode); }
        catch (OperationCanceledException) { outcome = "cancelled"; failureCode = "stt_cancelled"; throw; }
        catch (Exception e) when (e is WebSocketException or HttpRequestException or IOException) { outcome = "network_failure"; failureCode = "stt_unavailable"; throw new InterviewProviderException(failureCode); }
        catch (JsonException) { outcome = "failure"; failureCode = "stt_invalid_response"; throw new InterviewProviderException(failureCode); }
        catch (InterviewProviderException e) { outcome = "failure"; failureCode = e.Code; throw; }
        finally
        {
            socket.Abort();
            InterviewMetrics.SttDuration.Record(watch.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("provider", "Soniox"));
            if (outcome != "success") InterviewMetrics.SttFailure.Add(1);
            InterviewTrace.Emit("provider", "stt", outcome, "تبدیل فایل صوتی به متن", operationId, "WebSocket", "transcribe-websocket", "Soniox", route.Model,
                watch.Elapsed.TotalMilliseconds, null, failureCode);
            logger.LogInformation("Interview provider operation=stt provider=Soniox model={Model} status={Status} failureCode={FailureCode} durationMs={DurationMs}", route.Model, outcome, failureCode, watch.Elapsed.TotalMilliseconds);
        }
    }

    internal static async Task<SpeechTranscriptionResult> TranscribeSessionAsync(WebSocket socket, Stream audio, AiCapabilityOptions route, CancellationToken ct)
    {
        using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var config = JsonSerializer.SerializeToUtf8Bytes(new { model = route.Model, audio_format = "auto", language_hints = route.Options.LanguageHints });
        await socket.SendAsync(config.AsMemory(), WebSocketMessageType.Text, true, session.Token);
        var send = SendAudioAsync(socket, audio, session.Token);
        var receive = ReceiveTranscriptAsync(socket, session.Token);
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

    private static async Task SendAudioAsync(WebSocket socket, Stream audio, CancellationToken ct)
    {
        var buffer = new byte[16384]; int read;
        while ((read = await audio.ReadAsync(buffer, ct)) > 0)
            await socket.SendAsync(buffer.AsMemory(0, read), WebSocketMessageType.Binary, true, ct);
        // Soniox ends input with an empty TEXT message, not an empty binary chunk.
        await socket.SendAsync(ReadOnlyMemory<byte>.Empty, WebSocketMessageType.Text, true, ct);
    }

    private static async Task<string> ReceiveTranscriptAsync(WebSocket socket, CancellationToken ct)
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
                throw new InterviewProviderException(error.ValueKind == JsonValueKind.Number && error.TryGetInt32(out var status) && status is >= 400 and <= 599 ? $"stt_http_{status}" : "stt_invalid_response");
            if (root.TryGetProperty("tokens", out var tokens))
            {
                if (tokens.ValueKind != JsonValueKind.Array) throw new InterviewProviderException("stt_invalid_response");
                foreach (var token in tokens.EnumerateArray())
                {
                    if (token.ValueKind != JsonValueKind.Object || !token.TryGetProperty("is_final", out var final) || final.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                        throw new InterviewProviderException("stt_invalid_response");
                    if (!final.GetBoolean()) continue;
                    if (!token.TryGetProperty("text", out var value) || value.ValueKind != JsonValueKind.String) throw new InterviewProviderException("stt_invalid_response");
                    var text = value.GetString();
                    if (text is "<end>" or "<fin>") continue;
                    transcript.Append(text);
                    if (transcript.Length > 32000) throw new InterviewProviderException("stt_response_too_large");
                }
            }
            if (root.TryGetProperty("finished", out var finished) && finished.ValueKind == JsonValueKind.True)
            {
                // A valid completed session may contain no speech (for example a muted microphone).
                // Audio-check returns its existing no-speech result; answer processing validates text separately.
                return transcript.ToString();
            }
        }
    }
}
