using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Utilities;
using Regira.Utilities;
using System.Drawing;

namespace Regira.Drawing.GDI.Utilities;

public static class DrawUtility
{
    public static Image Draw(IEnumerable<ImageLayer> imageLayers, Image? target = null)
    {
        var images = imageLayers.AsList();
        target ??= CreateSizedCanvas(images);

        if (!images.Any())
        {
            return target;
        }

        using var g = GdiUtility.GetGraphics(target);
        foreach (var img in images)
        {
            AddImageLayer(img, g, new ImageSize(target.Width, target.Height));
        }

        return target;
    }

    public static Image CreateSizedCanvas(IEnumerable<ImageLayer> imageLayers)
    {
        var images = imageLayers.AsList();
        var size = new Size(
            images.Max(x => x.Options?.Size?.Width ?? (x.Source.Size?.Width ?? 0)),
            images.Max(x => x.Options?.Size?.Height ?? (x.Source.Size?.Height ?? 0))
        );
        return GdiUtility.Create(size);
    }

    public static void AddImageLayer(ImageLayer imageLayer, Graphics g1, ImageSize targetSize)
    {
        var img = imageLayer.Source;
        var options = imageLayer.Options ?? new ImageLayerOptions();

        var source = img.ToBitmap();

        // Change opacity
        using var opacityImage = GdiUtility.ChangeOpacity(source, options.Opacity);

        // Resize if needed
        using var resizedImage = options.Size is { Width: > 0, Height: > 0 }
            ? GdiUtility.ResizeFixed(opacityImage, options.Size.Value.ToGdiSize(), 100)
            : options.Size?.Width > 0 || options.Size?.Height > 0
                ? GdiUtility.Resize(opacityImage, options.Size.Value.ToGdiSize())
                : opacityImage;

        // Rotate?
        using var rotatedImage = Math.Abs(options.Rotation) > float.Epsilon
            ? GdiUtility.Rotate(resizedImage, options.Rotation, null)
            : resizedImage;

        // Position
        var coordinate = DrawImageUtility.GetPoint(options, targetSize, new ImageSize(rotatedImage.Width, rotatedImage.Height));

        g1.DrawImage(rotatedImage, coordinate.X, coordinate.Y);
    }
}