using Drawing.Testing.Extensions;
using Regira.Drawing.GDI.Services;
using Regira.Media.Drawing.Services.Abstractions;

namespace Drawing.Testing.GDI;

[TestFixture]
public class GdiImageBuilderTests
{
    readonly IImageService _imgService = new ImageService();

    [SetUp]
    public Task Setup() => _imgService.SetupTests();

    [Test]
    public Task Build_NoTarget() => _imgService.Build_NoTarget();
    [Test]
    public Task Build_WithTargetCanvas() => _imgService.Build_WithTargetCanvas();
    [Test]
    public Task Build_WithTargetImage() => _imgService.Build_WithTargetImage();
    [Test]
    public Task Build_Images() => _imgService.Build_Images();
}