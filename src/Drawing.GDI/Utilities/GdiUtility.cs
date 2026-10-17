using Regira.Media.Drawing.Constants;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Utilities;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using GdiColor = System.Drawing.Color;
using ImageFormat = System.Drawing.Imaging.ImageFormat;

namespace Regira.Drawing.GDI.Utilities;

public static class GdiUtility
{
    // enumerated once: the native enumeration intermittently misses an encoder under parallel use
    private static readonly Lazy<ImageCodecInfo[]> Encoders = new(ImageCodecInfo.GetImageEncoders);
    private const int EXIF_ORIENTATION_ID = 0x112;

    public static Image ChangeFormat(Image img, ImageFormat format, int quality = 100)
    {
        if (img.RawFormat.Equals(format))
        {
            return img;
        }

        var codec = GetEncoderInfo(format) ?? throw new Exception($"Could not get encoder info for format {format}");
        var encoderParams = new EncoderParameters { Param = { [0] = new EncoderParameter(Encoder.Quality, quality) } };

        var stream = new MemoryStream();
        using var newImg = new Bitmap(img.Width, img.Height);
        using (var g = GetGraphics(newImg))
        {
            //enforce white background when converting to JPEG
            g.Clear(ImageFormat.Jpeg.Equals(format) ? GdiColor.White : GdiColor.Transparent);

            g.DrawImage(img, 0, 0, img.Width, img.Height);
        }

        newImg.Save(stream, codec, encoderParams);
        return Image.FromStream(stream);
    }

    public static Image CropRectangle(Image img, Rectangle rect)
    {
        var target = new Bitmap(rect.Width, rect.Height);

        using Graphics g = Graphics.FromImage(target);
        g.DrawImage(img, new Rectangle(0, 0, target.Width, target.Height), rect, GraphicsUnit.Pixel);

        return target;
    }

    public static Image Resize(Image img, Size maxSize, int quality = 80)
    {
        var width = maxSize.Width;
        var height = maxSize.Height;

        var size = DrawImageUtility.CalculateSize(new ImageSize(img.Width, img.Height), new ImageSize(width, height));
        return ResizeFixed(img, size.ToGdiSize(), quality);
    }
    public static Image ResizeFixed(Image img, Size size, int quality = 80)
    {
        var result = new Bitmap(img, size);
        // https://stackoverflow.com/questions/24979017/how-to-resize-an-image-with-system-drawing-and-lower-the-image-size
        //var pixelFormat = img.RawFormat.Equals(ImageFormat.Png)
        //    ? (PixelFormat?)PixelFormat.Format32bppArgb
        //    : img.RawFormat.Equals(ImageFormat.Gif)
        //        ? null //PixelFormat.Format8bppIndexed should be the right value for a GIF, but will throw an error with some GIF images so it's not safe to specify.
        //        : PixelFormat.Format24bppRgb;
        //var result = pixelFormat.HasValue
        //    ? new Bitmap(size.Width, size.Height, pixelFormat.Value)
        //    : new Bitmap(size.Width, size.Height);
        //result.SetResolution(img.HorizontalResolution, img.VerticalResolution);

        //using var g = GetGraphics(result);
        //g.Clear(ImageFormat.Jpeg.Equals(img.RawFormat) ? Color.White : Color.Transparent);
        //g.CompositingQuality = CompositingQuality.HighSpeed;
        //g.InterpolationMode = InterpolationMode.Low;
        //g.SmoothingMode = SmoothingMode.HighSpeed;
        //g.DrawImage(img,
        //    new Rectangle(0, 0, result.Width, result.Height),
        //    new Rectangle(0, 0, img.Width, img.Height),
        //    GraphicsUnit.Pixel
        //);

        if (quality < 100)
        {
            var codec = GetEncoderInfo(img.RawFormat) ?? GetEncoderInfo(ImageFormat.Png)!;
            var encoderParams = new EncoderParameters
            {
                Param =
                [
                    new EncoderParameter(Encoder.Quality, quality)
                ]
            };
            var ms = new MemoryStream();
            result.Save(ms, codec, encoderParams);
            result.Dispose();
            return Image.FromStream(ms);
        }

        return result;
    }

    public static Image Rotate(Image img, int degrees, GdiColor? background)
    {
        ArgumentNullException.ThrowIfNull(img);

        var bkColor = background
                      ?? (img.RawFormat.Equals(ImageFormat.Png)
                          ? GdiColor.Transparent
                          : GdiColor.White);
        // GDI+ draws on no indexed format (a GIF, an 8-bit BMP)
        var pf = bkColor == GdiColor.Transparent || (img.PixelFormat & PixelFormat.Indexed) != 0
            ? PixelFormat.Format32bppArgb
            : img.PixelFormat;

        // the same turn as an angle from 0 to 359: GDI+ fails to draw an image turned by -90
        degrees = (degrees % 360 + 360) % 360;
        // the bounding box of the rotated image, as SkiaUtility.Rotate sizes it, the image turned around its centre
        var size = RotateUtility.CalculateSize(new[] { img.Width, img.Height }, degrees);
        var rotated = new Bitmap((int)size.Width, (int)size.Height, pf);
        try
        {
            using var g = Graphics.FromImage(rotated);
            g.Clear(bkColor);
            g.TranslateTransform(rotated.Width / 2f, rotated.Height / 2f);
            g.RotateTransform(degrees);
            g.TranslateTransform(-img.Width / 2f, -img.Height / 2f);
            g.DrawImage(img, 0, 0, img.Width, img.Height);
            return rotated;
        }
        catch
        {
            rotated.Dispose();
            throw;
        }
    }
    public static Image FlipHorizontal(Image image)
    {
        image.RotateFlip(RotateFlipType.RotateNoneFlipX);
        return image;
    }
    public static Image FlipVertical(Image image)
    {
        image.RotateFlip(RotateFlipType.RotateNoneFlipY);
        return image;
    }
    /// <summary>
    /// Reads and removes the orientation from EXIF data (if present -> use Image.FromFile).
    /// Rotates the image accordingly.
    /// </summary>
    /// <param name="image"></param>
    public static void ExifRotate(this Image image)
    {
        // https://www.cyotek.com/blog/handling-the-orientation-exif-tag-in-images-using-csharp
        if (Array.IndexOf(image.PropertyIdList, EXIF_ORIENTATION_ID) > -1)
        {
            // ReSharper disable PossibleNullReferenceException
            var orientation = image.GetPropertyItem(EXIF_ORIENTATION_ID)!.Value![0];
            // ReSharper restore PossibleNullReferenceException

            if (orientation is >= 1 and <= 8)
            {
                switch (orientation)
                {
                    case 2:
                        image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        break;
                    case 3:
                        image.RotateFlip(RotateFlipType.Rotate180FlipNone);
                        break;
                    case 4:
                        image.RotateFlip(RotateFlipType.Rotate180FlipX);
                        break;
                    case 5:
                        image.RotateFlip(RotateFlipType.Rotate90FlipX);
                        break;
                    case 6:
                        image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                        break;
                    case 7:
                        image.RotateFlip(RotateFlipType.Rotate270FlipX);
                        break;
                    case 8:
                        image.RotateFlip(RotateFlipType.Rotate270FlipNone);
                        break;
                }

                image.RemovePropertyItem(EXIF_ORIENTATION_ID);
            }
        }
    }

    public static Image MakeTransparent(Image img, GdiColor color)
    {
        var target = new Bitmap(img);
        target.MakeTransparent(color);
        return target;
    }
    public static Image ChangeOpacity(Image img, float opacity)
    {
        if (Math.Abs(opacity - 1) < float.Epsilon)
        {
            return new Bitmap(img);
        }

        var newImg = new Bitmap(img.Width, img.Height);
        using var g = GetGraphics(newImg);
        g.Clear(GdiColor.Transparent);
        var attributes = new ImageAttributes();
        var matrix = new ColorMatrix { Matrix33 = opacity };
        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        g.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attributes);

        return newImg;
    }
    public static GdiColor GetPixelColor(Image img, int x, int y)
    {
        if (img is Bitmap bitmap)
        {
            return bitmap.GetPixel(x, y);
        }

        using var tempBitmap = new Bitmap(img);
        return tempBitmap.GetPixel(x, y);
    }

    public static Image Create(Size size, GdiColor? backgroundColor = null, ImageFormat? format = null)
    {
        var width = size.Width;
        var height = size.Height;
        format ??= ImageDefaults.Format.ToGdiImageFormat();
        var gdiBgColor = backgroundColor ?? ImageDefaults.BackgroundColor.ToGdiColor();

        var backgroundBrush = new SolidBrush(gdiBgColor);

        var untypedImage = new Bitmap(width, height);
        using (var g = GetGraphics(untypedImage))
        {
            g.Clear(gdiBgColor);
            g.FillRectangle(backgroundBrush, 0, 0, width, height);
        }

        return ChangeFormat(untypedImage, format);
    }
    public static Image CreateTextImage(LabelImageOptions? options = null)
    {
        options ??= new LabelImageOptions();

        var font = new Font(FontFamily.Families.First(f => f.Name.Equals(options.FontName ?? LabelImageDefaults.FontName)), options.FontSize ?? LabelImageDefaults.FontSize);
        var textColor = (options.TextColor ?? LabelImageDefaults.TextColor).ToGdiColor();
        var backgroundColor = (options.BackgroundColor ?? LabelImageDefaults.BackgroundColor).ToGdiColor();

        SizeF textSize;
        using (var dummy = new Bitmap(1, 1))
        {
            using var drawing = GetGraphics(dummy);
            textSize = drawing.MeasureString(options.Text, font);
        }

        var padding = options.Padding ?? LabelImageDefaults.Padding;
        var img = Create(new Size((int)textSize.Width + padding * 2, (int)textSize.Height + padding * 2), backgroundColor);
        using (var drawing = GetGraphics(img))
        {
            drawing.Clear(backgroundColor);
            var brush = new SolidBrush(textColor);
            drawing.DrawString(options.Text, font, brush, padding, padding);
            drawing.Save();
            brush.Dispose();
        }

        return img;
    }



    public static Graphics GetGraphics(Image img, CompositingQuality quality = CompositingQuality.Default,
        InterpolationMode interpolationMode = InterpolationMode.Default, SmoothingMode smoothingMode = SmoothingMode.Default)
    {
        var g = Graphics.FromImage(img);
        g.CompositingQuality = quality;
        g.InterpolationMode = interpolationMode;
        g.SmoothingMode = smoothingMode;
        return g;
    }
    public static ImageCodecInfo? GetEncoderInfo(ImageFormat format)
        => Encoders.Value.FirstOrDefault(codec => codec.FormatID == format.Guid);
    public static string GetMimeType(ImageFormat format)
    {
        // a format GDI+ knows names itself; any other writes its Guid in brackets
        var name = format.ToString();
        return name.StartsWith('[') ? throw new NotSupportedException("Format not known") : $"image/{name}";
    }
}