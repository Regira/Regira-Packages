using Regira.Dimensions;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Utilities;
using Regira.Utilities;

namespace Regira.Media.Drawing.Services;

public static class PixelParserUtility
{
    /// <summary>
    /// Converts a given dimension value to pixels based on the specified unit, relative value, and DPI.
    /// </summary>
    /// <param name="value">The dimension value to be converted.</param>
    /// <param name="unit">The unit of measurement for the dimension value (e.g., points, inches, millimeters, percent).</param>
    /// <param name="relativeValue">The reference value used for percentage-based calculations.</param>
    /// <param name="dpi">The dots per inch (DPI) to be used for conversion. If not provided, a default DPI value is used.</param>
    /// <returns>The equivalent pixel value as an integer.</returns>
    public static int ToPixels(float value, LengthUnit unit, int relativeValue, int dpi) 
        => DimensionsUtility.GetPixels(value, unit, relativeValue, dpi);

    /// <summary>
    /// Converts a <see cref="Size2D"/> instance to an <see cref="ImageSize"/> based on the specified unit, target size, and DPI.
    /// </summary>
    /// <param name="size">The original size to be converted.</param>
    /// <param name="unit">The unit of measurement for the size (e.g., Points, Inches, Millimeters, Percent).</param>
    /// <param name="targetSize">The target image size to which the conversion is relative.</param>
    /// <param name="dpi">The dots per inch (DPI) value used for conversion. If <c>null</c>, a default DPI is used.</param>
    /// <returns>
    /// An <see cref="ImageSize"/> instance representing the converted size, or <c>null</c> if the conversion fails.
    /// </returns>
    public static ImageSize? ToImageSize(this Size2D size, LengthUnit unit, ImageSize targetSize, int dpi) 
        => DrawImageUtility.CalculateSize(size, unit, targetSize, dpi);

    /// <summary>
    /// Converts a <see cref="Position2D"/> offset to an <see cref="ImageEdgeOffset"/> based on the specified unit, target image size, and DPI.
    /// </summary>
    /// <param name="offset">The offset represented as a <see cref="Position2D"/>.</param>
    /// <param name="unit">The unit of measurement for the offset, such as points, inches, millimeters, or percent.</param>
    /// <param name="targetSize">The target image size as an <see cref="ImageSize"/>.</param>
    /// <param name="dpi">The dots per inch (DPI) value for the conversion. If null, a default DPI value is used.</param>
    /// <returns>An <see cref="ImageEdgeOffset"/> representing the converted offset.</returns>
    public static ImageEdgeOffset ToImageEdgeOffset(this Position2D offset, LengthUnit unit, ImageSize targetSize, int dpi) 
        => DrawImageUtility.CalculateEdgeOffset(offset, unit, targetSize, dpi);

    /// <summary>
    /// Converts a <see cref="Coordinate2D"/> to an <see cref="ImagePoint"/> based on the specified unit, target image size, and DPI.
    /// </summary>
    /// <param name="point">The point to be converted, represented as a <see cref="Coordinate2D"/>.</param>
    /// <param name="unit">The unit of measurement for the conversion, specified as a <see cref="LengthUnit"/>.</param>
    /// <param name="targetSize">The target image size, represented as an <see cref="ImageSize"/>.</param>
    /// <param name="dpi">
    /// The dots per inch (DPI) value used for the conversion. If <c>null</c>, a default DPI value is used.
    /// </param>
    /// <returns>
    /// An <see cref="ImagePoint"/> representing the converted point in the target image.
    /// </returns>
    public static ImagePoint ToImagePoint(this Coordinate2D point, LengthUnit unit, ImageSize targetSize, int dpi) 
        => DrawImageUtility.CalculatePoint(point, unit, targetSize, dpi);
}