using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;

namespace Regira.Media.Drawing.Services.Abstractions;

/// <summary>
/// Provides methods for image transformation (crop, resize, rotate, flip).
/// </summary>
public interface IImageTransformService
{
    /// <summary>
    /// Gets the dimensions (width and height) of the input image.
    /// </summary>
    /// <param name="input">The image file.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The size of the image as a <see cref="ImageSize"/>.</returns>
    Task<ImageSize> GetDimensions(IImageFile input, CancellationToken cancellationToken = default);
    /// <summary>
    /// Resizes the input image to the specified size, with optional quality.
    /// </summary>
    /// <param name="input">The source image file.</param>
    /// <param name="wantedSize">The desired size.</param>
    /// <param name="quality">The quality of the resize operation (default 100).</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The resized image file.</returns>
    Task<IImageFile> Resize(IImageFile input, ImageSize wantedSize, int quality = 100, CancellationToken cancellationToken = default);
    /// <summary>
    /// Resizes the input image to the exact specified size, ignoring aspect ratio, with optional quality.
    /// </summary>
    /// <param name="input">The source image file.</param>
    /// <param name="size">The target size.</param>
    /// <param name="quality">The quality of the resize operation (default 100).</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The resized image file.</returns>
    Task<IImageFile> ResizeFixed(IImageFile input, ImageSize size, int quality = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crops the specified rectangular region from the input image.
    /// </summary>
    /// <param name="input">The source image file to crop.</param>
    /// <param name="rect">
    /// The rectangular region to crop, defined by its top, left, bottom, and right margins.
    /// </param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The cropped image file.</returns>
    Task<IImageFile> CropRectangle(IImageFile input, ImageEdgeOffset rect, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the input image by the specified angle, with an optional background color.
    /// </summary>
    /// <param name="input">The source image file.</param>
    /// <param name="degrees"></param>
    /// <param name="background">The background color to use for empty areas (optional).</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The rotated image file.</returns>
    Task<IImageFile> Rotate(IImageFile input, int degrees, Color? background = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Flips the input image horizontally.
    /// </summary>
    /// <param name="input">The source image file.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The horizontally flipped image file.</returns>
    Task<IImageFile> FlipHorizontal(IImageFile input, CancellationToken cancellationToken = default);
    /// <summary>
    /// Flips the input image vertically.
    /// </summary>
    /// <param name="input">The source image file.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The vertically flipped image file.</returns>
    Task<IImageFile> FlipVertical(IImageFile input, CancellationToken cancellationToken = default);
}
