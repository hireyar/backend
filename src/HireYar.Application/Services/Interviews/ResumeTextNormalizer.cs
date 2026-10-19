namespace HireYar.Application.Interviews;

public static class ResumeTextNormalizer
{
    public static string? Normalize(string? text)
    {
        // PDF text extraction can emit NUL glyphs, which PostgreSQL text cannot store.
        var normalized = text?.Replace("\0", "", StringComparison.Ordinal).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
