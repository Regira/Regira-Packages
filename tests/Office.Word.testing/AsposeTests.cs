using Office.Word.testing.Abstractions;
using Regira.IO.Abstractions;
using Regira.Office.Models;
using Regira.Office.MimeTypes;
using Regira.Office.Word.Aspose;
using Regira.Office.Word.Models;

namespace Office.Word.testing;

/// <summary>
/// Word.Aspose runs every shared scenario in <see cref="WordTestsBase"/>; below are its case lists, its licence and
/// what it does differently.
/// </summary>
/// <remarks>
/// Runs with or without a licence. Unlicensed, Aspose.Words watermarks every document and truncates long
/// ones; the shared scenarios check what is present or absent and count no paragraphs, so they pass against that
/// evaluation output too. What a licence changes is covered by the two licence tests, which run only when one is
/// configured (<c>ASPOSE_WORDS_LICENSE</c>, Base64, or <c>ASPOSE_WORDS_LICENSE_PATH</c>), and by
/// <see cref="AsposeEvaluationTests"/>, which runs only when none is and proves those two checks fail
/// against evaluation output.
/// </remarks>
[WordFixture]
[Category("License")]
public class AsposeTests() : WordTestsBase(new WordService(LicenseFromEnvironment()), "Aspose")
{
    /// <summary>
    /// Words the evaluation watermark carries. Deliberately broad: they match whatever wording Aspose
    /// uses, and none of the test assets contains either.
    /// </summary>
    internal static readonly string[] EvaluationMarkers = ["Aspose", "Evaluation"];

    internal static bool HasLicence
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AsposeLicense.LicenseVariable))
           || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AsposeLicense.LicensePathVariable));

    /// <summary>
    /// The licence handed over through <see cref="AsposeWordConfig"/>, the way an application configures
    /// it. Nothing else in the suite applies a licence, so a licensed run exercises this route; the
    /// environment fallback is covered by <see cref="AsposeLicenseTests"/>. Without one the shared scenarios
    /// run on evaluation output, which they tolerate, so this fixture opts into it.
    /// </summary>
    internal static AsposeWordConfig LicenseFromEnvironment() => new()
    {
        LicenseBase64 = Environment.GetEnvironmentVariable(AsposeLicense.LicenseVariable),
        LicensePath = Environment.GetEnvironmentVariable(AsposeLicense.LicensePathVariable),
        AllowEvaluation = true
    };

    public static IEnumerable<string> SourceFiles => ["template.dot", "template.doc", "template.odt", "multipage.docx"];

    public static IEnumerable<TestCaseData> OutputFormats =>
    [
        Output(FileFormat.Pdf, "application/pdf"),
        Output(FileFormat.Html, "text/html"),
        Output(FileFormat.Rtf, "text/rtf"),
        Output(FileFormat.Odt, "application/vnd.oasis.opendocument.text"),
        Output(FileFormat.EPub, "application/epub+zip"),
        Output(FileFormat.Doc, ContentTypes.DOC),
        // a template is tagged as a .docx: one content type for every Open XML word-processing format
        Output(FileFormat.Dotx, ContentTypes.DOCX)
    ];

    protected override Task<IMemoryFile> Build(IEnumerable<Paragraph> paragraphs, IEnumerable<WordHeaderFooterInput> headers, ConversionOptions? conversion = null,
        WordTemplateInput[]? inputs = null, MergeOptions? merge = null, DocumentSettings? settings = null)
    {
        var builder = new DocumentBuilder((WordService)Backend).WithParagraphs(paragraphs);
        if (inputs != null)
        {
            builder.Load(inputs);
        }
        if (merge != null)
        {
            builder.WithMerge(merge);
        }
        if (settings != null)
        {
            builder.WithSettings(new WordDocumentSettings { PageSize = settings.PageSize, PageOrientation = settings.PageOrientation });
        }
        foreach (var header in headers)
        {
            builder.AddHeader(header);
        }
        return (conversion == null ? builder : builder.WithConversion(conversion)).Build();
    }

    private static void RequireLicence()
    {
        if (!HasLicence)
        {
            Assert.Ignore(
                $"Set {AsposeLicense.LicenseVariable} (the licence file, Base64-encoded) or {AsposeLicense.LicensePathVariable} " +
                "to verify the licence lifts the evaluation limits.");
        }
    }


    /// <summary>
    /// Answers the licensing question outright: a licence that does not cover Aspose.Words leaves the
    /// evaluation banner in, and every shared scenario still passes against it because the banner is only
    /// additional text.
    /// </summary>
    [Test]
    public async Task License_Removes_The_Evaluation_Watermark()
    {
        RequireLicence();

        var text = await Service.GetText(TemplateInput("lorem_ipsum.docx"));
        // a watermark can also be a shape, which only shows once the page is rendered
        using var pdf = await Service.Convert(TemplateInput("lorem_ipsum.docx"), FileFormat.Pdf);
        var pdfText = string.Join("\n", (await ReadPdf(pdf)).PageTexts);

        Assert.Multiple(() =>
        {
            foreach (var marker in EvaluationMarkers)
            {
                Assert.That(text, Does.Not.Contain(marker).IgnoreCase, "The licence does not cover Aspose.Words: the text carries the evaluation watermark.");
                Assert.That(pdfText, Does.Not.Contain(marker).IgnoreCase, "The licence does not cover Aspose.Words: the rendered pages carry the evaluation watermark.");
            }
        });
    }

    [Test]
    public async Task Long_Document_Is_Not_Truncated()
    {
        RequireLicence();

        // evaluation mode stops after a few hundred paragraphs, whatever its watermark says
        var text = await Service.GetText(new WordTemplateInput { Template = LongDocument() });

        Assert.That(text, Does.Contain(LongDocumentLastParagraph));
    }

    [TestCase(HeaderFooterType.Even, true)]
    [TestCase(HeaderFooterType.Even, false)]
    [TestCase(HeaderFooterType.FirstPage, true)]
    [TestCase(HeaderFooterType.FirstPage, false)]
    public override Task A_Story_Given_For_Some_Pages_Leaves_The_Other_Story_On_Them(HeaderFooterType type, bool specialHeader)
    {
        // evaluation mode replaces every header and footer with its own banner, so only licensed output shows them
        RequireLicence();
        return base.A_Story_Given_For_Some_Pages_Leaves_The_Other_Story_On_Them(type, specialHeader);
    }

    [Test]
    public async Task Odt_Template_Is_Read()
    {
        var text = await Service.GetText(TemplateInput("template.odt"));

        Assert.That(text, Is.Not.Empty);
    }


    private const int LongDocumentParagraphCount = 1000;
    internal static readonly string LongDocumentLastParagraph = $"Paragraph {LongDocumentParagraphCount} of {LongDocumentParagraphCount}";

    /// <summary>
    /// Well past the evaluation mode's paragraph limit.
    /// </summary>
    internal static IMemoryFile LongDocument()
        => Docx.Document(Enumerable.Range(1, LongDocumentParagraphCount)
            .Select(i => $"Paragraph {i} of {LongDocumentParagraphCount}")
            .ToArray());
}
