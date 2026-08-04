using Drawing.Testing.Extensions;
using Regira.Drawing.SkiaSharp.Services;
using Regira.Media.Drawing.Services.Abstractions;

namespace Drawing.Testing.Skia;

[TestFixture]
public class SkiaDrawPositionTests
{
    readonly IImageService _imgService = new ImageService();

    [SetUp]
    public Task Setup() => _imgService.SetupTests();

    [Test]
    public Task AddImage_No_Params() => _imgService.AddImage_No_Params();

    // Positioned
    [Test]
    public Task AddImage_Top_Left() => _imgService.AddImage_Top_Left();
    [Test]
    public Task AddImage_Bottom_Left() => _imgService.AddImage_Bottom_Left();
    [Test]
    public Task AddImage_Top_Right() => _imgService.AddImage_Top_Right();
    [Test]
    public Task AddImage_Bottom_Right() => _imgService.AddImage_Bottom_Right();

    // Centered
    [Test]
    public Task AddImage_Top_HCenter() => _imgService.AddImage_Top_HCenter();
    [Test]
    public Task AddImage_Bottom_HCenter() => _imgService.AddImage_Bottom_HCenter();
    [Test]
    public Task AddImage_Left_VCenter() => _imgService.AddImage_Left_VCenter();
    [Test]
    public Task AddImage_Right_VCenter() => _imgService.AddImage_Right_VCenter();
    [Test]
    public Task AddImage_Middle() => _imgService.AddImage_Middle();

    // Absolute
    [Test]
    public Task AddImage_Absolute_Top_Left() => _imgService.AddImage_Absolute_Top_Left();
    [Test]
    public Task AddImage_Absolute_Top_Right() => _imgService.AddImage_Absolute_Top_Right();
    [Test]
    public Task AddImage_Absolute_Bottom_Left() => _imgService.AddImage_Absolute_Bottom_Left();
    [Test]
    public Task AddImage_Absolute_Bottom_Right() => _imgService.AddImage_Absolute_Bottom_Right();

    // Margins
    [Test]
    public Task AddImage_Margin10() => _imgService.AddImage_Margin10();
    [Test]
    public Task AddImage_Top_Left_Margin10() => _imgService.AddImage_Top_Left_Margin10();
    [Test]
    public Task AddImage_Bottom_Left_Margin10() => _imgService.AddImage_Bottom_Left_Margin10();
    [Test]
    public Task AddImage_Top_Right_Margin10() => _imgService.AddImage_Top_Right_Margin10();
    [Test]
    public Task AddImage_Bottom_Right_Margin10() => _imgService.AddImage_Bottom_Right_Margin10();
}