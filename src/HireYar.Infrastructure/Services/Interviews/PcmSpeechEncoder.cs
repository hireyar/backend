using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using HireYar.Application.Interviews;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace HireYar.Infrastructure.Interviews;

public sealed class SpeechEncodingOptions
{
    public string FfmpegPath { get; set; } = "ffmpeg";
    public int TimeoutSeconds { get; set; } = 30;
}

public interface IPcmSpeechEncoder
{
    Task EnsureAvailableAsync(CancellationToken ct);
    Task<byte[]> EncodeAsync(byte[] pcm, decimal speed, CancellationToken ct);
}

public sealed class GeminiSpeechStartupCheck(IPcmSpeechEncoder encoder) : IHostedService
{
    public Task StartAsync(CancellationToken ct) => encoder.EnsureAvailableAsync(ct);
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

public sealed class FfmpegPcmSpeechEncoder(IOptions<SpeechEncodingOptions> options) : IPcmSpeechEncoder, IDisposable
{
    public const int MaxBytes = 10 * 1024 * 1024;
    private readonly SemaphoreSlim slots = new(2);
    private readonly SemaphoreSlim probe = new(1);
    private bool available;

    public async Task EnsureAvailableAsync(CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        var acquired = false;
        try {
            await probe.WaitAsync(deadline.Token); acquired = true;
            if (available) return;
            // Verify both executable and MP3 codec before making a chargeable provider call.
            await EncodeAsync(new byte[4800], 1, deadline.Token);
            available = true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new InterviewProviderException("tts_encoding_timeout"); }
        finally { if (acquired) probe.Release(); }
    }

    public async Task<byte[]> EncodeAsync(byte[] pcm, decimal speed, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (pcm.Length < 2 || pcm.Length % 2 != 0 || pcm.Length > MaxBytes || speed is < 0.25m or > 4m)
            throw new InterviewProviderException("tts_invalid_response");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        var acquired = false;
        var started = false;
        using var process = new Process { StartInfo = new() {
            FileName = options.Value.FfmpegPath, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        } };
        foreach (var argument in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-threads", "1",
            "-f", "s16le", "-ar", "24000", "-ac", "1", "-i", "pipe:0", "-map_metadata", "-1", "-vn" })
            process.StartInfo.ArgumentList.Add(argument);
        if (speed != 1) {
            process.StartInfo.ArgumentList.Add("-af");
            // Keep each atempo stage within 0.5..2, including the supported extreme speeds.
            var factors = new List<decimal>();
            var remaining = speed;
            while (remaining > 2) { factors.Add(2); remaining /= 2; }
            while (remaining < 0.5m) { factors.Add(0.5m); remaining /= 0.5m; }
            factors.Add(remaining);
            process.StartInfo.ArgumentList.Add(string.Join(',', factors.Select(f => "atempo=" + f.ToString(CultureInfo.InvariantCulture))));
        }
        foreach (var argument in new[] { "-c:a", "libmp3lame", "-b:a", "128k", "-threads", "1", "-f", "mp3", "pipe:1" })
            process.StartInfo.ArgumentList.Add(argument);
        try {
            await slots.WaitAsync(timeout.Token); acquired = true;
            if (!process.Start()) throw new InterviewProviderException("tts_encoder_unavailable");
            started = true;
            using var stop = timeout.Token.Register(() => Kill(process));
            using var output = new MemoryStream();
            Exception? failure = null;
            async Task Guard(Func<Task> operation) {
                try { await operation(); }
                catch (Exception error) { Interlocked.CompareExchange(ref failure, error, null); timeout.Cancel(); throw; }
            }
            try { await Task.WhenAll(
                Guard(async () => {
                    await process.StandardInput.BaseStream.WriteAsync(pcm, timeout.Token);
                    await process.StandardInput.BaseStream.FlushAsync(timeout.Token);
                    process.StandardInput.Close();
                }),
                Guard(async () => {
                    var buffer = new byte[8192]; int read;
                    while ((read = await process.StandardOutput.BaseStream.ReadAsync(buffer, timeout.Token)) != 0) {
                        if (output.Length + read > MaxBytes) throw new InterviewProviderException("tts_encoded_response_too_large");
                        await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                    }
                }),
                // Drain diagnostic output without logging or retaining potentially sensitive text.
                Guard(() => process.StandardError.BaseStream.CopyToAsync(Stream.Null, timeout.Token)),
                Guard(() => process.WaitForExitAsync(timeout.Token))); }
            catch {
                if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
                throw;
            }
            if (process.ExitCode != 0) throw new InterviewProviderException("tts_encoding_failed");
            var result = output.ToArray();
            if (result.Length < 3 || !(result.AsSpan(0, 3).SequenceEqual("ID3"u8) || result[0] == 0xff && (result[1] & 0xe0) == 0xe0))
                throw new InterviewProviderException("tts_encoding_failed");
            return result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new InterviewProviderException("tts_encoding_timeout"); }
        catch (Exception error) when (error is Win32Exception or FileNotFoundException) { throw new InterviewProviderException("tts_encoder_unavailable"); }
        catch (Exception error) when (error is IOException or InvalidOperationException) {
            ct.ThrowIfCancellationRequested();
            throw new InterviewProviderException("tts_encoding_failed");
        }
        finally {
            Kill(process);
            if (started) {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { await process.WaitForExitAsync(cleanup.Token); }
                catch (OperationCanceledException) { }
            }
            if (acquired) slots.Release();
        }
    }

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }

    public void Dispose() { slots.Dispose(); probe.Dispose(); }
}
