using System.IO.Compression;
using HireYar.Application.Common;

namespace HireYar.Application.Interviews;

public static class ResumeUploadValidator
{
    public const int MaxBytes = 10 * 1024 * 1024;
    public static async Task<(byte[] Bytes, string ContentType, string Extension)> ReadAsync(Stream source, string filename, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192]; int count;
        while ((count = await source.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + count > MaxBytes) throw AppException.Invalid("resume_too_large");
            buffer.Write(chunk, 0, count);
        }
        var bytes = buffer.ToArray(); var extension = Path.GetExtension(filename).ToLowerInvariant();
        if (extension == ".pdf" && bytes.AsSpan().StartsWith("%PDF-"u8)) return (bytes, "application/pdf", extension);
        if (extension == ".doc" && bytes.AsSpan().StartsWith(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 }))
            return (bytes, "application/msword", extension);
        if (extension == ".docx")
        {
            try
            {
                using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
                if (zip.GetEntry("[Content_Types].xml") is not null && zip.GetEntry("word/document.xml") is not null)
                    return (bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", extension);
            }
            catch (InvalidDataException) { }
        }
        throw AppException.Invalid("invalid_resume_format");
    }
}
