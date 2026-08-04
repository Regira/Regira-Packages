using Drawing.Testing.Extensions;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Services.Abstractions;

namespace Drawing.Testing.Abstractions;

public static class DrawingImageExtensions
{
    public static async Task Test_To_Png(this IImageService service, string filename)
    {
        using var image = await service.ReadImage(filename);
        var pngImg = await service.ChangeFormat(image, ImageFormat.Png);

        await service.SaveImage(pngImg, $"{Path.GetFileNameWithoutExtension(filename)}.png");
    }
    public static async Task Test_To_Jpeg(this IImageService service, string filename)
    {
        using var image = await service.ReadImage(filename);
        var pngImg = await service.ChangeFormat(image, ImageFormat.Jpeg);

        await service.SaveImage(pngImg, $"{Path.GetFileNameWithoutExtension(filename)}.jpg");
    }
    public static async Task Test_Resize(this IImageService service, string filename)
    {
        using var image = await service.ReadImage(filename);
        var original = image.Size;
        var wantedSize = new ImageSize(1024, 1024);
        using var resized = await service.Resize(image, wantedSize, 60);

        await service.SaveImage(resized, $"{Path.GetFileNameWithoutExtension(filename)}-resized.jpg");

        Assert.That(original!.Value.Width, Is.EqualTo(image.Size!.Value.Width));
        Assert.That(original.Value.Height, Is.EqualTo(image.Size!.Value.Height));
        Assert.That(resized.Size!.Value.Width <= wantedSize.Width, Is.True);
        Assert.That(resized.Size!.Value.Height <= wantedSize.Height, Is.True);
    }
    public static async Task Test_ResizeFixed(this IImageService service, string filename)
    {
        using var image = await service.ReadImage(filename);
        var original = image.Size;
        var targetSize = new ImageSize(1024, 1024);
        using var resized = await service.ResizeFixed(image, targetSize);

        await service.SaveImage(resized, $"{Path.GetFileNameWithoutExtension(filename)}-resized-fixed.jpg");

        Assert.That(original!.Value.Width, Is.EqualTo(image.Size!.Value.Width));
        Assert.That(original.Value.Height, Is.EqualTo(image.Size!.Value.Height));
        Assert.That(targetSize.Width, Is.EqualTo(resized.Size!.Value.Width));
        Assert.That(targetSize.Height, Is.EqualTo(resized.Size!.Value.Height));
    }
    public static async Task Test_RotateImage90Right(this IImageService service)
    {
        using var image = await service.ReadImage("img-1.jpg");
        using var rotatedR = await service.Rotate(image, 90);
        Assert.That(rotatedR.Size!.Value.Height, Is.EqualTo(image.Size!.Value.Width));
        Assert.That(rotatedR.Size!.Value.Width, Is.EqualTo(image.Size!.Value.Height));

        //var target90R = Path.Combine(_assetsDir, "Output", "img-1-90right.jpg");
        //rotatedR.Save(target90R);
        await service.SaveImage(rotatedR, "img-1-90right.jpg");
    }
    public static async Task Test_RotateImage90Left(this IImageService service)
    {
        using var image = await service.ReadImage("img-1.jpg");
        using var rotatedL = await service.Rotate(image, -90);
        Assert.That(rotatedL.Size!.Value.Height, Is.EqualTo(image.Size!.Value.Width));
        Assert.That(rotatedL.Size!.Value.Width, Is.EqualTo(image.Size!.Value.Height));

        //var target90L = Path.Combine(_assetsDir, "Output", "img-1-90left.jpg");
        //rotatedL.Save(target90L);
        await service.SaveImage(rotatedL, "img-1-90left.jpg");
    }
    public static async Task Test_CropRect(this IImageService service, string filename)
    {
        using var image = await service.ReadImage(filename);

        var halfWidth = image.Size!.Value.Width / 2;
        var halfHeight = image.Size!.Value.Height / 2;

        var rectangles = new Dictionary<string, ImageEdgeOffset>
        {
            ["topleft"] = new(0, 0, halfHeight, halfWidth),
            ["topright"] = new(0, halfWidth, halfHeight, 0),
            ["bottomleft"] = new(halfHeight, 0, 0, halfWidth),
            ["bottomright"] = new(halfHeight, halfWidth, 0, 0)
        };

        foreach (var rectangle in rectangles)
        {
            using var cropped = await service.CropRectangle(image, rectangle.Value);
            await service.SaveImage(cropped, $"{Path.GetFileNameWithoutExtension(filename)}-{rectangle.Key}{Path.GetExtension(filename)}");
        }
    }
    public static async Task Test_MakeTransparent(this IImageService service, string filename)
    {
        using var image = await service.ReadImage(filename);

        using var rgbImg = await service.MakeTransparent(image, new Color(200, 200, 200));
        await service.SaveImage(rgbImg, $"{Path.GetFileNameWithoutExtension(filename)}-transparent.png");
    }
    public static async Task Test_MakeOpaque(this IImageService service, string filename)
    {
        using var image = await service.ReadImage(filename);

        using var rgbImg = await service.MakeOpaque(image);
        await service.SaveImage(rgbImg, $"{Path.GetFileNameWithoutExtension(filename)}-rgb.png");
    }
}
