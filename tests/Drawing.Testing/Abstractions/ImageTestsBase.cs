using Regira.Media.Drawing.Services.Abstractions;

[assembly: Parallelizable(ParallelScope.Fixtures)]

namespace Drawing.Testing.Abstractions;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public abstract class ImageTestsBase(IImageService imageService)
{
    [TestCase("img-1.jpg")]
    [TestCase("img-2.jpg")]
    [TestCase("img-3.jpg")]
    [TestCase("img-4.jpg")]
    public Task To_Png(string filename) => imageService.Test_To_Png(filename);

    [TestCase("img-5.png")]
    [TestCase("img-6.png")]
    public Task To_Jpeg(string filename) => imageService.Test_To_Jpeg(filename);

    [TestCase("img-1.jpg")]
    [TestCase("img-2.jpg")]
    [TestCase("img-3.jpg")]
    [TestCase("img-4.jpg")]
    public Task Resize(string filename) => imageService.Test_Resize(filename);

    [TestCase("img-1.jpg")]
    [TestCase("img-2.jpg")]
    [TestCase("img-3.jpg")]
    [TestCase("img-4.jpg")]
    public Task ResizeFixed(string filename) => imageService.Test_ResizeFixed(filename);

    [Test]
    public Task RotateImage90Right() => imageService.Test_RotateImage90Right();
    [Test]
    public Task RotateImage90Left() => imageService.Test_RotateImage90Left();

    [TestCase("img-1.jpg")]
    [TestCase("img-2.jpg")]
    [TestCase("img-3.jpg")]
    [TestCase("img-4.jpg")]
    public Task CropRect(string filename) => imageService.Test_CropRect(filename);

    [TestCase("img-1.jpg")]
    [TestCase("img-2.jpg")]
    [TestCase("img-3.jpg")]
    [TestCase("img-4.jpg")]
    [TestCase("img-5.png")]
    [TestCase("img-6.png")]
    [TestCase("thumbs-up.jpg")]
    public Task MakeTransparent(string filename) => imageService.Test_MakeTransparent(filename);

    [TestCase("img-1.jpg")]
    [TestCase("img-2.jpg")]
    [TestCase("img-3.jpg")]
    [TestCase("img-4.jpg")]
    [TestCase("img-5.png")]
    [TestCase("img-6.png")]
    [TestCase("thumbs-up.jpg")]
    public Task Test_MakeOpaque(string filename) => imageService.Test_MakeOpaque(filename);
}