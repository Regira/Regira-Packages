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

    /// <summary>A paragraph that ends its section, the way Word stores a section break.</summary>
    public static W.Paragraph SectionBreak(string text)
        => new(
            new W.ParagraphProperties(new W.SectionProperties(new W.SectionType { Val = W.SectionMarkValues.NextPage })),
            new W.Run(new W.Text(text)));

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

    /// <summary>A document of the given body content, with the given content as its default header.</summary>
    public static IMemoryFile Document(IEnumerable<OpenXmlElement> body, IEnumerable<OpenXmlElement>? header = null)
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

    /// <summary>Whether every table cell ends with a paragraph, as Word requires.</summary>
    public static bool CellsEndWithParagraphs(IMemoryFile file)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        return doc.MainDocumentPart!.Document!.Body!
            .Descendants<W.TableCell>()
            .All(cell => cell.ChildElements.LastOrDefault(child => child is W.Paragraph or W.Table or W.SdtBlock) is W.Paragraph);
    }

    public static int TableCount(IMemoryFile file)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<W.Table>().Count();
    }
}
