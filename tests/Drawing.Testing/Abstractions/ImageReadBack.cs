using Regira.IO.Extensions;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Utilities;
using SkiaSharp;

namespace Drawing.Testing.Abstractions;

/// <summary>
/// Reads an output back through SkiaSharp's own codec rather than through the service under test: the format from the
/// file's leading bytes, the pixels unpremultiplied, so an alpha that should be 0 or 255 is read exactly.
/// </summary>
internal static class ImageReadBack
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    public static readonly SKColor Key = new(200, 200, 200, 255);
    public static readonly SKColor Lighter = new(240, 240, 240, 255);
    public static readonly SKColor Blue = new(50, 100, 150, 255);
    public static readonly SKColor HalfTransparent = new(10, 20, 30, 128);
    // colour channels that are not white, so a white pixel after MakeOpaque comes from the rule, not from the input
    public static readonly SKColor FullyTransparent = new(10, 20, 30, 0);
    public const int TransparentRow = 63;

    public static bool IsPng(IImageFile file) => file.GetBytes()!.Take(PngSignature.Length).SequenceEqual(PngSignature);
    public static bool IsJpeg(IImageFile file) => file.GetBytes()!.Take(JpegSignature.Length).SequenceEqual(JpegSignature);

    public static SKBitmap Decode(IImageFile file)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(file.GetBytes()));
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bitmap = new SKBitmap(info);
        Assert.That(codec.GetPixels(info, bitmap.GetPixels()), Is.EqualTo(SKCodecResult.Success));
        return bitmap;
    }

    /// <summary>
    /// A 64×64 PNG of four 32×32 quadrants: <see cref="Key"/> top left, <see cref="Lighter"/> top right,
    /// <see cref="Blue"/> bottom left and <see cref="HalfTransparent"/> bottom right, whose last row
    /// (<see cref="TransparentRow"/>) is <see cref="FullyTransparent"/>.
    /// </summary>
    public static IImageFile KnownPixels()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(64, 64, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                bitmap.SetPixel(x, y, x >= 32 && y == TransparentRow ? FullyTransparent : Quadrant(x, y));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var file = png.ToArray().ToBinaryFile().ToImageFile();
        file.Size = new ImageSize(64, 64);
        return file;
    }

    public static SKColor Quadrant(int x, int y) => (x < 32, y < 32) switch
    {
        (true, true) => Key,
        (false, true) => Lighter,
        (true, false) => Blue,
        _ => HalfTransparent
    };
}
