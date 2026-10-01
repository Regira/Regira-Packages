using Office.Word.testing.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Models;
using Regira.Office.Word.Models;
using Regira.Office.Word.Spire;
using Regira.Utilities;

namespace Office.Word.testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class DocumentBuilderTests() : WordAssetsTestsBase("Spire")
{

    [Test]
    public async Task Create()
    {
        var img = await File.ReadAllBytesAsync(InputPath("sample1.jpg"));
        var fpHeaderPath = InputPath("firstpage_header.docx");
        var headerPath = InputPath("add_header.docx");
        var outputPath = OutputPath("lorem-ipsum.docx");

        var paragraphs = LoremIpsum.Paragraphs
            .Select((s, i) => new Paragraph
            {
                Text = s,
                PageBreakAfter = true,
                Image = i % 5 == 0 ? new WordImage
                {
                    File = img.ToBinaryFile(),
                    HorizontalAlignment = (i % 10 == 0) ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                    Size = new(300, 169)
                } : null
            })
            .Repeat(10)
            .Take(99)
            .ToList();

        var headingParagraph = new Paragraph
        {
            Text = "Lorem Ipsum",
            Style = ParagraphStyle.Heading1
        };
        paragraphs.Insert(0, headingParagraph);

        using var fpHeaderFile = File.OpenRead(fpHeaderPath).ToBinaryFile();
        var fpHeader = new WordTemplateInput { Template = fpHeaderFile };
        using var headerFile = File.OpenRead(headerPath).ToBinaryFile();
        var header = new WordTemplateInput { Template = headerFile };

        var manager = new WordService();
        var builder = new DocumentBuilder(manager);
        using var docFile = await builder.WithParagraphs(paragraphs)
            .AddHeader(new WordHeaderFooterInput { Template = fpHeader, Type = HeaderFooterType.FirstPage })
            .AddHeader(new WordHeaderFooterInput { Template = header })
            .Build();

        var file = await docFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        Assert.That(file.Length > 0, Is.True);

        var content = await manager.GetText(new WordTemplateInput { Template = docFile });
        Assert.That(content.Contains(headingParagraph.Text), Is.True);
    }

    // the build is typed by the format it was converted to, as WordService.Convert types it
    [TestCase(FileFormat.Pdf, "application/pdf")]
    [TestCase(FileFormat.Doc, "application/msword")]
    [TestCase(FileFormat.Docx, "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public async Task A_Build_Is_Typed_By_Its_Output_Format(FileFormat format, string contentType)
    {
        using var file = await new DocumentBuilder(new WordService())
            .WithParagraphs([new Paragraph { Text = "Lorem Ipsum" }])
            .WithConversion(new ConversionOptions { OutputFormat = format })
            .Build();

        Assert.That(file.ContentType, Is.EqualTo(contentType));
    }
}