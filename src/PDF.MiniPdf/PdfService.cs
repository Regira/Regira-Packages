using System.IO.Compression;
using MiniSoftware;
using Regira.Dimensions;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.MimeTypes;
using Regira.Office.Models;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.MiniPdf.Internal;
using Regira.Office.PDF.Models;
using Regira.Office.Utilities;
using MiniPdfConverter = MiniSoftware.MiniPdf;

namespace Regira.Office.PDF.MiniPdf;

/// <summary>
/// Converts Word documents (<c>.docx</c>), spreadsheets (<c>.xlsx</c>) and presentations (<c>.pptx</c>) to PDF
/// in-process with <see href="https://github.com/mini-software/MiniPdf">MiniPdf</see>: no Office installation, no
/// server and no licence. MiniPdf lays the document out itself, so complex documents come out less faithful than
/// through LibreOffice.
/// <para>
/// The page settings of <see cref="DocumentInput"/> a source format takes:
/// </para>
/// <list type="bullet">
/// <item><c>.docx</c> — <see cref="DocumentInput.Format"/>, with or without <see cref="DocumentInput.Orientation"/>,
/// and <see cref="DocumentInput.Margins"/>. <c>Orientation</c> alone throws: MiniPdf replaces the page size as a
/// whole, so it needs the paper size to turn.</item>
/// <item><c>.xlsx</c> — <see cref="DocumentInput.Orientation"/>.</item>
/// <item><c>.pptx</c> — none; a slide keeps the presentation's slide size.</item>
/// </list>
/// <para>
/// Any other setting, and any other source format, throws <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// Text renders in the host's system fonts. On a host with few (a container), register TrueType fonts once at
/// startup with <c>MiniSoftware.MiniPdf.RegisterFont</c> — a registration for the whole process.
/// </para>
/// </summary>
public class PdfService : IDocumentToPdfService
{
    private const int PointsPerInch = 72;

    private enum SourceFormat
    {
        Docx,
        Xlsx,
        Pptx
    }

    public Task<IMemoryFile> Create(DocumentInput input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = input.Document?.GetBytes()
            ?? throw new ArgumentException("Document has no content.", nameof(input));
        var format = GetSourceFormat(bytes);
        var options = GetConversionOptions(input, format);
        if (format == SourceFormat.Xlsx)
        {
            bytes = SpreadsheetNamespace.AsDefault(bytes);
        }

        using var source = new MemoryStream(bytes, false);
        var pdf = MiniPdfConverter.ConvertToPdf(source, options);
        return Task.FromResult(pdf.ToMemoryFile(ContentTypes.PDF));
    }


    /// <summary>
    /// Reads the format from the package's parts, as MiniPdf does, so the settings are checked against the
    /// format MiniPdf converts.
    /// </summary>
    private static SourceFormat GetSourceFormat(byte[] bytes)
    {
        try
        {
            using var package = new ZipArchive(new MemoryStream(bytes, false), ZipArchiveMode.Read);
            foreach (var entry in package.Entries)
            {
                if (entry.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase))
                {
                    return SourceFormat.Docx;
                }
                if (entry.FullName.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
                {
                    return SourceFormat.Xlsx;
                }
                if (entry.FullName.StartsWith("ppt/", StringComparison.OrdinalIgnoreCase))
                {
                    return SourceFormat.Pptx;
                }
            }
        }
        catch (InvalidDataException)
        {
            // not a zip package
        }

        throw new NotSupportedException(
            "MiniPdf converts Office Open XML documents only: .docx, .xlsx and .pptx. A .doc, .xls, .odt or .rtf " +
            "document needs another IDocumentToPdfService.");
    }

    private static MiniPdfConversionOptions GetConversionOptions(DocumentInput input, SourceFormat format)
    {
        var options = new MiniPdfConversionOptions { Compress = true };
        switch (format)
        {
            case SourceFormat.Docx:
                if (input.Format.HasValue)
                {
                    var size = PageSizeUtility.GetPageSizeDimension(input.Format.Value, LengthUnit.Points,
                        input.Orientation ?? PageOrientation.Portrait, PointsPerInch);
                    options.PageSize = new MiniPdfPageSize(size.Width, size.Height);
                }
                else if (input.Orientation.HasValue)
                {
                    throw new NotSupportedException(
                        $"A .docx takes {nameof(DocumentInput.Orientation)} only together with {nameof(DocumentInput.Format)}: " +
                        "MiniPdf replaces the page size as a whole.");
                }
                if (input.Margins != null)
                {
                    options.Margins = new MiniPdfMargins(input.Margins.Left, input.Margins.Top, input.Margins.Right, input.Margins.Bottom);
                }
                break;
            case SourceFormat.Xlsx:
                ThrowIfSet(input.Format, nameof(DocumentInput.Format), "an .xlsx");
                ThrowIfSet(input.Margins, nameof(DocumentInput.Margins), "an .xlsx");
                if (input.Orientation.HasValue)
                {
                    options.Landscape = input.Orientation == PageOrientation.Landscape;
                }
                break;
            case SourceFormat.Pptx:
                ThrowIfSet(input.Format, nameof(DocumentInput.Format), "a .pptx");
                ThrowIfSet(input.Orientation, nameof(DocumentInput.Orientation), "a .pptx");
                ThrowIfSet(input.Margins, nameof(DocumentInput.Margins), "a .pptx");
                break;
        }
        return options;
    }

    private static void ThrowIfSet(object? value, string setting, string document)
    {
        if (value != null)
        {
            throw new NotSupportedException($"MiniPdf cannot apply {setting} to {document} document.");
        }
    }
}
