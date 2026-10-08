using System.Runtime.Versioning;
using Office.PDF.Testing.Abstractions;
using Regira.Office.PDF.Spire;

namespace Office.PDF.Testing;

/// <summary>
/// PDF.Spire runs the shared scenarios in <see cref="PdfTestsBase{TBackend}"/> for the interfaces it implements,
/// within the limits of FreeSpire.PDF's free edition, which the tests below pin: a vendor upgrade that moves them
/// fails here, and the guides that state them need updating.
/// </summary>
[PdfFixture]
[SupportedOSPlatform("windows")]
public class SpireTests() : PdfTestsBase<PdfManager>(new PdfManager(), "Spire")
{
    protected override int MaxPages => 10;
    protected override int RenderedPages => 3;

    [Test]
    public async Task Free_Edition_Refuses_To_Load_More_Than_Ten_Pages()
    {
        await Assert.CatchAsync(() => Backend.GetPageCount(ReadAsset("lorem-24-pages.pdf")));
    }

    [Test]
    public async Task Free_Edition_Refuses_To_Merge_Past_Ten_Pages()
    {
        var names = Enumerable.Range(1, 6).Select(i => $"lorem-ipsum{i}.pdf").ToList();
        Assert.That(names.Sum(name => InputFacts(name).PageCount), Is.GreaterThan(10), "the inputs pass ten pages");

        await Assert.CatchAsync(() => Backend.Merge(names.Select(ReadAsset).ToList()));
    }
}
