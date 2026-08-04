using Office.PDF.Testing.Abstractions;
using Regira.Office.PDF.Spire;

namespace Office.PDF.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SpirePdfTests
{
    private readonly PdfManager _pdfService = new();

    [Test]
    public Task Split_1to10()
        => PdfTestHelper.Split_Documents(_pdfService, "lorem-9-pages.pdf");

    [Test]
    public Task Merge_Split_Documents()
        => PdfTestHelper.Merge_Split_Documents(_pdfService, _pdfService, "lorem-9-pages.pdf");

    [Test]
    public Task ToImages()
        => PdfTestHelper.ToImages(_pdfService);
}