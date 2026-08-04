using Regira.IO.Extensions;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using SkiaSharp;

namespace Regira.Drawing.SkiaSharp.Utilities;

public static class ConversionUtility
{
    // Quality
    public static SKSamplingOptions ToFilterQuality(int quality)
    {
#if NETCOREAPP3_0_OR_GREATER
        quality = Math.Clamp(quality, 0, 100);
#endif

        var skiaQuality = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        if (quality < 100 * 0.3333)
        {
            skiaQuality = new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
        }
        else if (quality > 100 * 0.6667)
        {
            skiaQuality = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        }

        return skiaQuality;
    }

    // Size
    public static SKSize ToSkiaSize(this ImageSize size)
        => new(size.Width, size.Height);

    // Format
    public static SKEncodedImageFormat GetFormat(Stream stream)
    {
        var codec = SKCodec.Create(stream);
        stream.Position = 0;
        return codec.EncodedFormat;
    }
    public static SKEncodedImageFormat ToSkiaFormat(this ImageFormat format)
        => Enum.Parse<SKEncodedImageFormat>(format.ToString());
    public static ImageFormat ToImageFormat(this SKEncodedImageFormat format)
        => Enum.Parse<ImageFormat>(format.ToString());

    // Color
    public static Color ToColor(this SKColor color)
        => new(color.Red, color.Green, color.Blue, color.Alpha);
    public static SKColor ToSkiaColor(this Color color)
        => new(color.Red, color.Green, color.Blue, color.Alpha);

    // File
    public static IImageFile ToImageFile(this SKImage src, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        // FromImage reads the raster pixels directly; EncodedData is null for images that were
        // built from pixels rather than decoded from an encoded byte stream (e.g. Spire's SaveToImages).
        using var skBitmap = SKBitmap.FromImage(src);
        return ToImageFile(skBitmap, format);
    }
    public static IImageFile ToImageFile(this SKBitmap src, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var img = SKImage.FromBitmap(src);
        using var data = img.Encode(format, 100);
        return new ImageFile
        {
            Bytes = data.ToArray(),
            Size = new(src.Width, src.Height),
            ContentType = $"image/{format.ToString().ToLower()}",
            Format = ToImageFormat(format),
            Length = data.Size
        };
    }
    public static SKBitmap ToBitmap(this IImageFile file)
        => !file.HasContent()
        ? throw new NullReferenceException("Cannot convert an Image file without content")
        : file.HasStream()
            ? SKBitmap.Decode(file.Stream)
            : SKBitmap.Decode(file.GetBytes());
}