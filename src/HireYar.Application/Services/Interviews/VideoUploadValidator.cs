using System.Buffers.Binary;
using HireYar.Application.Common;
namespace HireYar.Application.Interviews;

public static class VideoUploadValidator
{
    public static async Task<string> ValidateAsync(Stream video, string fileName, string mime, long size, InterviewOptions options, CancellationToken ct)
    {
        if (size <= 0 || !video.CanSeek) throw AppException.Invalid("video_invalid");
        if (size > options.MaxVideoUploadBytes) throw new AppException(413, "video_too_large", "The video file is too large.");
        var prefix = new byte[16];
        var read = await video.ReadAtLeastAsync(prefix, 12, throwOnEndOfStream: false, ct); video.Position = 0;
        mime = mime.Split(';')[0].Trim().ToLowerInvariant();
        var valid = read >= 12 && Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".webm" => mime == "video/webm" && prefix.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }),
            ".mp4" => mime == "video/mp4" && prefix.AsSpan(4, 4).SequenceEqual("ftyp"u8) && BinaryPrimitives.ReadUInt32BigEndian(prefix.AsSpan(0, 4)) >= 12,
            _ => false
        };
        if (!valid) throw AppException.Invalid("video_invalid");
        return mime;
    }
}
