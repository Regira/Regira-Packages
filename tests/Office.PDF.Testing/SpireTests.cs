using System.Runtime.Versioning;
using Office.PDF.Testing.Abstractions;
using Regira.Office.PDF.Spire;

namespace Office.PDF.Testing;

/// <summary>
/// PDF.Spire runs the shared scenarios in <see cref="PdfTestsBase{TBackend}"/> for the interfaces it implements,
/// within the limits of FreeSpire.PDF's free edition, which the tests below pin: a vendor upgrade that moves them
/// fails here, and the guides that state them need updating.
/// </summary>
[TestFixture]
[SupportedOSPlatform("windows")]
public class SpireTests() : PdfTestsBase<PdfManager>(new PdfManager(), "Spire")
{
    protected override int MaxPages => 10;
    protected override int RenderedPages => 3;

    // the assets are read before the call, so a missing one fails the test instead of passing for the refusal
    [Test]
    public async Task Free_Edition_Refuses_To_Load_More_Than_Ten_Pages()
    {
        var pdf = ReadAsset("lorem-24-pages.pdf");

        var refusal = await Assert.ThrowsAsync<Exception>(() => Backend.GetPageCount(pdf));
        Assert.That(refusal!.Message, Does.Contain("limited to 10 pages"));
    }

    [Test]
    public async Task Free_Edition_Refuses_To_Merge_Past_Ten_Pages()
    {
        var names = Enumerable.Range(1, 6).Select(i => $"lorem-ipsum{i}.pdf").ToList();
        Assert.That(names.Sum(name => InputFacts(name).PageCount), Is.GreaterThan(10), "the inputs pass ten pages");
        var pdfs = names.Select(ReadAsset).ToList();

        var refusal = await Assert.ThrowsAsync<Exception>(() => Backend.Merge(pdfs));
        Assert.That(refusal!.Message, Does.Contain("limited to 10 pages"));
    }
}
