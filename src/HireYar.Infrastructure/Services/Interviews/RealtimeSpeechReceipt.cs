using System.Security.Cryptography;
using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using Microsoft.AspNetCore.DataProtection;
namespace HireYar.Infrastructure.Interviews;

public sealed class RealtimeSpeechReceipt(IDataProtectionProvider protection, TimeProvider time) : IRealtimeSpeechReceipt
{
    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private readonly IDataProtector protector = protection.CreateProtector("HireYar.RealtimeSpeech.Receipt.v1");
    private sealed record Payload(RealtimeSpeechContext Context, SpeechTranscriptionResult Result, string Sha256, long Bytes, DateTimeOffset ExpiresAt);
    public string Issue(RealtimeSpeechContext context, SpeechTranscriptionResult result, string audioSha256, long audioBytes) =>
        protector.Protect(JsonSerializer.Serialize(new Payload(context, result, audioSha256, audioBytes, time.GetUtcNow().AddMinutes(15)), Json));
    public async Task<SpeechTranscriptionResult> VerifyAsync(string receipt, RealtimeSpeechContext context, Stream audio, CancellationToken ct)
    {
        Payload? value;
        try { value = receipt.Length > 180000 ? null : JsonSerializer.Deserialize<Payload>(protector.Unprotect(receipt)); }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException) { value = null; }
        if (value is null || value.Context != context || value.ExpiresAt <= time.GetUtcNow() || string.IsNullOrWhiteSpace(value.Result.Provider) || value.Result.Provider.Length > 100 ||
            string.IsNullOrWhiteSpace(value.Result.Model) || value.Result.Model.Length > 100 ||
            value.Result.Text.Length > 32000 || !audio.CanSeek || audio.Length != value.Bytes)
            throw AppException.Invalid("realtime_receipt_invalid");
        audio.Position = 0;
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(audio, ct)); audio.Position = 0;
        if (!string.Equals(hash, value.Sha256, StringComparison.Ordinal)) throw AppException.Invalid("realtime_audio_mismatch");
        return value.Result;
    }
}
