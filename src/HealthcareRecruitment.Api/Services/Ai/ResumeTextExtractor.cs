using System.Text;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace HealthcareRecruitment.Api.Services.Ai;

/// <summary>Pulls plain text out of an uploaded PDF or Word resume so it can be fed to the AI assistant.</summary>
public static class ResumeTextExtractor
{
    private const int MaxChars = 20_000; // matches User.ResumeText column length

    public static string Extract(Stream fileStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        var text = extension switch
        {
            ".pdf" => ExtractPdf(fileStream),
            ".docx" => ExtractDocx(fileStream),
            _ => throw new NotSupportedException("Only PDF (.pdf) and Word (.docx) resumes are supported."),
        };

        text = text.Trim();
        if (text.Length == 0)
        {
            throw new InvalidOperationException(
                "Couldn't read any text from that file — it may be a scanned image rather than real text. Try exporting it again or use a different file.");
        }

        return text.Length > MaxChars ? text[..MaxChars] : text;
    }

    private static string ExtractPdf(Stream fileStream)
    {
        using var document = PdfDocument.Open(fileStream);
        var sb = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return sb.ToString();
    }

    private static string ExtractDocx(Stream fileStream)
    {
        using var document = WordprocessingDocument.Open(fileStream, false);
        return document.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
    }
}
