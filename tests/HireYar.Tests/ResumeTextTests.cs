using System.Globalization;
using System.Text;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Interviews;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace HireYar.Tests;

public sealed class ResumeTextTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("\0 \t\r\n\0", null)]
    [InlineData("  سابقه\0 کار\r\nC#\t.NET\0 و نیم‌فاصله  ", "سابقه کار\r\nC#\t.NET و نیم‌فاصله")]
    public void Normalization_removes_only_nul_and_trims_edges(string? source, string? expected) =>
        Assert.Equal(expected, ResumeTextNormalizer.Normalize(source));

    [Fact]
    public async Task Pdf_nul_glyph_is_removed_from_extracted_resume()
    {
        var bytes = PdfWithNulGlyph();
        using var pdf = PdfDocument.Open(bytes);
        Assert.Contains('\0', ContentOrderTextExtractor.GetText(pdf.GetPage(1)));
        Assert.Equal("AB", await new ResumeTextExtractor().ExtractAsync(bytes, ".pdf", default));
    }

    internal static byte[] PdfWithNulGlyph()
    {
        const string content = "BT /F1 12 Tf 20 250 Td <410042> Tj ET\n";
        const string cmap = "/CIDInit /ProcSet findresource begin 12 dict begin begincmap\n" +
            "/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n" +
            "/CMapName /TestUnicode def /CMapType 2 def\n1 begincodespacerange <00> <FF> endcodespacerange\n" +
            "3 beginbfchar <41> <0041> <00> <0000> <42> <0042> endbfchar\n" +
            "endcmap CMapName currentdict /CMap defineresource pop end end\n";
        string[] objects = [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding /ToUnicode 6 0 R >>",
            $"<< /Length {content.Length} >>\nstream\n{content}endstream",
            $"<< /Length {cmap.Length} >>\nstream\n{cmap}endstream"
        ];
        var text = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(text.Length);
            text.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = text.Length;
        text.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) text.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        text.Append($"trailer << /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(text.ToString());
    }
}
