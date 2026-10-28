using Regira.Media.Drawing.Services.Abstractions;

namespace Drawing.Testing.Abstractions;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public abstract class ImageTestsBase(IImageService imageService)
{
    [TestCase("img-1-480.jpg")]
    [TestCase("img-2-480.jpg")]
    [TestCase("img-3-480.jpg")]
    [TestCase("img-4-480.jpg")]
    public Task To_Png(string filename) => imageService.Test_To_Png(filename);

    [TestCase("img-5.png")]
    [TestCase("img-6.png")]
    public Task To_Jpeg(string filename) => imageService.Test_To_Jpeg(filename);

    [TestCase("img-1-480.jpg")]
    [TestCase("img-2-480.jpg")]
    [TestCase("img-3-480.jpg")]
    [TestCase("img-4-480.jpg")]
    public Task Resize(string filename) => imageService.Test_Resize(filename);

    [TestCase("img-1-480.jpg")]
    [TestCase("img-2-480.jpg")]
    [TestCase("img-3-480.jpg")]
    [TestCase("img-4-480.jpg")]
    public Task ResizeFixed(string filename) => imageService.Test_ResizeFixed(filename);

    [Test]
    public Task RotateImage90Right() => imageService.Test_RotateImage90Right();
    [Test]
    public Task RotateImage90Left() => imageService.Test_RotateImage90Left();
    [Test]
    public Task RotateImage45() => imageService.Test_RotateImage45();

    [TestCase("img-1-480.jpg")]
    [TestCase("img-2-480.jpg")]
    [TestCase("img-3-480.jpg")]
    [TestCase("img-4-480.jpg")]
    public Task CropRect(string filename) => imageService.Test_CropRect(filename);
    [Test]
    public Task CropRect_Keeps_The_Pixels_Of_Each_Quadrant() => imageService.Test_CropRect_Keeps_The_Pixels_Of_Each_Quadrant();

    [TestCase("img-1-480.jpg")]
    [TestCase("img-2-480.jpg")]
    [TestCase("img-3-480.jpg")]
    [TestCase("img-4-480.jpg")]
    [TestCase("img-5.png")]
    [TestCase("img-6.png")]
    [TestCase("thumbs-up.jpg")]
    public Task MakeTransparent(string filename) => imageService.Test_MakeTransparent(filename);
    [Test]
    public virtual Task MakeTransparent_Clears_The_Key_Colour_Only() => imageService.Test_MakeTransparent_Clears_The_Key_Colour_Only();

    [TestCase("img-1-480.jpg")]
    [TestCase("img-2-480.jpg")]
    [TestCase("img-3-480.jpg")]
    [TestCase("img-4-480.jpg")]
    [TestCase("img-5.png")]
    [TestCase("img-6.png")]
    [TestCase("thumbs-up.jpg")]
    public Task Test_MakeOpaque(string filename) => imageService.Test_MakeOpaque(filename);
    [Test]
    public virtual Task MakeOpaque_Leaves_No_Alpha() => imageService.Test_MakeOpaque_Leaves_No_Alpha();
}