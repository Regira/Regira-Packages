using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using ConversionOptions = Regira.Office.Word.Models.ConversionOptions;
using DocumentSettings = Regira.Office.Word.Models.DocumentSettings;
using WordDrawing = DocumentFormat.OpenXml.Wordprocessing.Drawing;
using Margins = Regira.Office.Models.Margins;
using RegiraPageOrientation = Regira.Office.Models.PageOrientation;
using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace Regira.Office.Word.Gotenberg.Internal;

/// <summary>
/// Applies <see cref="ConversionOptions.Settings"/> to an OOXML package before it is uploaded.
/// <para>
/// Gotenberg's LibreOffice route has no page-size or margin fields, so the settings are written into
/// every section's <c>w:sectPr</c> instead, and LibreOffice lays the document out from those. The
/// scaling rules match <c>Word.Spire</c>: a table spanning (nearly) the old text width, and every
/// picture, grows or shrinks with the text width.
/// </para>
/// </summary>
internal static class OpenXmlPageSetup
{
    private const double TwipsPerPoint = 20;

    public static byte[] Apply(byte[] source, ConversionOptions options)
    {
        if (options.Settings is not { } settings)
        {
            return source;
        }

        using var stream = new MemoryStream();
        stream.Write(source, 0, source.Length);
        stream.Position = 0;

        using (var doc = WordprocessingDocument.Open(stream, true))
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body != null)
            {
                if (body.Elements<SectionProperties>().FirstOrDefault() == null)
                {
                    // Word writes one; without it the application default applies
                    body.AppendChild(new SectionProperties());
                }

                foreach (var (properties, blocks) in Sections(body))
                {
                    ApplySection(properties, blocks, settings, options);
                }
            }
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Splits the body into sections. A section ends at a paragraph carrying its own <c>w:sectPr</c>;
    /// the body-level <c>w:sectPr</c> closes the last one.
    /// </summary>
    private static IEnumerable<(SectionProperties Properties, List<OpenXmlElement> Blocks)> Sections(Body body)
    {
        var blocks = new List<OpenXmlElement>();
        foreach (var element in body.ChildElements.ToArray())
        {
            if (element is SectionProperties last)
            {
                yield return (last, blocks);
                blocks = [];
                continue;
            }

            blocks.Add(element);
            if (element is Paragraph { ParagraphProperties.SectionProperties: { } properties })
            {
                yield return (properties, blocks);
                blocks = [];
            }
        }
    }

    private static void ApplySection(SectionProperties properties, List<OpenXmlElement> blocks, DocumentSettings settings, ConversionOptions options)
    {
        var pageSize = GetOrAdd<PageSize>(properties);
        var pageMargin = properties.GetFirstChild<PageMargin>();

        var originalWidth = TextWidth(pageSize, pageMargin);

        var (width, height) = PageSizes.Twips(settings.PageSize);
        var landscape = settings.PageOrientation == RegiraPageOrientation.Landscape;
        pageSize.Width = landscape ? height : width;
        pageSize.Height = landscape ? width : height;
        pageSize.Orient = landscape ? PageOrientationValues.Landscape : PageOrientationValues.Portrait;

        if (settings.Margins != null)
        {
            pageMargin ??= GetOrAdd<PageMargin>(properties);
            SetMargins(pageMargin, settings.Margins);
        }

        var newWidth = TextWidth(pageSize, pageMargin);
        if (originalWidth is not > 0 || newWidth is not > 0 || originalWidth == newWidth)
        {
            return;
        }

        var scale = newWidth.Value / originalWidth.Value;

        if (options.AutoScaleTables)
        {
            foreach (var table in blocks.SelectMany(DescendantsAndSelf<Table>))
            {
                ScaleTable(table, originalWidth.Value, scale);
            }
        }
        if (options.AutoScalePictures)
        {
            foreach (var drawing in blocks.SelectMany(DescendantsAndSelf<WordDrawing>))
            {
                ScalePicture(drawing, scale);
            }
        }
    }

    private static void SetMargins(PageMargin pageMargin, Margins margins)
    {
        pageMargin.Top = (int)Math.Round(margins.Top * TwipsPerPoint);
        pageMargin.Bottom = (int)Math.Round(margins.Bottom * TwipsPerPoint);
        // top and bottom may be negative (text over the header); left and right may not
        pageMargin.Left = (uint)Math.Max(0, Math.Round(margins.Left * TwipsPerPoint));
        pageMargin.Right = (uint)Math.Max(0, Math.Round(margins.Right * TwipsPerPoint));
        // required by the schema; Word's defaults
        pageMargin.Header ??= 720U;
        pageMargin.Footer ??= 720U;
        pageMargin.Gutter ??= 0U;
    }

    /// <summary>
    /// The width available to body text, in twips; <c>null</c> when the page width is not stated.
    /// </summary>
    private static double? TextWidth(PageSize pageSize, PageMargin? pageMargin)
    {
        if (pageSize.Width?.Value is not { } pageWidth)
        {
            return null;
        }
        return (double)pageWidth - (pageMargin?.Left?.Value ?? 0) - (pageMargin?.Right?.Value ?? 0);
    }

    /// <summary>
    /// Scales a table that spans (nearly) the whole text width. Relative (percentage) widths already
    /// follow the page, and a clearly narrower table keeps its designed width.
    /// </summary>
    private static void ScaleTable(Table table, double originalWidth, double scale)
    {
        var tableWidth = table.GetFirstChild<TableProperties>()?.TableWidth;
        if (tableWidth?.Type?.Value == TableWidthUnitValues.Pct)
        {
            return;
        }

        var grid = table.GetFirstChild<TableGrid>()?.Elements<GridColumn>().ToArray() ?? [];
        var width = tableWidth?.Type?.Value == TableWidthUnitValues.Dxa && TryParse(tableWidth.Width?.Value, out var dxa)
            ? dxa
            : grid.Sum(column => TryParse(column.Width?.Value, out var w) ? w : 0);

        // the Word.Spire rule: leave a table alone when it is more than 10% narrower than the text width
        if (width <= 0 || originalWidth / width - 1 >= .1)
        {
            return;
        }

        if (tableWidth?.Type?.Value == TableWidthUnitValues.Dxa)
        {
            tableWidth.Width = Scale(tableWidth.Width?.Value, scale);
        }
        foreach (var column in grid)
        {
            column.Width = Scale(column.Width?.Value, scale);
        }
        // this table's own cells, not those of a table nested inside them
        var cellWidths = table.Elements<TableRow>()
            .SelectMany(row => row.Elements<TableCell>())
            .Select(cell => cell.TableCellProperties?.TableCellWidth)
            .Where(cellWidth => cellWidth?.Type?.Value == TableWidthUnitValues.Dxa);
        foreach (var cellWidth in cellWidths)
        {
            cellWidth!.Width = Scale(cellWidth.Width?.Value, scale);
        }
    }

    private static void ScalePicture(WordDrawing drawing, double scale)
    {
        // wp:extent sizes the picture in the text flow, a:ext the graphic inside it; both in EMUs
        foreach (var extent in drawing.Descendants<Wp.Extent>())
        {
            if (extent.Cx?.Value is { } cx) extent.Cx = Scale(cx, scale);
            if (extent.Cy?.Value is { } cy) extent.Cy = Scale(cy, scale);
        }
        foreach (var extents in drawing.Descendants<A.Extents>())
        {
            if (extents.Cx?.Value is { } cx) extents.Cx = Scale(cx, scale);
            if (extents.Cy?.Value is { } cy) extents.Cy = Scale(cy, scale);
        }
    }

    private static T GetOrAdd<T>(SectionProperties properties) where T : OpenXmlElement, new()
    {
        var element = properties.GetFirstChild<T>();
        if (element != null)
        {
            return element;
        }

        element = new T();
        // AddChild respects the schema's child order, which Word insists on
        properties.AddChild(element);
        return element;
    }

    private static IEnumerable<T> DescendantsAndSelf<T>(OpenXmlElement element) where T : OpenXmlElement
        => element is T self ? element.Descendants<T>().Prepend(self) : element.Descendants<T>();

    private static bool TryParse(string? value, out double result)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static string? Scale(string? value, double scale)
        => TryParse(value, out var number)
            ? Math.Round(number * scale).ToString(CultureInfo.InvariantCulture)
            : value;

    private static long Scale(long value, double scale)
        => (long)Math.Round(value * scale);
}
