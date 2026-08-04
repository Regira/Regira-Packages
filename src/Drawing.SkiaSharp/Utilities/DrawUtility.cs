using Regira.Media.Drawing.Constants;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Utilities;
using SkiaSharp;

namespace Regira.Drawing.SkiaSharp.Utilities;

public static class DrawUtility
{
    public static SKBitmap Draw(IEnumerable<ImageLayer> imageLayers, SKBitmap? target = null)
    {
        var images = imageLayers.ToList();
        target ??= CreateSizedCanvas(images);

        if (!images.Any())
        {
            return target;
        }

        var canvas = new SKCanvas(target);
        foreach (var img in images)
        {
            AddImage(img, canvas, new ImageSize(target.Width, target.Height));
        }

        canvas.Flush();
        return target;
    }

    public static SKBitmap CreateSizedCanvas(IEnumerable<ImageLayer> imageLayers)
    {
        var images = imageLayers.ToList();
        var size = new SKSize(
            images.Max(x => x.Options?.Size?.Width ?? (x.Source.Size?.Width ?? 0)),
            images.Max(x => x.Options?.Size?.Height ?? (x.Source.Size?.Height ?? 0))
        );
        return SkiaUtility.Create(size, ImageDefaults.BackgroundColor.ToSkiaColor());
    }

    public static void AddImage(ImageLayer imageLayer, SKCanvas canvas, ImageSize targetSize)
    {
        var img = imageLayer.Source;
        var options = imageLayer.Options ?? new ImageLayerOptions();

        var source = img.ToBitmap();

        // Change opacity
        using var opacityImage = SkiaUtility.ChangeOpacity(source, options.Opacity);

        // Resize if needed
        using var resizedImage = options.Size is { Width: > 0, Height: > 0 }
            ? SkiaUtility.ResizeFixed(opacityImage, options.Size.Value.ToSkiaSize(), 100)
            : options.Size?.Width > 0 || options.Size?.Height > 0
                ? SkiaUtility.Resize(opacityImage, options.Size.Value.ToSkiaSize(), 100)
                : opacityImage;

        // Rotate if needed
        using var rotatedImage = Math.Abs(options.Rotation) > float.Epsilon
            ? SkiaUtility.Rotate(resizedImage, options.Rotation, SKColor.Empty)
            : resizedImage;

        // Position
        var coordinate = DrawImageUtility.GetPoint(options, targetSize, new ImageSize(rotatedImage.Width, rotatedImage.Height));

        canvas.DrawBitmap(rotatedImage, coordinate.X, coordinate.Y);
    }
}
