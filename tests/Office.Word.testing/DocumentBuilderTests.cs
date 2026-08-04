using Regira.IO.Extensions;
using Regira.Office.Word.Models;
using Regira.Office.Word.Spire;
using Regira.Utilities;

namespace Office.Word.testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class DocumentBuilderTests
{
    private readonly string _assetsDir;
    public DocumentBuilderTests()
    {
        var assemblyDir = AssemblyUtility.GetAssemblyDirectory()!;
        _assetsDir = Path.Combine(assemblyDir, "../../../", "Assets");
        Directory.CreateDirectory(Path.Combine(_assetsDir, "Output"));
    }

    [Test]
    public async Task Create()
    {
        var img = await File.ReadAllBytesAsync(Path.Combine(_assetsDir, "Input", "sample1.jpg"));
        var fpHeaderPath = Path.Combine(_assetsDir, "Input", "firstpage_header.docx");
        var headerPath = Path.Combine(_assetsDir, "Input", "add_header.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "lorem-ipsum.docx");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

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

        var manager = new WordManager();
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
}