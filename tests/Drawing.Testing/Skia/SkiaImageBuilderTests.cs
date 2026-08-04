using Drawing.Testing.Extensions;
using Regira.Drawing.SkiaSharp.Services;
using Regira.Media.Drawing.Services.Abstractions;

namespace Drawing.Testing.Skia;

[TestFixture]
public class SkiaImageBuilderTests
{
    readonly IImageService _imgService = new ImageService();

    [SetUp]
    public Task Setup() => _imgService.SetupTests();

    [Test]
    public Task Build_NoTarget() => _imgService.Build_NoTarget();
    [Test]
    public Task Build_WithTargetCanvas() => _imgService.Build_WithTargetCanvas();
    //[Test]
    //public Task Build_WithTargetCanvas_In_Mm() => _imgService.Build_WithTargetCanvas_In_Mm();
    [Test]
    public Task Build_WithTargetImage() => _imgService.Build_WithTargetImage();
    [Test]
    public Task Build_Images() => _imgService.Build_Images();
}