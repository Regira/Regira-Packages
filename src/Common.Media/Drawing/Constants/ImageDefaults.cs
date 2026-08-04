using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models;

namespace Regira.Media.Drawing.Constants;

public static class ImageDefaults
{
    public static ImageFormat Format { get; set; } = ImageFormat.Png;
    /// <summary>
    /// Fully opaque alpha value.
    /// </summary>
    public static byte Alpha { get; set; } = byte.MaxValue;
    /// <summary>
    /// Gets or sets the default background color used for images.
    /// </summary>
    /// <remarks>
    /// This property defines the default background color applied to images when no specific color is provided.
    /// The default value is white with full opacity (<c>#FFFFFFFF</c>).
    /// </remarks>
    public static Color BackgroundColor { get; set; } = "#FFFFFFFF";
}