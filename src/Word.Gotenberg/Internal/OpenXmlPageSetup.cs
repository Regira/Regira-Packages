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
using WordPageSizes = Regira.Office.Word.Layout.WordPageSizes;
using PictureScaling = Regira.Office.Word.Layout.PictureScaling;

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
    private const double EmusPerPoint = 12700;

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

        var (width, height) = WordPageSizes.Twips(settings.PageSize);
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
            // document order: a drawing comes before the drawings nested in it, which grow no more than it does
            var scales = new Dictionary<WordDrawing, double>();
            foreach (var drawing in blocks.SelectMany(DescendantsAndSelf<WordDrawing>))
            {
                var holder = drawing.Ancestors<WordDrawing>().FirstOrDefault();
                scales[drawing] = ScalePicture(drawing, originalWidth.Value, newWidth.Value, holder != null && scales.TryGetValue(holder, out var cap) ? cap : null);
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

    /// <param name="drawing">The drawing to scale</param>
    /// <param name="originalTextWidth">The text width before the page setting</param>
    /// <param name="newTextWidth">The text width after it</param>
    /// <param name="cap">The scale of the drawing this one is nested in: a text box's room grows by the box's scale</param>
    /// <returns>The scale the drawing took</returns>
    private static double ScalePicture(WordDrawing drawing, double originalTextWidth, double newTextWidth, double? cap)
    {
        // wp:extent sizes the picture in the text flow, a:ext the graphic inside it; both in EMUs. A drawing nested in
        // this one, a picture in a text box, scales as a drawing of its own
        var flowExtents = Own<Wp.Extent>(drawing);
        // only the top-level graphic's a:ext: a:graphicData / pic:pic, wps:wsp or wpg:wgp / its spPr or grpSpPr / a:xfrm.
        // The shapes in a group sit in the child space its a:chOff and a:chExt map onto that a:ext, so they follow it. A
        // drawing canvas (wpc:wpc) has no size beyond wp:extent and places its shapes in EMUs of its own, so each shape
        // directly in it scales, its a:off with it
        var graphicExtents = Own<A.Extents>(drawing).Where(extents => IsGraphic(Shape(extents)) || InCanvas(Shape(extents)));
        var canvasOffsets = Own<A.Offset>(drawing).Where(offset => InCanvas(Shape(offset)));

        // as far as the text width grows, stopping at Word's 22-inch shape limit as the other Word backends do; the
        // picture's size in the text flow decides where that is
        var size = flowExtents.FirstOrDefault();
        var scale = Math.Min(
            PictureScaling.Factor(originalTextWidth, newTextWidth, (size?.Cx?.Value ?? 0) / EmusPerPoint, (size?.Cy?.Value ?? 0) / EmusPerPoint),
            cap ?? double.MaxValue);

        foreach (var extent in flowExtents)
        {
            if (extent.Cx?.Value is { } cx) extent.Cx = Scale(cx, scale);
            if (extent.Cy?.Value is { } cy) extent.Cy = Scale(cy, scale);
        }
        foreach (var extents in graphicExtents)
        {
            if (extents.Cx?.Value is { } cx) extents.Cx = Scale(cx, scale);
            if (extents.Cy?.Value is { } cy) extents.Cy = Scale(cy, scale);
        }
        foreach (var offset in canvasOffsets)
        {
            if (offset.X?.Value is { } x) offset.X = Scale(x, scale);
            if (offset.Y?.Value is { } y) offset.Y = Scale(y, scale);
        }
        return scale;
    }

    private const string CanvasNamespace = "http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas";

    /// <summary>The shape an <c>a:xfrm</c>'s <c>a:off</c> or <c>a:ext</c> places: <c>a:xfrm</c> / its <c>spPr</c> or <c>grpSpPr</c> / the shape.</summary>
    private static OpenXmlElement? Shape(OpenXmlElement transformPart) => transformPart.Parent?.Parent?.Parent;

    /// <summary>Whether the shape is the drawing's graphic itself.</summary>
    private static bool IsGraphic(OpenXmlElement? shape) => shape?.Parent is A.GraphicData;

    /// <summary>Whether the shape sits directly in a drawing canvas that is the drawing's graphic.</summary>
    private static bool InCanvas(OpenXmlElement? shape)
        => shape?.Parent is { LocalName: "wpc", NamespaceUri: CanvasNamespace } canvas && canvas.Parent is A.GraphicData;

    /// <summary>The drawing's elements of the given type, leaving out those of a drawing nested in it.</summary>
    private static List<T> Own<T>(WordDrawing drawing) where T : OpenXmlElement
        => drawing.Descendants<T>().Where(element => element.Ancestors<WordDrawing>().First() == drawing).ToList();

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
