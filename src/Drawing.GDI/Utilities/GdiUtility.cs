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
    private static readonly KnownImageFormats KnownImageFormats = new();
    private static readonly ImageEncoders Encoders = new();
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
        // https://www.codeproject.com/Articles/3319/Image-Rotation-in-NET
        if (img == null)
        {
            throw new ArgumentNullException(nameof(img));
        }

        GdiColor bkColor = background
                           ?? (img.RawFormat.Equals(ImageFormat.Png)
                               ? GdiColor.Transparent
                               : GdiColor.White);

        var pf = bkColor == GdiColor.Transparent ? PixelFormat.Format32bppArgb : img.PixelFormat;

        var pi2 = (float)(Math.PI / 2.0);

        // Why can't C# allow these to be const, or at least readonly
        // *sigh*  I'm starting to talk like Christian Graus :omg:
        float oldWidth = img.Width;
        float oldHeight = img.Height;

        // Convert degrees to radians
        double theta = degrees * Math.PI / 180.0;
        double locked_theta = theta;

        // Ensure theta is now [0, 2pi)
        while (locked_theta < 0.0)
            locked_theta += 2 * Math.PI;

        #region Explaination of the calculations
        /*
         * The trig involved in calculating the new width and height
         * is fairly simple; the hard part was remembering that when 
         * PI/2 <= theta <= PI and 3PI/2 <= theta < 2PI the width and 
         * height are switched.
         * 
         * When you rotate a rectangle, r, the bounding box surrounding r
         * contains for right-triangles of empty space.  Each of the 
         * triangles hypotenuse's are a known length, either the width or
         * the height of r.  Because we know the length of the hypotenuse
         * and we have a known angle of rotation, we can use the trig
         * function identities to find the length of the other two sides.
         * 
         * sine = opposite/hypotenuse
         * cosine = adjacent/hypotenuse
         * 
         * solving for the unknown we get
         * 
         * opposite = sine * hypotenuse
         * adjacent = cosine * hypotenuse
         * 
         * Another interesting point about these triangles is that there
         * are only two different triangles. The proof for which is easy
         * to see, but its been too long since I've written a proof that
         * I can't explain it well enough to want to publish it.  
         * 
         * Just trust me when I say the triangles formed by the lengths 
         * width are always the same (for a given theta) and the same 
         * goes for the height of r.
         * 
         * Rather than associate the opposite/adjacent sides with the
         * width and height of the original bitmap, I'll associate them
         * based on their position.
         * 
         * adjacent/oppositeTop will refer to the triangles making up the 
         * upper right and lower left corners
         * 
         * adjacent/oppositeBottom will refer to the triangles making up 
         * the upper left and lower right corners
         * 
         * The names are based on the right side corners, because thats 
         * where I did my work on paper (the right side).
         * 
         * Now if you draw this out, you will see that the width of the 
         * bounding box is calculated by adding together adjacentTop and 
         * oppositeBottom while the height is calculate by adding 
         * together adjacentBottom and oppositeTop.
         */
        #endregion

        double adjacentTop, oppositeTop;
        double adjacentBottom, oppositeBottom;

        // We need to calculate the sides of the triangles based
        // on how much rotation is being done to the bitmap.
        //   Refer to the first paragraph in the explaination above for 
        //   reasons why.
        if ((locked_theta >= 0.0 && locked_theta < pi2) ||
            (locked_theta >= Math.PI && locked_theta < (Math.PI + pi2)))
        {
            adjacentTop = Math.Abs(Math.Cos(locked_theta)) * oldWidth;
            oppositeTop = Math.Abs(Math.Sin(locked_theta)) * oldWidth;

            adjacentBottom = Math.Abs(Math.Cos(locked_theta)) * oldHeight;
            oppositeBottom = Math.Abs(Math.Sin(locked_theta)) * oldHeight;
        }
        else
        {
            adjacentTop = Math.Abs(Math.Sin(locked_theta)) * oldHeight;
            oppositeTop = Math.Abs(Math.Cos(locked_theta)) * oldHeight;

            adjacentBottom = Math.Abs(Math.Sin(locked_theta)) * oldWidth;
            oppositeBottom = Math.Abs(Math.Cos(locked_theta)) * oldWidth;
        }

        var newWidth = adjacentTop + oppositeBottom;
        var newHeight = adjacentBottom + oppositeTop;

        // The newWidth/newHeight expressed as ints
        var nWidth = (int)Math.Round(newWidth);
        var nHeight = (int)Math.Round(newHeight);

        var rotatedBmp = new Bitmap(nWidth, nHeight, pf);

        using Graphics g = Graphics.FromImage(rotatedBmp);
        g.Clear(bkColor);
        // This array will be used to pass in the three points that 
        // make up the rotated image
        Point[] points;

        /*
             * The values of opposite/adjacentTop/Bottom are referring to 
             * fixed locations instead of in relation to the
             * rotating image so I need to change which values are used
             * based on the how much the image is rotating.
             * 
             * For each point, one of the coordinates will always be 0, 
             * nWidth, or nHeight.  This because the Bitmap we are drawing on
             * is the bounding box for the rotated bitmap.  If both of the 
             * corrdinates for any of the given points wasn't in the set above
             * then the bitmap we are drawing on WOULDN'T be the bounding box
             * as required.
             */
        if (locked_theta >= 0.0 && locked_theta < pi2)
        {
            points =
            [
                new Point( (int) oppositeBottom, 0 ),
                new Point( nWidth, (int) oppositeTop ),
                new Point( 0, (int) adjacentBottom )
            ];

        }
        else if (locked_theta >= pi2 && locked_theta < Math.PI)
        {
            points =
            [
                new Point( nWidth, (int) oppositeTop ),
                new Point( (int) adjacentTop, nHeight ),
                new Point( (int) oppositeBottom, 0 )
            ];
        }
        else if (locked_theta >= Math.PI && locked_theta < (Math.PI + pi2))
        {
            points =
            [
                new Point( (int) adjacentTop, nHeight ),
                new Point( 0, (int) adjacentBottom ),
                new Point( nWidth, (int) oppositeTop )
            ];
        }
        else
        {
            points =
            [
                new Point( 0, (int) adjacentBottom ),
                new Point( (int) oppositeBottom, 0 ),
                new Point( (int) adjacentTop, nHeight )
            ];
        }

        g.DrawImage(img, points);

        return rotatedBmp;
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
    {
        var mimeType = GetMimeType(format);
        if (Encoders.ContainsKey(mimeType))
        {
            return Encoders[mimeType];
        }

        return null;
    }
    public static string GetMimeType(ImageFormat format)
    {
        if (KnownImageFormats.TryGetValue(format.Guid, out var name))
        {
            return $"image/{name}";
        }

        throw new NotSupportedException("Format not known");
    }
}