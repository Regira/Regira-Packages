using Regira.Drawing.SkiaSharp.Utilities;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Media.Drawing.Constants;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services.Abstractions;
using Regira.Media.Drawing.Utilities;
using SkiaSharp;

namespace Regira.Drawing.SkiaSharp.Services;

/// <summary>
/// Provides a set of methods for handling image processing tasks, such as parsing, resizing, cropping, rotating, 
/// flipping, and changing formats. This class also supports creating new images, drawing on images, and retrieving 
/// image properties like dimensions and pixel colors.
/// </summary>
/// <remarks>
/// This service is built on top of SkiaSharp and integrates with various abstractions and utilities for image manipulation.
/// It supports multiple image formats and provides functionality for both memory-based and stream-based image operations.
/// 
/// All operations are exposed as <c>Task</c>-returning methods to enable async usage and cancellation, but the underlying
/// SkiaSharp APIs are synchronous; the work is performed inline and wrapped via <see cref="Task.FromResult{T}"/>. Callers
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

        using var bitmap = SKBitmap.Decode(stream);
        if (bitmap == null)
        {
            return Task.FromResult<IImageFile?>(null);
        }

        var img = new ImageFile
        {
            Stream = stream,
            Size = new[] { bitmap.Width, bitmap.Height }
        };
        return Task.FromResult<IImageFile?>(img);
    }
    /// <inheritdoc/>
    public Task<IImageFile?> Parse(byte[]? bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (bytes == null)
        {
            return Task.FromResult<IImageFile?>(null);
        }

        using var ms = new MemoryStream(bytes);
        using var bitmap = SKBitmap.Decode(ms);
        if (bitmap == null)
        {
            return Task.FromResult<IImageFile?>(null);
        }

        var img = new ImageFile
        {
            Bytes = bytes,
            Size = new[] { bitmap.Width, bitmap.Height }
        };
        return Task.FromResult<IImageFile?>(img);
    }
    /// <inheritdoc/>
    public Task<IImageFile?> Parse(byte[] rawBytes, ImageSize size, ImageFormat? format = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        format ??= ImageFormat.Jpeg;

        using var skImg = SKImage.FromPixels(
            new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul),
            SKData.CreateCopy(rawBytes)
        );

        using var skBitmap = SKBitmap.FromImage(skImg);
        if (skBitmap == null)
        {
            return Task.FromResult<IImageFile?>(null);
        }

        using var img = SkiaUtility.ChangeFormat(skBitmap, format.Value.ToSkiaFormat());
        return Task.FromResult<IImageFile?>(img.ToImageFile(format.Value.ToSkiaFormat()));
    }
    /// <inheritdoc/>
    public Task<IImageFile?> Parse(IMemoryFile file, CancellationToken cancellationToken = default) =>
        file.HasStream() ? Parse(file.Stream, cancellationToken) : Parse(file.GetBytes(), cancellationToken);

    /// <inheritdoc/>
    public Task<ImageFormat> GetFormat(IImageFile input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetFormatCore(input));
    }

    private static ImageFormat GetFormatCore(IImageFile input)
    {
        if (input.Stream != null && input.Stream != Stream.Null)
        {
            return ConversionUtility.GetFormat(input.Stream).ToImageFormat();
        }

        var bytes = input.GetBytes();
        if (bytes == null)
        {
            throw new Exception("Could not get contents of image");
        }

        using var ms = new MemoryStream(bytes);
        return ConversionUtility.GetFormat(ms).ToImageFormat();
    }

    /// <inheritdoc/>
    public Task<IImageFile> ChangeFormat(IImageFile input, ImageFormat targetFormat, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var skiaFormat = targetFormat.ToSkiaFormat();
        using var convertedBitmap = SkiaUtility.ChangeFormat(input.ToBitmap(), skiaFormat);
        return Task.FromResult<IImageFile>(convertedBitmap.ToImageFile(skiaFormat));
    }

    /// <inheritdoc/>
    public Task<IImageFile> CropRectangle(IImageFile input, ImageEdgeOffset rect, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var format = GetFormatCore(input);
        using var img = input.ToBitmap();
        var (topLeft, bottomRight) = DrawImageUtility.ToPoints(rect, new ImageSize(img.Width, img.Height));
        var skRect = new SKRect(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        using var croppedBitmap = SkiaUtility.CropRectangle(img, skRect);
        return Task.FromResult<IImageFile>(croppedBitmap.ToImageFile(format.ToSkiaFormat()));
    }

    /// <inheritdoc/>
    public Task<ImageSize> GetDimensions(IImageFile input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = input.ToBitmap();
        return Task.FromResult(new ImageSize(img.Width, img.Height));
    }
    /// <inheritdoc/>
    public Task<IImageFile> Resize(IImageFile input, ImageSize wantedSize, int quality = 80, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var format = GetFormatCore(input);
        using var sourceBitmap = input.ToBitmap();
        using var scaledBitmap = SkiaUtility.Resize(sourceBitmap, new SKSize(wantedSize.Width, wantedSize.Height), quality);
        return Task.FromResult<IImageFile>(scaledBitmap.ToImageFile(format.ToSkiaFormat()));
    }
    /// <inheritdoc/>
    public Task<IImageFile> ResizeFixed(IImageFile input, ImageSize size, int quality = 80, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var format = GetFormatCore(input);
        using var sourceBitmap = input.ToBitmap();
        using var scaledBitmap = SkiaUtility.ResizeFixed(sourceBitmap, new SKSize(size.Width, size.Height), quality);
        return Task.FromResult<IImageFile>(scaledBitmap.ToImageFile(format.ToSkiaFormat()));
    }

    /// <inheritdoc/>
    public Task<IImageFile> Rotate(IImageFile input, int degrees, Color? background = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var format = GetFormatCore(input);
        using var sourceBitmap = input.ToBitmap();
        using var rotatedBitmap = SkiaUtility.Rotate(sourceBitmap, degrees, (background ?? Color.Transparent).ToSkiaColor());
        return Task.FromResult<IImageFile>(rotatedBitmap.ToImageFile(format.ToSkiaFormat()));
    }
    /// <inheritdoc/>
    public Task<IImageFile> FlipHorizontal(IImageFile input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var format = GetFormatCore(input);
        using var sourceBitmap = input.ToBitmap();
        using var flippedBitmap = SkiaUtility.FlipHorizontal(sourceBitmap);
        return Task.FromResult<IImageFile>(flippedBitmap.ToImageFile(format.ToSkiaFormat()));
    }
    /// <inheritdoc/>
    public Task<IImageFile> FlipVertical(IImageFile input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var format = GetFormatCore(input);
        using var sourceBitmap = input.ToBitmap();
        using var flippedBitmap = SkiaUtility.FlipVertical(sourceBitmap);
        return Task.FromResult<IImageFile>(flippedBitmap.ToImageFile(format.ToSkiaFormat()));
    }

    /// <inheritdoc/>
    public Task<Color> GetPixelColor(IImageFile input, int x, int y, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var image = input.ToBitmap();
        return Task.FromResult(SkiaUtility.GetPixelColor(image, x, y).ToColor());
    }
    /// <inheritdoc/>
    public Task<IImageFile> MakeTransparent(IImageFile input, Color? color = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        color ??= new Color(245, 245, 245);
        using var sourceBitmap = input.ToBitmap();
        var transparentBitmap = SkiaUtility.MakeTransparent(sourceBitmap, color.Value.ToSkiaColor());
        // return as PNG image
        return Task.FromResult<IImageFile>(transparentBitmap.ToImageFile());
    }
    /// <inheritdoc/>
    public Task<IImageFile> MakeOpaque(IImageFile input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var inputBitmap = input.ToBitmap();

        var outputBitmap = new SKBitmap(inputBitmap.Width, inputBitmap.Height);
        using (var canvas = new SKCanvas(outputBitmap))
        {
            canvas.Clear(SKColors.White);
        }

        for (int x = 0; x < inputBitmap.Width; x++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int y = 0; y < inputBitmap.Height; y++)
            {
                var pixelColor = inputBitmap.GetPixel(x, y);
                // Create a new color without the alpha value
                var newColor = SkiaUtility.IsPixelTransparent(pixelColor)
                    ? SKColors.White
                    : new SKColor(pixelColor.Red, pixelColor.Green, pixelColor.Blue, 255);
                outputBitmap.SetPixel(x, y, newColor);
            }
        }

        return Task.FromResult<IImageFile>(outputBitmap.ToImageFile());
    }

    /// <inheritdoc/>
    public Task<IImageFile> Create(ImageSize size, Color? backgroundColor = null, ImageFormat? format = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = SkiaUtility.Create(size.ToSkiaSize(), (backgroundColor ?? ImageDefaults.BackgroundColor).ToSkiaColor());
        return Task.FromResult<IImageFile>(img.ToImageFile((format ?? ImageDefaults.Format).ToSkiaFormat()));
    }
    /// <inheritdoc/>
    public Task<IImageFile> CreateTextImage(LabelImageOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var img = SkiaUtility.CreateTextImage(options);
        return Task.FromResult<IImageFile>(img.ToImageFile());
    }
    /// <inheritdoc/>
    public Task<IImageFile> Draw(IEnumerable<ImageLayer> imageLayers, IImageFile? target = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var imagesCollection = imageLayers.ToArray();
        using var targetImage = target?.ToBitmap() ?? DrawUtility.CreateSizedCanvas(imagesCollection);
        DrawUtility.Draw(imagesCollection, targetImage);
        return Task.FromResult<IImageFile>(targetImage.ToImageFile());
    }
}
