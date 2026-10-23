using Drawing.Testing.Extensions;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services.Abstractions;

namespace Drawing.Testing.Abstractions;

public static class DrawingImageExtensions
{
    public static async Task Test_To_Png(this IImageService service, string filename)
    {
        using var image = await service.ReadImage(filename);
        var pngImg = await service.ChangeFormat(image, ImageFormat.Png);

        await service.SaveImage(pngImg, $"{Path.GetFileNameWithoutExtension(filename)}.png");

        Assert.That(ImageReadBack.IsPng(pngImg), Is.True, "the output starts with the PNG signature");
        AssertSameSize(image, pngImg);
    }
    public static async Task Test_To_Jpeg(this IImageService service, string filename)
    {
        using var image = await service.ReadImage(filename);
        var jpegImg = await service.ChangeFormat(image, ImageFormat.Jpeg);

        await service.SaveImage(jpegImg, $"{Path.GetFileNameWithoutExtension(filename)}.jpg");

        Assert.That(ImageReadBack.IsJpeg(jpegImg), Is.True, "the output starts with the JPEG signature");
        AssertSameSize(image, jpegImg);
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
    /// <summary>At an angle that is not a right angle the canvas is the rotated image's bounding box, to the pixel.</summary>
    public static async Task Test_RotateImage45(this IImageService service)
    {
        using var image = await service.ReadImage("img-1.jpg");
        using var rotated = await service.Rotate(image, 45);
        var side = (image.Size!.Value.Width + image.Size!.Value.Height) * Math.Sqrt(0.5);

        Assert.Multiple(() =>
        {
            Assert.That(rotated.Size!.Value.Width, Is.EqualTo(side).Within(1));
            Assert.That(rotated.Size!.Value.Height, Is.EqualTo(side).Within(1));
        });
        await service.SaveImage(rotated, "img-1-45.jpg");
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

            using var decoded = ImageReadBack.Decode(cropped);
            Assert.That((decoded.Width, decoded.Height),
                Is.EqualTo((image.Size.Value.Width - halfWidth, image.Size.Value.Height - halfHeight)), rectangle.Key);
        }
    }
    /// <summary>Each quadrant of the known image, cropped out, holds that quadrant's pixels.</summary>
    public static async Task Test_CropRect_Keeps_The_Pixels_Of_Each_Quadrant(this IImageService service)
    {
        using var image = ImageReadBack.KnownPixels();
        var rectangles = new Dictionary<string, (ImageEdgeOffset Offset, int X, int Y)>
        {
            ["topleft"] = (new(0, 0, 32, 32), 0, 0),
            ["topright"] = (new(0, 32, 32, 0), 32, 0),
            ["bottomleft"] = (new(32, 0, 0, 32), 0, 32),
            ["bottomright"] = (new(32, 32, 0, 0), 32, 32)
        };

        foreach (var (name, (offset, x, y)) in rectangles)
        {
            using var cropped = await service.CropRectangle(image, offset);
            using var decoded = ImageReadBack.Decode(cropped);
            var expected = ImageReadBack.Quadrant(x, y);
            var actual = decoded.GetPixel(16, 16);
            // drawing through premultiplied alpha rounds a half-transparent pixel's colour by up to one level
            var tolerance = expected.Alpha == 255 ? 0 : 1;
            Assert.Multiple(() =>
            {
                Assert.That((decoded.Width, decoded.Height), Is.EqualTo((32, 32)), name);
                Assert.That(actual.Alpha, Is.EqualTo(expected.Alpha), name);
                Assert.That(new[] { actual.Red, actual.Green, actual.Blue }.Select(c => (int)c),
                    Is.EqualTo(new[] { expected.Red, expected.Green, expected.Blue }.Select(c => (int)c)).Within(tolerance).AsCollection, name);
            });
        }
    }
    public static async Task Test_MakeTransparent(this IImageService service, string filename)
    {
        using var image = await service.ReadImage(filename);

        using var rgbImg = await service.MakeTransparent(image, new Color(200, 200, 200));
        await service.SaveImage(rgbImg, $"{Path.GetFileNameWithoutExtension(filename)}-transparent.png");

        AssertSameSize(image, rgbImg);
    }
    /// <summary>The interface clears "the specified color": the key quadrant only, not a lighter or a darker one.</summary>
    public static async Task Test_MakeTransparent_Clears_The_Key_Colour_Only(this IImageService service)
    {
        using var image = ImageReadBack.KnownPixels();
        var key = ImageReadBack.Key;

        using var transparent = await service.MakeTransparent(image, new Color(key.Red, key.Green, key.Blue));
        using var decoded = ImageReadBack.Decode(transparent);

        Assert.Multiple(() =>
        {
            Assert.That(decoded.GetPixel(16, 16).Alpha, Is.Zero, "the key colour");
            Assert.That(decoded.GetPixel(16, ImageReadBack.NearKeyRow), Is.EqualTo(ImageReadBack.NearKey), "the key colour but for one channel");
            Assert.That(decoded.GetPixel(48, 16), Is.EqualTo(ImageReadBack.Lighter), "a lighter colour");
            Assert.That(decoded.GetPixel(16, 48), Is.EqualTo(ImageReadBack.Blue), "a darker colour");
        });
    }
    public static async Task Test_MakeOpaque(this IImageService service, string filename)
    {
        using var image = await service.ReadImage(filename);

        using var rgbImg = await service.MakeOpaque(image);
        await service.SaveImage(rgbImg, $"{Path.GetFileNameWithoutExtension(filename)}-rgb.png");

        AssertSameSize(image, rgbImg);
    }
    /// <summary>
    /// Every pixel ends opaque; an opaque pixel is unchanged, a half-transparent one keeps its colour, only its alpha
    /// becomes 255, and a fully transparent one turns white — the same rule on every backend.
    /// </summary>
    public static async Task Test_MakeOpaque_Leaves_No_Alpha(this IImageService service)
    {
        using var image = ImageReadBack.KnownPixels();

        using var opaque = await service.MakeOpaque(image);
        using var decoded = ImageReadBack.Decode(opaque);

        var half = ImageReadBack.HalfTransparent;
        var halfMadeOpaque = decoded.GetPixel(48, 48);
        Assert.Multiple(() =>
        {
            Assert.That(decoded.Pixels.Where(pixel => pixel.Alpha != 255), Is.Empty, "every pixel is opaque");
            Assert.That(decoded.GetPixel(16, 16), Is.EqualTo(ImageReadBack.Key));
            Assert.That(decoded.GetPixel(48, 16), Is.EqualTo(ImageReadBack.Lighter));
            Assert.That(decoded.GetPixel(16, 48), Is.EqualTo(ImageReadBack.Blue));
            // a decoder that stores premultiplied alpha rounds a half-transparent colour by up to one level
            Assert.That(new[] { halfMadeOpaque.Red, halfMadeOpaque.Green, halfMadeOpaque.Blue }.Select(c => (int)c),
                Is.EqualTo(new[] { half.Red, half.Green, half.Blue }.Select(c => (int)c)).Within(1).AsCollection,
                "a half-transparent pixel keeps its colour");
            Assert.That(decoded.GetPixel(48, ImageReadBack.TransparentRow), Is.EqualTo(SkiaSharp.SKColors.White), "a fully transparent pixel turns white");
        });
    }

    private static void AssertSameSize(IImageFile source, IImageFile output)
    {
        using var decoded = ImageReadBack.Decode(output);
        Assert.That((decoded.Width, decoded.Height), Is.EqualTo((source.Size!.Value.Width, source.Size.Value.Height)));
    }
}
