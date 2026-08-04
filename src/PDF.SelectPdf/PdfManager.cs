using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.MimeTypes;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Models;
using Regira.Utilities;
using SelectPdf;

namespace Regira.Office.PDF.SelectPdf;

public class PdfManager : IHtmlToPdfService
{
    public Task<IMemoryFile> Create(HtmlInput template, CancellationToken cancellationToken = default)
    {
        var doc = GetPdfDocument(template);
        var ms = new MemoryStream();
        doc.Save(ms);
        doc.Close();
        var pdf = ms.ToMemoryFile(ContentTypes.PDF);
        return Task.FromResult(pdf);
    }

    protected PdfDocument GetPdfDocument(HtmlInput template)
    {
        var pageSize = (PdfPageSize)Enum.Parse(typeof(PdfPageSize), template.Format.ToString(), true);
        var pdfOrientation = (PdfPageOrientation)Enum.Parse(typeof(PdfPageOrientation), template.Orientation.ToString(), true);
        var margins = template.Margins;
        var htmlString = template.HtmlContent;

        var converter = new HtmlToPdf
        {
            Options =
            {
                PdfPageSize = pageSize,
                PdfPageOrientation = pdfOrientation,
                MarginTop = (int)margins.Top,
                MarginRight = (int)margins.Right,
                MarginBottom = (int)margins.Bottom,
                MarginLeft = (int)margins.Left
            }
        };

        // header
        if (template.HeaderHtmlContent != null)
        {
            var header = new PdfHtmlSection(template.HeaderHtmlContent, null);
            converter.Header.Add(header);
            converter.Options.DisplayHeader = true;
            if (template.HeaderHeight.HasValue)
            {
                converter.Header.Height = MillimetersToPoints(template.HeaderHeight.Value);
            }
        }
        // footer
        if (template.FooterHtmlContent != null)
        {
            var footer = new PdfHtmlSection(template.FooterHtmlContent, null);
            converter.Footer.Add(footer);
            converter.Options.DisplayFooter = true;
            if (template.FooterHeight.HasValue)
            {
                converter.Footer.Height = MillimetersToPoints(template.FooterHeight.Value);
            }
        }

        var doc = converter.ConvertHtmlString(htmlString);
        return doc;
    }

    static int MillimetersToPoints(float mm)
    {
        return (int)DimensionsUtility.MmToPt(mm);
    }
}