using Office.PDF.Testing.Abstractions;
using Regira.IO.Storage.FileSystem;
using Regira.Media.Drawing.Enums;
using Regira.Office.PDF.Models;
using Regira.Office.PDF.Spire;
using Regira.Utilities;

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

    [Test]
    public async Task ToImages_In_Requested_Format()
    {
        var bf = (await FileSystemUtility.Parse(Path.Combine(AssemblyUtility.GetAssemblyDirectory()!, "../../../Assets/Input/sample.pdf")))!;

        var images = await _pdfService.ToImages(bf, new PdfToImagesOptions { Format = ImageFormat.Png });

        Assert.That(images, Has.Count.EqualTo(2));
        Assert.That(images.Select(image => image.Format), Is.All.EqualTo(ImageFormat.Png));
        // PNG signature: 0x89 'P' 'N' 'G'
        Assert.That(images.Select(image => image.Bytes![..4]), Is.All.EqualTo(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
    }
}