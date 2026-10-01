using System.Buffers.Binary;
using HireYar.Application.Common;
namespace HireYar.Application.Interviews;
public static class AudioUploadValidator
{
    public static async Task ValidateAsync(Stream audio, string fileName, string mime, long size, int? durationSeconds, InterviewOptions options, CancellationToken ct)
    {
        if (size <= 0) throw AppException.Invalid("audio_invalid");
        if (size > options.MaxUploadBytes) throw new AppException(413, "audio_too_large", "The audio file is too large.");
        if (durationSeconds is <= 0 || durationSeconds > options.MaxAnswerSeconds) throw AppException.Invalid("answer_duration_invalid");
        if (!audio.CanSeek) throw AppException.Invalid("audio_invalid");
        var prefix = new byte[16]; var read = await audio.ReadAtLeastAsync(prefix, 12, throwOnEndOfStream: false, ct); audio.Position = 0;
        if (read < 12) throw AppException.Invalid("audio_invalid");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        mime = mime.Split(';')[0].Trim().ToLowerInvariant();
        var valid = extension switch {
            ".webm" => mime is "audio/webm" or "video/webm" && prefix.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }),
            ".wav" => mime is "audio/wav" or "audio/x-wav" && prefix.AsSpan(0, 4).SequenceEqual("RIFF"u8) && prefix.AsSpan(8, 4).SequenceEqual("WAVE"u8),
            ".mp3" => mime is "audio/mpeg" or "audio/mp3" && (prefix.AsSpan(0, 3).SequenceEqual("ID3"u8) || prefix[0] == 0xff && (prefix[1] & 0xe0) == 0xe0),
            ".m4a" => mime is "audio/mp4" or "audio/x-m4a" && prefix.AsSpan(4, 4).SequenceEqual("ftyp"u8) && BinaryPrimitives.ReadUInt32BigEndian(prefix.AsSpan(0, 4)) >= 12,
            ".ogg" => mime is "audio/ogg" or "application/ogg" && prefix.AsSpan(0, 4).SequenceEqual("OggS"u8),
            _ => false
        };
        if (!valid) throw AppException.Invalid("audio_invalid");
    }
    public static string Extension(string mime) => mime.Split(';')[0].Trim() switch {
        "audio/wav" or "audio/x-wav" => ".wav", "audio/mpeg" or "audio/mp3" => ".mp3",
        "audio/mp4" or "audio/x-m4a" => ".m4a", "audio/ogg" or "application/ogg" => ".ogg", _ => ".webm"
    };
}
