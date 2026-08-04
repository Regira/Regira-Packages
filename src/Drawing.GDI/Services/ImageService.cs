using Regira.Drawing.GDI.Utilities;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Media.Drawing.Constants;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services.Abstractions;
using Regira.Media.Drawing.Utilities;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Color = Regira.Media.Drawing.Models.Color;
using ImageFormat = Regira.Media.Drawing.Enums.ImageFormat;

namespace Regira.Drawing.GDI.Services;

/// <summary>
/// Provides a set of services for manipulating and processing images, including parsing, resizing, cropping, 
/// format conversion, and other image-related operations. This class implements the <see cref="IImageService"/> interface.
/// </summary>
/// <remarks>
/// This service supports various image formats defined in the <see cref="ImageFormat"/> enumeration and 
/// works with image files represented by the <see cref="IImageFile"/> interface. It also provides utility 
/// methods for creating, drawing, and modifying images.
/// 
/// All operations are exposed as <c>Task</c>-returning methods to enable async usage and cancellation, but the underlying
/// GDI+ APIs are synchronous; the work is performed inline and wrapped via <see cref="Task.FromResult{T}"/>. Callers
/// that need to offload work can do so with <see cref="Task.Run(System.Action)"/>.
/// </remarks>
public class ImageService : IImageService
{
    /// <inheritdoc/>
    public Task<IImageFile?> Parse(Stream? stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (stream == null)
        {
            return Task.FromResult<IImageFile?>(null);
        }

        using var img = Image.FromStream(stream);
        return Task.FromResult<IImageFile?>(img.ToImageFile(img.RawFormat));
    }
    /// <inheritdoc/>
    public Task<IImageFile?> Parse(byte[]? bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (bytes == null)
        {
            return Task.FromResult<IImageFile?>(null);
        }

        using var stream = new MemoryStream(bytes);
        return Parse(stream, cancellationToken);
    }
    /// <inheritdoc/>
    public Task<IImageFile?> Parse(byte[] rawBytes, ImageSize size, ImageFormat? format = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);

        var bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);
        var pNative = bmpData.Scan0;

        Marshal.Copy(rawBytes, 0, pNative, rawBytes.Length);
        bmp.UnlockBits(bmpData);
        return Task.FromResult<IImageFile?>(bmp.ToImageFile((format ?? ImageFormat.Jpeg).ToGdiImageFormat()));
    }
    /// <inheritdoc/>
    public Task<IImageFile?> Parse(IMemoryFile file, CancellationToken cancellationToken = default) =>
        file.HasStream() ? Parse(file.Stream, cancellationToken) : Parse(file.GetBytes(), cancellationToken);

    /// <inheritdoc/>
    public Task<ImageFormat> GetFormat(IImageFile input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = input.ToBitmap();
        return Task.FromResult(img.RawFormat.ToImageFormat());
    }
    /// <inheritdoc/>
    public Task<IImageFile> ChangeFormat(IImageFile input, ImageFormat targetFormat, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = input.ToBitmap();
        using var convertedImg = GdiUtility.ChangeFormat(img, targetFormat.ToGdiImageFormat());
        return Task.FromResult<IImageFile>(convertedImg.ToImageFile(targetFormat.ToGdiImageFormat()));
    }

    /// <inheritdoc/>
    public Task<IImageFile> CropRectangle(IImageFile input, ImageEdgeOffset rect, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = input.ToBitmap();
        var (coordinate, size) = DrawImageUtility.ToPointSize(rect, new ImageSize(img.Width, img.Height));
        var gdiRectangle = new Rectangle(coordinate.X, coordinate.Y, size.Width, size.Height);
        using var cropped = GdiUtility.CropRectangle(img, gdiRectangle);
        return Task.FromResult<IImageFile>(cropped.ToImageFile(img.RawFormat));
    }

    /// <inheritdoc/>
    public Task<ImageSize> GetDimensions(IImageFile input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = input.ToBitmap();
        return Task.FromResult(new ImageSize(img.Width, img.Height));
    }
    /// <inheritdoc/>
    public Task<IImageFile> Resize(IImageFile input, ImageSize wantedSize, int quality = 100, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = input.ToBitmap();
        using var resized = GdiUtility.Resize(img, new Size(wantedSize.Width, wantedSize.Height));
        return Task.FromResult<IImageFile>(resized.ToImageFile(img.RawFormat));
    }
    /// <inheritdoc/>
    public Task<IImageFile> ResizeFixed(IImageFile input, ImageSize size, int quality = 100, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = input.ToBitmap();
        using var resized = GdiUtility.ResizeFixed(img, new Size(size.Width, size.Height));
        return Task.FromResult<IImageFile>(resized.ToImageFile(img.RawFormat));
    }

    /// <inheritdoc/>
    public Task<IImageFile> Rotate(IImageFile input, int degrees, Color? background = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = input.ToBitmap();
        using var rotated = GdiUtility.Rotate(img, degrees, background?.ToGdiColor());
        return Task.FromResult<IImageFile>(rotated.ToImageFile(img.RawFormat));
    }
    /// <inheritdoc/>
    public Task<IImageFile> FlipHorizontal(IImageFile input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = input.ToBitmap();
        using var flipped = GdiUtility.FlipHorizontal(img);
        return Task.FromResult<IImageFile>(flipped.ToImageFile(img.RawFormat));
    }
    /// <inheritdoc/>
    public Task<IImageFile> FlipVertical(IImageFile input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = input.ToBitmap();
        using var flipped = GdiUtility.FlipVertical(img);
        return Task.FromResult<IImageFile>(flipped.ToImageFile(img.RawFormat));
    }

    /// <inheritdoc/>
    public Task<Color> GetPixelColor(IImageFile input, int x, int y, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = input.ToBitmap();
        return Task.FromResult(GdiUtility.GetPixelColor(img, x, y).ToColor());
    }
    /// <inheritdoc/>
    public Task<IImageFile> MakeTransparent(IImageFile input, Color? color = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        color ??= new Color(245, 245, 245);
        using var img = input.ToBitmap();
        using var target = GdiUtility.MakeTransparent(img, color.Value.ToGdiColor());
        return Task.FromResult<IImageFile>(target.ToImageFile(System.Drawing.Imaging.ImageFormat.Png));
    }
    /// <inheritdoc/>
    public Task<IImageFile> MakeOpaque(IImageFile input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = input.ToBitmap();
        using var target = GdiUtility.ChangeOpacity(img, 1);
        return Task.FromResult<IImageFile>(target.ToImageFile(img.RawFormat));
    }

    /// <inheritdoc/>
    public Task<IImageFile> Create(ImageSize size, Color? backgroundColor = null, ImageFormat? format = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = GdiUtility.Create(size.ToGdiSize(), (backgroundColor ?? ImageDefaults.BackgroundColor).ToGdiColor(), (format ?? ImageDefaults.Format).ToGdiImageFormat());
        return Task.FromResult<IImageFile>(img.ToImageFile(img.RawFormat));
    }
    /// <inheritdoc/>
    public Task<IImageFile> CreateTextImage(LabelImageOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = GdiUtility.CreateTextImage(options);
        return Task.FromResult<IImageFile>(img.ToImageFile(img.RawFormat));
    }
    /// <inheritdoc/>
    public Task<IImageFile> Draw(IEnumerable<ImageLayer> imageLayers, IImageFile? target = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var imagesCollection = imageLayers.ToArray();
        using var targetImage = target?.ToBitmap() ?? DrawUtility.CreateSizedCanvas(imagesCollection);
        DrawUtility.Draw(imagesCollection, targetImage);
        return Task.FromResult<IImageFile>(targetImage.ToImageFile(targetImage.RawFormat));
    }
}
