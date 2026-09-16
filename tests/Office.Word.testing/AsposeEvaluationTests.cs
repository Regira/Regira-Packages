using Office.Word.testing.Abstractions;
using Regira.Drawing.SkiaSharp.Services;
using Regira.Office.Models;
using Regira.Office.PDF.DocNET;
using Regira.Office.Word.Aspose;
using Regira.Office.Word.Models;

namespace Office.Word.testing;

/// <summary>
/// The counterparts of <see cref="AsposeTests.License_Removes_The_Evaluation_Watermark"/> and
/// <see cref="AsposeTests.Long_Document_Is_Not_Truncated"/>: unlicensed, the same checks must fail, or
/// their passing proves nothing.
/// </summary>
/// <remarks>
/// A licence applies to the whole process and cannot be withdrawn, so this only runs when none is
/// configured — which is exactly when those two checks skip themselves. Handing Aspose a licence it
/// rejects is tested here for the same reason: it must never be tried in a process that holds a real one.
/// </remarks>
[TestFixture]
[Category("License")]
public class AsposeEvaluationTests() : WordAssetsTestsBase("Aspose")
{
    private static readonly AsposeWordConfig Evaluation = new() { AllowEvaluation = true };

    [SetUp]
    public void RequireNoLicence()
    {
        if (AsposeTests.HasLicence)
        {
            Assert.Ignore("A licence is configured, and it licenses the whole process: evaluation output cannot be produced here.");
        }
    }

    [Test]
    public async Task Unlicensed_Output_Is_Truncated()
    {
        var text = await new WordService(Evaluation).GetText(new WordTemplateInput { Template = AsposeTests.LongDocument() });

        Assert.That(text, Does.Not.Contain(AsposeTests.LongDocumentLastParagraph),
            "Unlicensed output keeps the whole long document; the truncation check would pass regardless.");
    }

    [Test]
    public async Task Unlicensed_Output_Carries_An_Evaluation_Marker()
    {
        var service = new WordService(Evaluation);
        var text = await service.GetText(TemplateInput("lorem_ipsum.docx"));
        using var pdf = await service.Convert(TemplateInput("lorem_ipsum.docx"), FileFormat.Pdf);
        var pdfText = await new PdfManager(new ImageService()).GetText(pdf);

        TestContext.Out.WriteLine($"Evaluation text:{Environment.NewLine}{text[..Math.Min(text.Length, 400)]}");

        Assert.Multiple(() =>
        {
            Assert.That(AsposeTests.EvaluationMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase)), Is.True,
                "Unlicensed text output carries none of the evaluation markers; the licence check would pass regardless.");
            Assert.That(AsposeTests.EvaluationMarkers.Any(marker => pdfText.Contains(marker, StringComparison.OrdinalIgnoreCase)), Is.True,
                "Unlicensed PDF output carries none of the evaluation markers; the licence check would pass regardless.");
        });
    }

    [Test]
    public void No_License_Fails_Unless_Evaluation_Is_Allowed()
    {
        // a setting that resolves to nothing — LicensePath = configuration["Aspose:LicensePath"] with the key missing
        var ex = Assert.Throws<InvalidOperationException>(() => AsposeLicense.Register(new AsposeWordConfig { LicensePath = null }, _ => null));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain(nameof(AsposeWordConfig.AllowEvaluation)));
            Assert.DoesNotThrow(() => AsposeLicense.Register(Evaluation, _ => null));
        });
    }

    [Test]
    public void A_Rejected_License_Names_Its_Source()
    {
        var notALicense = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("<License><Data>not signed</Data></License>"));

        var ex = Assert.Throws<InvalidOperationException>(() => _ = new WordService(new AsposeWordConfig { LicenseBase64 = notALicense }));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain($"{nameof(AsposeWordConfig)}.{nameof(AsposeWordConfig.LicenseBase64)}"));
            // Aspose's own reason is kept
            Assert.That(ex.InnerException, Is.Not.Null);
        });
    }
}
