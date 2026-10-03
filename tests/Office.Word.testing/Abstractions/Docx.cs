using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.MimeTypes;
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
        properties.AppendChild(new W.SectionProperties(new W.SectionType { Val = W.SectionMarkValues.NextPage }));
        return new W.Paragraph(properties, new W.Run(new W.Text(text)));
    }

    /// <summary>A paragraph holding an inline text box of the given paragraphs.</summary>
    public static W.Paragraph TextBox(params string[] texts)
    {
        var content = new W.TextBoxContent(Paragraphs(texts));
        var drawing = $"""
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
                      <wps:txbx>{content.OuterXml}</wps:txbx>
                      <wps:bodyPr/>
                    </wps:wsp>
                  </a:graphicData>
                </a:graphic>
              </wp:inline>
            </w:drawing>
            """;
        return new W.Paragraph(new W.Run(new W.Drawing(drawing)));
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
              <wps:txbx>{new W.TextBoxContent(Paragraphs(texts)).OuterXml}</wps:txbx>
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

    /// <summary>A paragraph of the given text that references footnote 1.</summary>
    public static W.Paragraph FootnoteReference(string text)
        => new(new W.Run(new W.Text(text)), new W.Run(new W.FootnoteReference { Id = 1 }));

    /// <summary>
    /// A document of the given body content, with the given content as its default header and as footnote 1, which
    /// <see cref="FootnoteReference"/> refers to.
    /// </summary>
    public static IMemoryFile Document(IEnumerable<OpenXmlElement> body, IEnumerable<OpenXmlElement>? header = null, IEnumerable<OpenXmlElement>? footnote = null)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
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

            var content = new W.Body(body);
            content.AppendChild(sectionProperties);
            main.Document = new W.Document(content);
        }
        return stream.ToArray().ToMemoryFile(ContentTypes.DOCX);
    }

    public static IMemoryFile Document(params string[] paragraphs) => Document(Paragraphs(paragraphs));

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

    /// <summary>The number of <typeparamref name="TElement"/> elements in the body.</summary>
    public static int Count<TElement>(IMemoryFile file)
        where TElement : OpenXmlElement
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<TElement>().Count();
    }

    public static int TableCount(IMemoryFile file)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<W.Table>().Count();
    }
}
