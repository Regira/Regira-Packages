using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.MimeTypes;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Office.Word.testing.Abstractions;

/// <summary>
/// Small .docx templates built in code, for scenarios that read better with the template beside the assertions
/// than as a binary asset. An element belongs to one document, so build fresh content for every template.
/// </summary>
internal static class Docx
{
    public static W.Paragraph Paragraph(string text)
        => new(new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    public static IEnumerable<W.Paragraph> Paragraphs(params string[] texts) => texts.Select(Paragraph);

    /// <summary>A paragraph formatted as <paramref name="format"/> says, a run per span, each formatted as its span says.</summary>
    public static W.Paragraph Formatted(ParagraphFormat format, params Span[] spans)
    {
        var paragraph = new W.Paragraph(Properties(format));
        paragraph.Append(spans.Select(span => new W.Run(Properties(span.Format), new W.Text(span.Text) { Space = SpaceProcessingModeValues.Preserve })));
        return paragraph;
    }

    // the children in the order the schema gives them
    private static W.ParagraphProperties Properties(ParagraphFormat format)
    {
        var properties = new W.ParagraphProperties();
        if (format.Style != null) properties.AppendChild(new W.ParagraphStyleId { Val = format.Style });
        if (format.Shading != null) properties.AppendChild(new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = format.Shading });
        // an attribute set to null is still written, empty, and a reader takes an empty measure for 0: set only those given
        if (format.SpacingBefore != null || format.SpacingAfter != null || format.LineSpacing != null)
        {
            var spacing = properties.AppendChild(new W.SpacingBetweenLines());
            if (format.SpacingBefore != null) spacing.Before = format.SpacingBefore.ToString();
            if (format.SpacingAfter != null) spacing.After = format.SpacingAfter.ToString();
            if (format.LineSpacing != null) (spacing.Line, spacing.LineRule) = (format.LineSpacing.ToString(), W.LineSpacingRuleValues.Auto);
        }
        if (format.IndentLeft != null || format.IndentFirstLine != null)
        {
            var indentation = properties.AppendChild(new W.Indentation());
            if (format.IndentLeft != null) indentation.Left = format.IndentLeft.ToString();
            if (format.IndentFirstLine != null) indentation.FirstLine = format.IndentFirstLine.ToString();
        }
        if (format.Alignment != null) properties.AppendChild(new W.Justification { Val = new W.JustificationValues(format.Alignment) });
        return properties;
    }

    // the children in the order the schema gives them
    private static W.RunProperties Properties(RunFormat format)
    {
        var properties = new W.RunProperties();
        if (format.Style != null) properties.AppendChild(new W.RunStyle { Val = format.Style });
        if (format.Font != null) properties.AppendChild(new W.RunFonts { Ascii = format.Font, HighAnsi = format.Font });
        if (format.Bold) properties.AppendChild(new W.Bold());
        if (format.Italic) properties.AppendChild(new W.Italic());
        if (format.Strike) properties.AppendChild(new W.Strike());
        if (format.Color != null) properties.AppendChild(new W.Color { Val = format.Color });
        if (format.Size != null) properties.AppendChild(new W.FontSize { Val = format.Size.ToString() });
        if (format.Highlight != null) properties.AppendChild(new W.Highlight { Val = new W.HighlightColorValues(format.Highlight) });
        if (format.Underline != null) properties.AppendChild(new W.Underline { Val = new W.UnderlineValues(format.Underline) });
        return properties;
    }

    /// <summary>
    /// A style of the given type, for <see cref="Document(IEnumerable{OpenXmlElement}, IEnumerable{OpenXmlElement}?, IEnumerable{OpenXmlElement}?, bool, int?, IEnumerable{W.Style}?)"/>
    /// to define: a paragraph style spaces its paragraphs, a character style makes its text bold.
    /// </summary>
    public static W.Style Style(string id, W.StyleValues type)
        => new(
            new W.StyleName { Val = id },
            type == W.StyleValues.Paragraph
                ? new W.StyleParagraphProperties(new W.SpacingBetweenLines { Before = "120", After = "120" })
                : new W.StyleRunProperties(new W.Bold()))
        {
            Type = type,
            StyleId = id,
            CustomStyle = true
        };

    /// <summary>A paragraph without a run, as Word stores an empty line.</summary>
    public static W.Paragraph EmptyParagraph() => new();

    /// <summary>A paragraph of the given text, followed by a page break.</summary>
    public static W.Paragraph PageBreakAfter(string text)
        => new(new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }, new W.Break { Type = W.BreakValues.Page }));

    /// <summary>A paragraph whose text is split over one run per given text, the way Word saves a text edited in parts.</summary>
    public static W.Paragraph SplitParagraph(params string[] runs)
        => new(runs.Select(text => new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve })));

    /// <summary>A paragraph whose whole text is deleted under track changes.</summary>
    public static W.Paragraph DeletedParagraph(string text)
        => new(new W.DeletedRun(new W.Run(new W.DeletedText(text) { Space = SpaceProcessingModeValues.Preserve })) { Id = "1", Author = "Reviewer" });

    /// <summary>
    /// A paragraph that is a text edited under track changes: <paramref name="before"/>, then <paramref name="deleted"/>
    /// deleted and <paramref name="inserted"/> inserted in its place, then <paramref name="after"/>.
    /// </summary>
    public static W.Paragraph EditedParagraph(string before, string deleted, string inserted, string after)
        => new(
            new W.Run(new W.Text(before) { Space = SpaceProcessingModeValues.Preserve }),
            new W.DeletedRun(new W.Run(new W.DeletedText(deleted))) { Id = "2", Author = "Reviewer" },
            new W.InsertedRun(new W.Run(new W.Text(inserted))) { Id = "3", Author = "Reviewer" },
            new W.Run(new W.Text(after) { Space = SpaceProcessingModeValues.Preserve }));

    /// <summary>
    /// A paragraph whose visible text is <paramref name="before"/> and <paramref name="after"/>, with
    /// <paramref name="deleted"/> between them deleted under track changes.
    /// </summary>
    public static W.Paragraph WithDeletion(string before, string deleted, string after)
        => new(
            new W.Run(new W.Text(before) { Space = SpaceProcessingModeValues.Preserve }),
            new W.DeletedRun(new W.Run(new W.DeletedText(deleted))) { Id = "4", Author = "Reviewer" },
            new W.Run(new W.Text(after) { Space = SpaceProcessingModeValues.Preserve }));

    /// <summary>A paragraph holding one complex field, with the given code and result.</summary>
    public static W.Paragraph FieldParagraph(string code, string result)
        => new(
            new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }),
            new W.Run(new W.FieldCode(code) { Space = SpaceProcessingModeValues.Preserve }),
            new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }),
            new W.Run(new W.Text(result)),
            new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));

    /// <summary>
    /// A paragraph that ends its section, the way Word stores a section break — and, when asked, starts on a new page
    /// of its own.
    /// </summary>
    public static W.Paragraph SectionBreak(string text, bool pageBreakBefore = false)
    {
        var properties = new W.ParagraphProperties();
        if (pageBreakBefore)
        {
            properties.AppendChild(new W.PageBreakBefore());
        }
        properties.AppendChild(new W.SectionProperties(new W.SectionType { Val = W.SectionMarkValues.NextPage }, A4()));
        return new W.Paragraph(properties, new W.Run(new W.Text(text)));
    }

    /// <summary>An A4 portrait page, which Word writes in every section.</summary>
    private static W.PageSize A4() => new() { Width = (uint)Facts.A4Twips.Width, Height = (uint)Facts.A4Twips.Height };

    /// <summary>A paragraph holding an inline text box of the given paragraphs, written as DrawingML only.</summary>
    public static W.Paragraph TextBox(params string[] texts)
        => new(new W.Run(new W.Drawing(TextBoxDrawing(TextBoxContent(Paragraphs(texts))))));

    /// <summary>
    /// A paragraph holding an inline text box of the given paragraphs, the way Word writes one: an
    /// <c>mc:AlternateContent</c> with the DrawingML box as its <c>mc:Choice</c>, and a VML <c>v:textbox</c> holding
    /// the same paragraphs as its <c>mc:Fallback</c>, for readers that do not know DrawingML shapes.
    /// </summary>
    public static W.Paragraph TextBoxWithFallback(params string[] texts) => TextBoxWithFallback(Paragraphs(texts).ToArray());

    /// <inheritdoc cref="TextBoxWithFallback(string[])"/>
    public static W.Paragraph TextBoxWithFallback(params W.Paragraph[] paragraphs)
    {
        var content = TextBoxContent(paragraphs);
        var alternateContent = $"""
            <mc:AlternateContent xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                                 xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                                 xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape"
                                 xmlns:v="urn:schemas-microsoft-com:vml"
                                 xmlns:o="urn:schemas-microsoft-com:office:office">
              <mc:Choice Requires="wps">{TextBoxDrawing(content)}</mc:Choice>
              <mc:Fallback>
                <w:pict>
                  <v:shapetype id="_x0000_t202" coordsize="21600,21600" o:spt="202" path="m,l,21600r21600,l21600,xe">
                    <v:stroke joinstyle="miter"/>
                    <v:path gradientshapeok="t" o:connecttype="rect"/>
                  </v:shapetype>
                  <v:shape id="Text Box 1" o:spid="_x0000_s1026" type="#_x0000_t202" style="width:200pt;height:60pt">
                    <v:textbox>{content}</v:textbox>
                  </v:shape>
                </w:pict>
              </mc:Fallback>
            </mc:AlternateContent>
            """;
        return new W.Paragraph(new W.Run(new AlternateContent(alternateContent)));
    }

    // a text box's w:txbxContent, as markup: each copy of the box is parsed from it, so none shares an element
    private static string TextBoxContent(IEnumerable<W.Paragraph> paragraphs) => new W.TextBoxContent(paragraphs).OuterXml;

    private static string TextBoxDrawing(string content) => $"""
        <w:drawing xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                   xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                   xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                   xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape">
          <wp:inline distT="0" distB="0" distL="0" distR="0">
            <wp:extent cx="2540000" cy="762000"/>
            <wp:docPr id="1" name="Text Box 1"/>
            <a:graphic>
              <a:graphicData uri="http://schemas.microsoft.com/office/word/2010/wordprocessingShape">
                <wps:wsp>
                  <wps:cNvSpPr txBox="1"/>
                  <wps:spPr>
                    <a:xfrm><a:off x="0" y="0"/><a:ext cx="2540000" cy="762000"/></a:xfrm>
                    <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                  </wps:spPr>
                  <wps:txbx>{content}</wps:txbx>
                  <wps:bodyPr/>
                </wps:wsp>
              </a:graphicData>
            </a:graphic>
          </wp:inline>
        </w:drawing>
        """;

    /// <summary>A table row, each cell holding one paragraph of its text.</summary>
    public static W.TableRow Row(params string[] cells)
        => new(cells.Select(text => new W.TableCell(
            new W.TableCellProperties(new W.TableCellWidth { Width = "3000", Type = W.TableWidthUnitValues.Dxa }),
            Paragraph(text))));

    /// <summary>A table row whose cells each hold their paragraph inside a content control, as a plain-text control does.</summary>
    public static W.TableRow ControlledRow(params string[] cells)
        => new(cells.Select((text, i) => new W.TableCell(
            new W.TableCellProperties(new W.TableCellWidth { Width = "3000", Type = W.TableWidthUnitValues.Dxa }),
            new W.SdtBlock(new W.SdtProperties(new W.SdtId { Val = 100 + i }), new W.SdtContentBlock(Paragraph(text))))));

    /// <summary>A table row formatted as <paramref name="format"/> says, a cell per paragraph.</summary>
    public static W.TableRow Row(RowFormat format, params W.Paragraph[] cells)
    {
        var row = new W.TableRow();
        if (format.Height != null)
        {
            row.AppendChild(new W.TableRowProperties(new W.TableRowHeight { Val = (uint)format.Height }));
        }
        row.Append(cells.Select((paragraph, i) =>
        {
            var properties = new W.TableCellProperties(new W.TableCellWidth { Width = "3000", Type = W.TableWidthUnitValues.Dxa });
            if (format.Shadings.ElementAtOrDefault(i) is { } fill)
            {
                properties.AppendChild(new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = fill });
            }
            return new W.TableCell(properties, paragraph);
        }));
        return row;
    }

    /// <summary>Rows inside a row-level content control, as Word wraps a repeating section.</summary>
    public static W.SdtRow RowControl(int id, params W.TableRow[] rows)
        => new(new W.SdtProperties(new W.SdtId { Val = id }), new W.SdtContentRow(rows));

    /// <summary>A table of the given rows and row-level content controls, as many columns wide as its widest row.</summary>
    public static W.Table TableOf(params OpenXmlElement[] rows)
    {
        var columns = rows.SelectMany(row => row is W.TableRow own ? [own] : row.Descendants<W.TableRow>())
            .Max(row => row.Elements<W.TableCell>().Count());
        var table = new W.Table(
            new W.TableProperties(new W.TableBorders(new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4 })),
            new W.TableGrid(Enumerable.Range(0, columns).Select(_ => new W.GridColumn { Width = "3000" })));
        table.Append(rows);
        return table;
    }

    /// <summary>The table with the given Alt Text title: the title a collection table is found by.</summary>
    public static W.Table Titled(W.Table table, string title)
    {
        table.GetFirstChild<W.TableProperties>()!.AppendChild(new W.TableCaption { Val = title });
        return table;
    }

    /// <summary>
    /// A paragraph holding an embedded object as Word writes one: a VML shape that shows it, and an <c>o:OLEObject</c>
    /// naming that shape by its id. The object's own part is left out: only the markup that ties the two is read.
    /// </summary>
    public static W.Paragraph EmbeddedObject(string shapeId = "_x0000_i1025")
        => new(new W.Run(new W.EmbeddedObject($"""
            <w:object xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                      xmlns:v="urn:schemas-microsoft-com:vml"
                      xmlns:o="urn:schemas-microsoft-com:office:office">
              <v:shape id="{shapeId}" o:spid="_x0000_s1030" style="width:60pt;height:40pt" o:ole=""/>
              <o:OLEObject Type="Embed" ProgID="Package" ShapeID="{shapeId}" DrawAspect="Icon" ObjectID="_1700000000"/>
            </w:object>
            """)));

    /// <summary>
    /// The document with each <see cref="EmbeddedObject"/> made whole, as Word writes one: an embedded <c>.docx</c> part
    /// for the object, and a picture part for the shape that shows it.
    /// </summary>
    public static IMemoryFile WithEmbeddedDocuments(IMemoryFile file)
    {
        // a 1×1 transparent PNG
        var preview = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
        const string relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        using var stream = new MemoryStream();
        stream.Write(file.GetBytes()!);
        using (var doc = WordprocessingDocument.Open(stream, true))
        {
            var main = doc.MainDocumentPart!;
            foreach (var embedded in main.Document!.Body!.Descendants<W.EmbeddedObject>())
            {
                var package = main.AddEmbeddedPackagePart(EmbeddedPackagePartType.Docx);
                using (var content = new MemoryStream(Document("Embedded").GetBytes()!))
                {
                    package.FeedData(content);
                }
                var image = main.AddImagePart(ImagePartType.Png);
                using (var content = new MemoryStream(preview))
                {
                    image.FeedData(content);
                }

                var shape = embedded.Descendants().First(element => element.LocalName == "shape");
                shape.AppendChild(new OpenXmlUnknownElement("v", "imagedata", "urn:schemas-microsoft-com:vml"))
                    .SetAttribute(new OpenXmlAttribute("r", "id", relationships, main.GetIdOfPart(image)));
                var ole = embedded.Descendants().First(element => element.LocalName == "OLEObject");
                ole.SetAttribute(new OpenXmlAttribute("", "ProgID", "", "Word.Document.12"));
                ole.SetAttribute(new OpenXmlAttribute("r", "id", relationships, main.GetIdOfPart(package)));
            }
        }
        return stream.ToArray().ToMemoryFile(ContentTypes.DOCX);
    }

    /// <summary>Every embedded object's <c>o:OLEObject/@ShapeID</c> with the id of the VML shape beside it.</summary>
    public static IReadOnlyList<(string ShapeId, string ShapeIdReferenced)> EmbeddedObjects(IMemoryFile file)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<W.EmbeddedObject>()
            .Select(embedded => (
                embedded.Descendants().First(element => element.LocalName == "shape").GetAttributes().First(attribute => attribute.LocalName == "id").Value ?? "",
                embedded.Descendants().First(element => element.LocalName == "OLEObject").GetAttributes().First(attribute => attribute.LocalName == "ShapeID").Value ?? ""))
            .ToList();
    }

    /// <summary>A paragraph holding an inline group of text boxes (<c>wpg:wgp</c>), one per array of paragraphs.</summary>
    public static W.Paragraph GroupedTextBoxes(params string[][] boxes)
    {
        var shapes = string.Concat(boxes.Select((texts, i) => $"""
            <wps:wsp>
              <wps:cNvPr id="{i + 2}" name="Text Box {i + 2}"/>
              <wps:cNvSpPr txBox="1"/>
              <wps:spPr>
                <a:xfrm><a:off x="0" y="{i * 800000}"/><a:ext cx="2540000" cy="762000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
              </wps:spPr>
              <wps:txbx>{TextBoxContent(Paragraphs(texts))}</wps:txbx>
              <wps:bodyPr/>
            </wps:wsp>
            """));
        var height = boxes.Length * 800000;
        var drawing = $"""
            <w:drawing xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                       xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                       xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                       xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape"
                       xmlns:wpg="http://schemas.microsoft.com/office/word/2010/wordprocessingGroup">
              <wp:inline distT="0" distB="0" distL="0" distR="0">
                <wp:extent cx="2540000" cy="{height}"/>
                <wp:docPr id="1" name="Group 1"/>
                <a:graphic>
                  <a:graphicData uri="http://schemas.microsoft.com/office/word/2010/wordprocessingGroup">
                    <wpg:wgp>
                      <wpg:cNvGrpSpPr/>
                      <wpg:grpSpPr>
                        <a:xfrm><a:off x="0" y="0"/><a:ext cx="2540000" cy="{height}"/><a:chOff x="0" y="0"/><a:chExt cx="2540000" cy="{height}"/></a:xfrm>
                      </wpg:grpSpPr>
                      {shapes}
                    </wpg:wgp>
                  </a:graphicData>
                </a:graphic>
              </wp:inline>
            </w:drawing>
            """;
        return new W.Paragraph(new W.Run(new W.Drawing(drawing)));
    }

    /// <summary>A block-level content control holding the given paragraphs.</summary>
    public static W.SdtBlock ContentControl(params string[] texts)
        => new(new W.SdtProperties(new W.SdtId { Val = 1 }), new W.SdtContentBlock(Paragraphs(texts)));

    /// <summary>A table of one row; each cell holds the given paragraphs.</summary>
    public static W.Table Table(params string[][] cells)
    {
        var row = new W.TableRow();
        foreach (var texts in cells)
        {
            var cell = new W.TableCell(new W.TableCellProperties(new W.TableCellWidth { Width = "3000", Type = W.TableWidthUnitValues.Dxa }));
            cell.Append(Paragraphs(texts));
            row.AppendChild(cell);
        }

        var table = new W.Table(
            new W.TableProperties(new W.TableBorders(new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4 })),
            new W.TableGrid(cells.Select(_ => new W.GridColumn { Width = "3000" })));
        table.AppendChild(row);
        return table;
    }

    /// <summary>A paragraph of the given text inside a bookmark of the given name.</summary>
    public static W.Paragraph Bookmarked(string text, string name, int id = 1)
        => new(new W.BookmarkStart { Name = name, Id = id.ToString() }, new W.Run(new W.Text(text)), new W.BookmarkEnd { Id = id.ToString() });

    /// <summary>
    /// The ids and names the document holds more than once, though Word needs them unique: drawing ids
    /// (<c>wp:docPr</c>, a text box's <c>mc:Fallback</c> copy left out, since it repeats its <c>mc:Choice</c> copy's
    /// on purpose), bookmark names and content-control ids, in the body, headers and footers.
    /// </summary>
    public static IReadOnlyList<string> DuplicateIds(IMemoryFile file)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        var stories = Stories(doc).ToArray();
        var drawings = stories.SelectMany(story => story.Descendants<DW.DocProperties>())
            .Where(properties => !properties.Ancestors<AlternateContentFallback>().Any())
            .Select(properties => $"wp:docPr {properties.Id?.Value}");
        var bookmarks = stories.SelectMany(story => story.Descendants<W.BookmarkStart>()).Select(bookmark => $"bookmark {bookmark.Name?.Value}");
        var controls = stories.SelectMany(story => story.Descendants<W.SdtId>()).Select(control => $"w:sdt {control.Val?.Value}");
        return drawings.Concat(bookmarks).Concat(controls)
            .GroupBy(id => id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
    }

    /// <summary>The names of the bookmarks in the body, in order.</summary>
    public static IReadOnlyList<string> BookmarkNames(IMemoryFile file)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<W.BookmarkStart>()
            .Select(bookmark => bookmark.Name?.Value ?? "")
            .Where(name => !name.StartsWith('_'))
            .ToList();
    }

    /// <summary>The Open XML SDK validator's errors for the document, as Office 2019 reads it.</summary>
    public static IReadOnlyList<string> ValidationErrors(IMemoryFile file)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        return new DocumentFormat.OpenXml.Validation.OpenXmlValidator(FileFormatVersions.Office2019)
            .Validate(doc)
            .Select(error => $"{error.Part?.Uri} {error.Path?.XPath}: {error.Description}")
            .ToList();
    }

    /// <summary>A paragraph of the given text that references footnote 1.</summary>
    public static W.Paragraph FootnoteReference(string text)
        => new(new W.Run(new W.Text(text)), new W.Run(new W.FootnoteReference { Id = 1 }));

    /// <summary>
    /// A document of the given body content, with the given content as its default header and as footnote 1, which
    /// <see cref="FootnoteReference"/> refers to, on A4 pages — turned to landscape when asked. Given a
    /// <paramref name="normalFontSize"/>, in half-points, its Normal style sets that size and no space between paragraphs.
    /// Given <paramref name="styles"/>, its styles part defines them too.
    /// </summary>
    public static IMemoryFile Document(IEnumerable<OpenXmlElement> body, IEnumerable<OpenXmlElement>? header = null, IEnumerable<OpenXmlElement>? footnote = null,
        bool landscape = false, int? normalFontSize = null, IEnumerable<W.Style>? styles = null)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            if (normalFontSize != null || styles != null)
            {
                var definitions = new W.Styles();
                if (normalFontSize is { } fontSize)
                {
                    definitions.AppendChild(new W.Style(
                        new W.StyleName { Val = "Normal" },
                        new W.StyleParagraphProperties(new W.SpacingBetweenLines { After = "0", Line = "240", LineRule = W.LineSpacingRuleValues.Auto }),
                        new W.StyleRunProperties(new W.FontSize { Val = fontSize.ToString() }))
                    {
                        Type = W.StyleValues.Paragraph,
                        StyleId = "Normal",
                        Default = true
                    });
                }
                definitions.Append(styles ?? []);
                main.AddNewPart<StyleDefinitionsPart>().Styles = definitions;
            }
            var sectionProperties = new W.SectionProperties();
            if (header != null)
            {
                var headerPart = main.AddNewPart<HeaderPart>();
                headerPart.Header = new W.Header(header);
                sectionProperties.AppendChild(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(headerPart) });
            }
            if (footnote != null)
            {
                var footnotesPart = main.AddNewPart<FootnotesPart>();
                footnotesPart.Footnotes = new W.Footnotes(
                    new W.Footnote(new W.Paragraph(new W.Run(new W.SeparatorMark()))) { Type = W.FootnoteEndnoteValues.Separator, Id = -1 },
                    new W.Footnote(new W.Paragraph(new W.Run(new W.ContinuationSeparatorMark()))) { Type = W.FootnoteEndnoteValues.ContinuationSeparator, Id = 0 },
                    new W.Footnote(footnote) { Id = 1 });
            }

            // after the header reference: a section's properties keep the schema's order
            sectionProperties.AppendChild(landscape
                ? new W.PageSize { Width = (uint)Facts.A4Twips.Height, Height = (uint)Facts.A4Twips.Width, Orient = W.PageOrientationValues.Landscape }
                : A4());
            var content = new W.Body(body);
            content.AppendChild(sectionProperties);
            main.Document = new W.Document(content);
        }
        return stream.ToArray().ToMemoryFile(ContentTypes.DOCX);
    }

    public static IMemoryFile Document(params string[] paragraphs) => Document(Paragraphs(paragraphs));

    /// <summary>The document with each picture in its body made the given size, in points.</summary>
    public static IMemoryFile WithPictureSize(IBinaryFile file, double width, double height)
    {
        using var stream = new MemoryStream();
        stream.Write(file.GetBytes()!);
        using (var doc = WordprocessingDocument.Open(stream, true))
        {
            foreach (var drawing in doc.MainDocumentPart!.Document!.Body!.Descendants<W.Drawing>())
            {
                // wp:extent sizes the picture in the text flow, a:ext the graphic inside it
                foreach (var extent in drawing.Descendants<DW.Extent>())
                {
                    (extent.Cx, extent.Cy) = ((long)Math.Round(width * 12700), (long)Math.Round(height * 12700));
                }
                foreach (var extent in drawing.Descendants<A.Extents>())
                {
                    (extent.Cx, extent.Cy) = ((long)Math.Round(width * 12700), (long)Math.Round(height * 12700));
                }
            }
        }
        return stream.ToArray().ToMemoryFile(ContentTypes.DOCX);
    }

    /// <summary>The document with each picture in its body made the given width, in points, keeping its proportions.</summary>
    public static IMemoryFile WithPictureWidth(IBinaryFile file, double width)
    {
        using var stream = new MemoryStream();
        stream.Write(file.GetBytes()!);
        using (var doc = WordprocessingDocument.Open(stream, true))
        {
            foreach (var drawing in doc.MainDocumentPart!.Document!.Body!.Descendants<W.Drawing>())
            {
                var cx = drawing.Descendants<DW.Extent>().FirstOrDefault()?.Cx?.Value;
                if (cx is not > 0)
                {
                    continue;
                }
                // wp:extent sizes the picture in the text flow, a:ext the graphic inside it
                var scale = width * 12700 / cx.Value;
                foreach (var extent in drawing.Descendants<DW.Extent>())
                {
                    (extent.Cx, extent.Cy) = ((long)Math.Round(extent.Cx!.Value * scale), (long)Math.Round(extent.Cy!.Value * scale));
                }
                foreach (var extent in drawing.Descendants<A.Extents>())
                {
                    (extent.Cx, extent.Cy) = ((long)Math.Round(extent.Cx!.Value * scale), (long)Math.Round(extent.Cy!.Value * scale));
                }
            }
        }
        return stream.ToArray().ToMemoryFile(ContentTypes.DOCX);
    }

    /// <summary>The text of every header part, in part order.</summary>
    public static string HeaderText(IMemoryFile file)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        return string.Join("\n", doc.MainDocumentPart!.HeaderParts.Select(part => part.Header?.InnerText));
    }

    /// <summary>The text of every footnote, separators included.</summary>
    public static string FootnoteText(IMemoryFile file)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        return string.Join("\n", doc.MainDocumentPart!.FootnotesPart?.Footnotes?.Descendants<W.Paragraph>().Select(paragraph => paragraph.InnerText) ?? []);
    }

    /// <summary>Whether every table cell ends with a paragraph, as Word requires.</summary>
    public static bool CellsEndWithParagraphs(IMemoryFile file)
        => EndWithParagraphs<W.TableCell>(file);

    /// <summary>
    /// Whether every table cell, text box and content control in the body ends with a paragraph, as Word requires.
    /// </summary>
    public static bool TextBodiesEndWithParagraphs(IMemoryFile file)
        => EndWithParagraphs<W.TableCell>(file) && EndWithParagraphs<W.TextBoxContent>(file) && EndWithParagraphs<W.SdtContentBlock>(file);

    private static bool EndWithParagraphs<TContainer>(IMemoryFile file)
        where TContainer : OpenXmlElement
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        return doc.MainDocumentPart!.Document!.Body!
            .Descendants<TContainer>()
            .All(container => container.ChildElements.LastOrDefault(child => child is W.Paragraph or W.Table or W.SdtBlock) is W.Paragraph);
    }

    /// <summary>The body's visible text, a paragraph a line: deleted text is left out.</summary>
    public static string VisibleText(IMemoryFile file)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        return string.Join("\n", doc.MainDocumentPart!.Document!.Body!.Descendants<W.Paragraph>()
            .Select(paragraph => string.Concat(paragraph.Descendants<W.Text>().Select(text => text.Text))));
    }

    /// <summary>The text of the body, text boxes included.</summary>
    public static string BodyText(IMemoryFile file)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        return string.Join("\n", doc.MainDocumentPart!.Document!.Body!.Descendants<W.Paragraph>().Select(paragraph => paragraph.InnerText));
    }

    /// <summary>
    /// The paragraphs of the body, headers and footers that still hold a tag, <c>{{</c> or <c>&lt;{</c>: each read
    /// without the text boxes inside it, and marked when it sits in an <c>mc:Fallback</c>. Read from the package itself,
    /// so the copy a backend's own reader passes over is read too.
    /// </summary>
    public static IReadOnlyList<string> Leftovers(IMemoryFile file)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        return Stories(doc)
            .SelectMany(story => story.Descendants<W.Paragraph>())
            .Select(paragraph => (Paragraph: paragraph, Text: OwnText(paragraph)))
            .Where(paragraph => paragraph.Text.Contains("{{") || paragraph.Text.Contains("<{"))
            .Select(paragraph => paragraph.Paragraph.Ancestors<AlternateContentFallback>().Any() ? $"{paragraph.Text} (mc:Fallback)" : paragraph.Text)
            .ToList();
    }

    /// <summary>
    /// Every text box the body, headers and footers hold in two copies, DrawingML in <c>mc:Choice</c> and VML in
    /// <c>mc:Fallback</c>: the text of each copy, a paragraph a line.
    /// </summary>
    public static IReadOnlyList<(string Choice, string Fallback)> TextBoxCopies(IMemoryFile file)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        return Stories(doc)
            .SelectMany(story => story.Descendants<AlternateContent>())
            .Select(alternate => (Choice: alternate.GetFirstChild<AlternateContentChoice>(), Fallback: alternate.GetFirstChild<AlternateContentFallback>()))
            .Where(copies => copies.Choice?.Descendants<W.TextBoxContent>().Any() == true && copies.Fallback != null)
            .Select(copies => (TextBoxText(copies.Choice!), TextBoxText(copies.Fallback!)))
            .ToList();

        static string TextBoxText(OpenXmlElement copy)
            => string.Join("\n", copy.Descendants<W.TextBoxContent>().SelectMany(box => box.Descendants<W.Paragraph>()).Select(OwnText));
    }

    private static IEnumerable<OpenXmlElement> Stories(WordprocessingDocument doc)
    {
        var main = doc.MainDocumentPart!;
        OpenXmlElement?[] stories = [main.Document?.Body, .. main.HeaderParts.Select(part => part.Header), .. main.FooterParts.Select(part => part.Footer)];
        return stories.OfType<OpenXmlElement>();
    }

    /// <summary>The paragraph's text, without that of a text box inside it.</summary>
    private static string OwnText(W.Paragraph paragraph)
        => string.Concat(paragraph.Descendants<W.Text>()
            .Where(text => text.Ancestors<W.Paragraph>().First() == paragraph)
            .Select(text => text.Text));

    /// <summary>The number of <typeparamref name="TElement"/> elements in the body.</summary>
    public static int Count<TElement>(IMemoryFile file)
        where TElement : OpenXmlElement
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<TElement>().Count();
    }
}
