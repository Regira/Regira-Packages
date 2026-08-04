using Regira.Media.Drawing.Constants;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Utilities;
using SkiaSharp;

namespace Regira.Drawing.SkiaSharp.Utilities;

public static class SkiaUtility
{
    // https://docs.microsoft.com/en-us/xamarin/xamarin-forms/user-interface/graphics/skiasharp/bitmaps/drawing

    public static SKBitmap CropRectangle(SKBitmap src, SKRect rect)
    {
        var target = new SKBitmap(new SKImageInfo((int)rect.Width, (int)rect.Height, src.ColorType, src.AlphaType));
        using var canvas = new SKCanvas(target);
        canvas.DrawBitmap(src, rect, new SKRect(0, 0, rect.Width, rect.Height));
        return target;
    }

    public static SKBitmap Resize(SKBitmap src, SKSize wantedSize, int quality = 80)
    {
        var targetSize = DrawImageUtility.CalculateSize(new ImageSize(src.Width, src.Height), new ImageSize((int)wantedSize.Width, (int)wantedSize.Height));
        return ResizeFixed(src, new SKSize(targetSize.Width, targetSize.Height), quality);
    }
    public static SKBitmap ResizeFixed(SKBitmap src, SKSize wantedSize, int quality = 80)
    {
        var target = new SKBitmap(new SKImageInfo((int)wantedSize.Width, (int)wantedSize.Height, src.ColorType, src.AlphaType));
        src.ScalePixels(target, ConversionUtility.ToFilterQuality(quality));
        if (quality < 100)
        {
            using var data = target.Encode(SKEncodedImageFormat.Jpeg, quality);
            return SKBitmap.Decode(data);
        }
        return target;
    }
    public static SKBitmap Rotate(SKBitmap src, int degrees, SKColor backgroundColor)
    {
        //degrees = (360 + degrees) % 360;
        var newSize = RotateUtility.CalculateSize(new[] { src.Width, src.Height }, degrees);
        var rotated = new SKBitmap(new SKImageInfo((int)newSize.Width, (int)newSize.Height));
        using var canvas = new SKCanvas(rotated);
        canvas.Clear(backgroundColor);
        canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-src.Width / 2f, -src.Height / 2f);
        canvas.DrawBitmap(src, new SKPoint());
        return rotated;
    }
    public static SKBitmap FlipHorizontal(SKBitmap src)
    {
        var flippedBitmap = new SKBitmap(new SKImageInfo(src.Width, src.Height));
        using var canvas = new SKCanvas(flippedBitmap);
        canvas.Clear();
        canvas.Scale(-1, 1, src.Width / 2f, 0);
        canvas.DrawBitmap(src, new SKPoint());
        return flippedBitmap;
    }
    public static SKBitmap FlipVertical(SKBitmap source)
    {
        var flippedBitmap = new SKBitmap(new SKImageInfo(source.Width, source.Height));
        using var canvas = new SKCanvas(flippedBitmap);
        canvas.Clear();
        canvas.Scale(1, -1, 0, source.Height / 2f);
        canvas.DrawBitmap(source, new SKPoint());
        return flippedBitmap;
    }

    public static SKBitmap ChangeFormat(SKBitmap src, SKEncodedImageFormat targetFormat)
    {
        var info = new SKImageInfo(src.Width, src.Height, src.ColorType, SKAlphaType.Premul);
        var converted = new SKBitmap(info);
        using (var canvas = new SKCanvas(converted))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(src, new SKPoint());
        }
        using var img = SKImage.FromBitmap(converted);
        var data = img.Encode(targetFormat, 100);
        return SKBitmap.Decode(data);
    }

    public static SKBitmap MakeTransparent(SKBitmap src, SKColor color)
    {
        var transparentImage = new SKBitmap(new SKImageInfo(src.Width, src.Height, src.ColorType, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(transparentImage))
        {
            canvas.DrawBitmap(src, 0, 0);
        }

        for (var r = 0; r < transparentImage.Height; r++)
        {
            for (var c = 0; c < transparentImage.Width; c++)
            {
                var imgColor = transparentImage.GetPixel(c, r);
                if (imgColor.Red > color.Red && imgColor.Green > color.Green && imgColor.Blue > color.Blue)
                {
                    transparentImage.SetPixel(c, r, SKColors.Transparent);
                }
            }
        }

        return transparentImage;
    }
    public static bool IsPixelTransparent(SKColor pixel)
    {
        return pixel.Alpha == 0;
    }
    public static SKBitmap ChangeOpacity(SKBitmap img, float opacity)
    {
        if (Math.Abs(opacity - 1) < float.Epsilon)
        {
            return img.Copy();
        }

        var info = new SKImageInfo(img.Width, img.Height);
        var newImg = new SKBitmap(info);

        using var canvas = new SKCanvas(newImg);

        canvas.DrawBitmap(img, 0, 0);

        using var paint = new SKPaint();
        paint.Color = new SKColor(255, 255, 255, (byte)(opacity * 255));
        paint.BlendMode = SKBlendMode.DstIn;

        canvas.DrawRect(SKRect.Create(img.Width, img.Height), paint);

        return newImg;
    }
    public static SKColor GetPixelColor(SKBitmap img, int x, int y)
        => img.GetPixel(x, y);


    public static SKBitmap Create(SKSize size, SKColor backgroundColor)
    {
        var bitmap = new SKBitmap((int)size.Width, (int)size.Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(backgroundColor);
        return bitmap;
    }
    public static SKBitmap CreateTextImage(LabelImageOptions? options = null)
    {
        options ??= new LabelImageOptions();

        var textColor = (options.TextColor ?? LabelImageDefaults.TextColor).ToSkiaColor();
        var backgroundColor = (options.BackgroundColor ?? LabelImageDefaults.BackgroundColor).ToSkiaColor();

        // Create SKFont for measuring
        using var typeface = SKTypeface.FromFamilyName(options.FontName ?? LabelImageDefaults.FontName, SKFontStyle.Normal);
        using var font = new SKFont(typeface, options.FontSize ?? LabelImageDefaults.FontSize);

        // Measure text width using SKFont
        float textWidth = font.MeasureText(options.Text);
        float textHeight = font.Metrics.Descent - font.Metrics.Ascent;

        // Add padding
        var padding = options.Padding ?? LabelImageDefaults.Padding;
        int width = (int)Math.Ceiling(textWidth + padding * 2);
        int height = (int)Math.Ceiling(textHeight + padding * 2);

        var info = new SKImageInfo(width, height);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(backgroundColor);

        // Use SKPaint for styling draw
        using var paint = new SKPaint();
        paint.IsAntialias = true;
        paint.Color = textColor;

        // Baseline-adjusted position
        float x = padding;
        float y = padding - font.Metrics.Ascent;

        canvas.DrawText(options.Text, x, y, font, paint);

        // Step 5: Save to file
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return SKBitmap.Decode(data);
    }
}