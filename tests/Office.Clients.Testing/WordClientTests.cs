using Microsoft.Extensions.DependencyInjection;
using Office.Clients.Testing.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Office.Models;
using Regira.Office.Word.Abstractions;
using Regira.Office.Word.Models;

namespace Office.Clients.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class WordClientTests : OfficeClientTestsBase
{
    private readonly string _inputDir;
    private readonly string _outputDir;

    private IWordCreator _creator = null!;
    private IWordConverter _converter = null!;
    private IWordMerger _merger = null!;
    private IWordTextExtractor _textExtractor = null!;

    public WordClientTests()
    {
        var testsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        _inputDir = Path.Combine(testsDir, "Office.Word.testing", "Assets", "Input");
        _outputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Assets/Output"));
        Directory.CreateDirectory(_outputDir);
    }

    [SetUp]
    public void Setup()
    {
        _creator = Services.GetRequiredService<IWordCreator>();
        _converter = Services.GetRequiredService<IWordConverter>();
        _merger = Services.GetRequiredService<IWordMerger>();
        _textExtractor = Services.GetRequiredService<IWordTextExtractor>();
    }

    private async Task<WordTemplateInput> LoadTemplate(string fileName)
    {
        var bytes = await File.ReadAllBytesAsync(Path.Combine(_inputDir, fileName));
        return new WordTemplateInput { Template = new BinaryFileItem { Bytes = bytes } };
    }

    [Test]
    public async Task Create_Word()
    {
        var input = await LoadTemplate("doc-1.docx");

        using var result = await _creator.Create(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.GetLength() > 0, Is.True);
    }

    [Test]
    public async Task Convert_To_Pdf()
    {
        var input = await LoadTemplate("doc-1.docx");

        using var result = await _converter.Convert(input, FileFormat.Pdf);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.GetLength() > 0, Is.True);
    }

    [Test]
    public async Task Merge_Documents()
    {
        var input1 = await LoadTemplate("doc-1.docx");
        var input2 = await LoadTemplate("doc-2.docx");

        using var result = await _merger.Merge([input1, input2]);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.GetLength() > 0, Is.True);
    }

    [Test]
    public async Task GetText()
    {
        var input = await LoadTemplate("doc-1.docx");

        var text = await _textExtractor.GetText(input);

        Assert.That(text, Is.Not.Null);
        Assert.That(text, Is.Not.Empty);
    }
}
