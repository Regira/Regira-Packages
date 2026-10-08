using Office.PDF.Testing.Abstractions;
using Regira.Drawing.SkiaSharp.Services;
using Regira.Office.Models;
using Regira.Office.PDF.DocNET;
using Regira.Office.PDF.Models;

namespace Office.PDF.Testing;

/// <summary>
/// PDF.DocNET runs every shared scenario in <see cref="PdfTestsBase{TBackend}"/>; below is what it does differently,
/// and its merge by file path.
/// </summary>
[PdfFixture]
public class DocNETTests() : PdfTestsBase<PdfManager>(new PdfManager(new ImageService()), "DocNET")
{
    /// <summary>
    /// DocNET makes each page the size of its image: the image is scaled down to fit the format's page less its
    /// margins, measured in units of the input's DPI, and the page takes that many points.
    /// </summary>
    [TestCase("img-1.jpg,img-2.jpg,img-3.jpg,img-4.jpg", PageSize.A4, PageOrientation.Portrait)]
    [TestCase("lion.png,horse.png", PageSize.A4, PageOrientation.Landscape)]
    [TestCase("img-1.jpg,lion.png", PageSize.A5, PageOrientation.Portrait)]
    public override async Task ImagesToPdf_Puts_Each_Image_On_A_Page_Of_Its_Own(string images, PageSize format, PageOrientation orientation)
    {
        var input = new ImagesInput { Images = ReadImages(images), Format = format, Orientation = orientation };

        using var pdf = await Backend.ImagesToPdf(input);

        var output = await ReadPdf(pdf);
        var maxWidth = input.MaxDimensions.Width - input.Margins.Left - input.Margins.Right;
        var maxHeight = input.MaxDimensions.Height - input.Margins.Top - input.Margins.Bottom;
        Assert.That(output.PageCount, Is.EqualTo(input.Images.Count));
        foreach (var (page, i) in output.Pages.Select((page, i) => (page, i + 1)))
        {
            Assert.That(page.Width, Is.LessThanOrEqualTo(maxWidth + 1), $"page {i} width");
            Assert.That(page.Height, Is.LessThanOrEqualTo(maxHeight + 1), $"page {i} height");
            Assert.That(page.Images, Has.Count.EqualTo(1), $"page {i} images");
            var image = page.Images[0];
            Assert.That(new[] { image.Left, image.Top, image.Right, image.Bottom }, Is.EqualTo(new[] { 0, 0, page.Width, page.Height }).Within(1),
                $"page {i}'s image fills it");
        }
    }

    [Test]
    public async Task Merge_By_Path_Keeps_Each_Pages_Text_In_Order()
    {
        var names = Enumerable.Range(1, 6).Select(i => $"lorem-ipsum{i}.pdf").ToArray();

        using var merged = Backend.Merge(names.Select(InputPath));

        var output = await ReadPdf(merged);
        Assert.That(output.PageTexts, Is.EqualTo(names.SelectMany(name => InputFacts(name).PageTexts)));
    }
}
