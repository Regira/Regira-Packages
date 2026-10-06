using Microsoft.Extensions.Configuration;
using Office.Word.testing.Abstractions;
using Regira.IO.Abstractions;
using Regira.Office.Models;
using Regira.Office.Word.Models;
using Regira.Office.Word.Syncfusion;

namespace Office.Word.testing;

/// <summary>
/// Word.Syncfusion runs every shared scenario in <see cref="WordTestsBase"/>; below are its case lists, its licence
/// and what DocIO does differently.
/// </summary>
[WordFixture]
[Category("License")]
public class SyncfusionTests() : WordTestsBase(CreateService(), "Syncfusion")
{
    private const string LicenseVariable = "SYNCFUSION_LICENSE_KEY";
    private const string LicenseSetting = "SyncFusion:LicenseKey";

    /// <summary>
    /// The key comes from user secrets (<c>dotnet user-secrets set "SyncFusion:LicenseKey" "..."</c>),
    /// falling back to the <c>SYNCFUSION_LICENSE_KEY</c> environment variable the package itself reads,
    /// so CI can hand it over without a secrets file. Null when neither is set.
    /// </summary>
    private static readonly string? LicenseKey =
        new ConfigurationBuilder()
            .AddUserSecrets(typeof(SyncfusionTests).Assembly, optional: true)
            .Build()[LicenseSetting]
        ?? Environment.GetEnvironmentVariable(LicenseVariable);

    /// <summary>
    /// The banner DocIO prepends to every document when the key is missing, wrong, or does not
    /// cover the Word library.
    /// </summary>
    private const string TrialBanner = "trial version of Syncfusion Word library";

    private static WordService CreateService()
        => new(new SyncfusionWordConfig { LicenseKey = LicenseKey });

    // template.odt is absent: DocIO cannot load ODT (see Odt_Template_Is_Not_Supported).
    public static IEnumerable<string> SourceFiles => ["template.dot", "template.doc", "multipage.docx"];

    // EPub is absent: unavailable on .NET Core (see Convert_To_EPub_Is_Not_Supported).
    public static IEnumerable<TestCaseData> OutputFormats =>
    [
        Output(FileFormat.Pdf, "application/pdf"),
        Output(FileFormat.Html, "text/html"),
        Output(FileFormat.Rtf, "text/rtf"),
        Output(FileFormat.Odt, "application/vnd.oasis.opendocument.text")
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


    /// <summary>
    /// Answers the licensing question outright: a key that does not license DocIO produces a
    /// watermarked document, and every other assertion in this fixture still passes against one
    /// because the banner is only additional text.
    /// </summary>
    [Test]
    public async Task License_Key_Removes_The_Trial_Banner()
    {
        if (string.IsNullOrWhiteSpace(LicenseKey))
        {
            // Only this test needs a key. The rest run unlicensed: the banner is extra text, so it
            // does not affect what they assert.
            Assert.Ignore($"Set the {LicenseSetting} user secret or {LicenseVariable} to verify the key licenses DocIO.");
        }

        var text = await Service.GetText(TemplateInput("lorem_ipsum.docx"));

        Assert.That(text, Does.Not.Contain(TrialBanner),
            $"The configured key ({LicenseSetting} / {LicenseVariable}) does not license the DocIO Word library — output is watermarked.");
    }

    [Test]
    public async Task Odt_Template_Is_Not_Supported()
    {
        // DocIO can save ODT but cannot load it.
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => Service.Create(TemplateInput("template.odt")));
        Assert.That(ex!.Message, Does.Contain("ODT"));
    }

    [Test]
    public async Task Convert_To_EPub_Is_Not_Supported()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => Service.Convert(TemplateInput("template.docx"), FileFormat.EPub));
        Assert.That(ex!.Message, Does.Contain("EPUB"));
    }
}
