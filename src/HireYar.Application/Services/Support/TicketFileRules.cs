using System.Globalization;
using System.Text;
using HireYar.Application.Common;

namespace HireYar.Application.Support;

// No decoding, rendering, archive extraction or trust in the browser's MIME declaration.
public static class TicketFileRules
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxFiles = 3;
    public static void Validate(IReadOnlyList<TicketUpload> files)
    {
        if (files.Count > MaxFiles) throw AppException.Invalid("ticket_attachment_count");
        foreach (var file in files)
        {
            var name = file.FileName;
            if (string.IsNullOrWhiteSpace(name) || name.Length > 180 || name != name.Trim() || name.StartsWith('.') ||
                name.EndsWith('.') || name.Contains("..", StringComparison.Ordinal) ||
                name.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format || "<>:\"/\\|?*".Contains(c)))
                throw AppException.Invalid("ticket_file_name");
            if (file.Content.Length is < 1 or > MaxFileBytes)
                throw new AppException(413, "ticket_file_size", "Each attachment must contain 1 byte to 5 MiB.");
            var ext = Path.GetExtension(name).ToLowerInvariant();
            var bytes = file.Content.AsSpan();
            bool valid;
            string expected;
            switch (ext)
            {
                case ".png":
                    expected = "image/png";
                    valid = bytes.Length >= 20 && bytes.StartsWith(new byte[] {137,80,78,71,13,10,26,10}) &&
                        bytes.EndsWith(new byte[] {0,0,0,0,73,69,78,68,174,66,96,130});
                    break;
                case ".jpg": case ".jpeg":
                    expected = "image/jpeg";
                    valid = bytes.Length >= 5 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217;
                    break;
                case ".pdf":
                    expected = "application/pdf";
                    valid = bytes.StartsWith("%PDF-1."u8) || bytes.StartsWith("%PDF-2."u8);
                    valid &= bytes.Length >= 12 && bytes[^Math.Min(bytes.Length, 1024)..].IndexOf("%%EOF"u8) >= 0;
                    break;
                case ".txt":
                    expected = "text/plain";
                    try
                    {
                        var text = new UTF8Encoding(false, true).GetString(bytes);
                        valid = !text.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t');
                    }
                    catch (DecoderFallbackException) { valid = false; }
                    break;
                default: throw AppException.Invalid("ticket_file_type");
            }
            if (!string.Equals(file.ContentType, expected, StringComparison.OrdinalIgnoreCase) || !valid)
                throw AppException.Invalid("ticket_file_content");
        }
    }
}

