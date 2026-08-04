using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;

namespace Regira.Media.Drawing.Services.Abstractions;

/// <summary>
/// Provides methods for creating and drawing images.
/// </summary>
public interface IImageDrawService
{
    /// <summary>
    /// Creates a new blank image with the specified size, background color, and format.
    /// </summary>
    /// <param name="size">The size of the image.</param>
    /// <param name="backgroundColor">The background color (optional).</param>
    /// <param name="format">The image format (optional).</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The created image file.</returns>
    Task<IImageFile> Create(ImageSize size, Color? backgroundColor = null, ImageFormat? format = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Creates an image containing text, using the specified label options.
    /// </summary>
    /// <param name="options">The label image options (optional).</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The created text image file.</returns>
    Task<IImageFile> CreateTextImage(LabelImageOptions? options = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Draws multiple images onto a target image or a new canvas, with optional DPI.
    /// </summary>
    /// <param name="items">The collection of images to add.</param>
    /// <param name="target">The target image file (optional).</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The resulting image file with all images drawn.</returns>
    Task<IImageFile> Draw(IEnumerable<ImageLayer> items, IImageFile? target = null, CancellationToken cancellationToken = default);
}
