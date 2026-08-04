using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models.Abstractions;

namespace Regira.Media.Drawing.Services.Abstractions;

/// <summary>
/// Provides methods for image format detection and conversion.
/// </summary>
public interface IImageFormatService
{
    /// <summary>
    /// Determines the <see cref="ImageFormat"/> of the given <see cref="IImageFile"/>.
    /// </summary>
    /// <param name="input">The image file to inspect.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The detected image format.</returns>
    Task<ImageFormat> GetFormat(IImageFile input, CancellationToken cancellationToken = default);
    /// <summary>
    /// Converts an image to a different format.
    /// </summary>
    /// <param name="input">The source image file.</param>
    /// <param name="targetFormat">The desired image format.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The converted image file.</returns>
    Task<IImageFile> ChangeFormat(IImageFile input, ImageFormat targetFormat, CancellationToken cancellationToken = default);
}
