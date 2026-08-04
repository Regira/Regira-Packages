using Drawing.Testing.Extensions;
using Regira.Drawing.SkiaSharp.Services;
using Regira.Media.Drawing.Services.Abstractions;

namespace Drawing.Testing.Skia;

[TestFixture]
public class SkiaDrawTextTests
{
    readonly IImageService _imageService = new ImageService();

    [Test]
    public Task Add_Text_No_Params() => _imageService.Add_Text_No_Params();

    [Test]
    public Task Add_Text_With_Options() => _imageService.Add_Text_With_Options();

    [Test]
    public Task Add_Text_With_Margin() => _imageService.Add_Text_With_Margin();
}