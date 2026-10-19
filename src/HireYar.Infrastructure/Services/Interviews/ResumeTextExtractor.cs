using System.IO.Compression;
using System.Text;
using System.Xml;
using HireYar.Application.Interviews;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace HireYar.Infrastructure.Interviews;

public sealed class ResumeTextExtractor : IResumeTextExtractor
{
    public Task<string?> ExtractAsync(byte[] file, string extension, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var text = new StringBuilder();
        try
        {
            if (extension == ".pdf")
            {
                using var pdf = PdfDocument.Open(file);
                if (pdf.NumberOfPages > 50) return Task.FromResult<string?>(null);
                foreach (var page in pdf.GetPages())
                {
                    ct.ThrowIfCancellationRequested();
                    text.AppendLine(ContentOrderTextExtractor.GetText(page));
                    if (text.Length > 32000) return Task.FromResult<string?>(null);
                }
            }
            else if (extension == ".docx")
            {
                using var zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read);
                var entry = zip.GetEntry("word/document.xml");
                if (entry is null || entry.Length > 2 * 1024 * 1024) return Task.FromResult<string?>(null);
                using var stream = entry.Open();
                using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2 * 1024 * 1024 });
                while (reader.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    if (reader.NodeType == XmlNodeType.Text) text.Append(reader.Value);
                    if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "p") text.AppendLine();
                    if (text.Length > 32000) return Task.FromResult<string?>(null);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        // Malformed/encrypted/scanned files require an explicit text transcription, never a fabricated resume.
        catch (Exception) { return Task.FromResult<string?>(null); }
        return Task.FromResult(ResumeTextNormalizer.Normalize(text.ToString()));
    }
}
