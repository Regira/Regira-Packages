using Regira.Dimensions;
using Regira.Media.Drawing.Constants;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services.Abstractions;
using Regira.Utilities;

namespace Regira.Media.Drawing.Utilities;

public static class DrawImageUtility
{
    /// <summary>
    /// Creates an <see cref="IImageFile"/> instance using the first <see cref="IImageCreator"/> 
    /// in the provided <paramref name="services"/> collection that can handle the specified <paramref name="input"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the input object used to create the <see cref="IImageFile"/>. Must be a reference type.
    /// </typeparam>
    /// <param name="services">
    /// A collection of <see cref="IImageCreator"/> instances used to create the <see cref="IImageFile"/>.
    /// </param>
    /// <param name="input">
    /// The input object of type <typeparamref name="T"/> used to create the <see cref="IImageFile"/>.
    /// </param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// An <see cref="IImageFile"/> instance if a suitable <see cref="IImageCreator"/> is found and creation succeeds; otherwise, <c>null</c>.
    /// </returns>
    public static Task<IImageFile?> Create<T>(this IEnumerable<IImageCreator> services, T input, CancellationToken cancellationToken = default)
        where T : class
    {
        var creator = services.FirstOrDefault(s => s.CanCreate(input));
        return creator is null
            ? Task.FromResult<IImageFile?>(null)
            : creator.Create(input, cancellationToken);
    }
    /// <summary>
    /// Retrieves an <see cref="IImageFile"/> instance from the provided <paramref name="item"/> or creates one using the available <paramref name="services"/>.
    /// </summary>
    /// <param name="services">
    /// A collection of <see cref="IImageCreator"/> instances used to create an <see cref="IImageFile"/> if it cannot be directly retrieved from the <paramref name="item"/>.
    /// </param>
    /// <param name="item">
    /// The <see cref="IImageLayer"/> instance containing the source object to retrieve or create the <see cref="IImageFile"/>.
    /// </param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// An <see cref="IImageFile"/> instance if successfully retrieved or created; otherwise, <c>null</c>.
    /// </returns>
    public static Task<IImageFile?> GetImageFile(this IEnumerable<IImageCreator> services, IImageLayer item, CancellationToken cancellationToken = default)
        => item.Source is IImageFile imageFile
            ? Task.FromResult<IImageFile?>(imageFile)
            : services.Create(item.Source, cancellationToken);

    /// <summary>
    /// Calculates the coordinates for positioning an image within a target area based on the provided options, target size, image size, and DPI.
    /// </summary>
    /// <param name="options">
    /// The options specifying how the image should be positioned, including margins, alignment, and dimension units.
    /// </param>
    /// <param name="targetSize">
    /// The size of the target area where the image will be placed.
    /// </param>
    /// <param name="imageSize">
    /// The size of the image to be positioned.
    /// </param>
    /// <returns>
    /// A <see cref="ImagePoint"/> representing the calculated X and Y coordinates for the image's imgOffset.
    /// </returns>
    public static ImagePoint GetPoint(ImageLayerOptions options, ImageSize targetSize, ImageSize imageSize)
    {
        var inputPosition = options.Offset ?? new ImageEdgeOffset();
        var imgLeft = inputPosition.Left;
        var imgRight = inputPosition.Right;
        var imgTop = inputPosition.Top;
        var imgBottom = inputPosition.Bottom;
        var imgMargin = options.Margin;

        var left = 0;
        if (options.Position.HasFlag(ImagePosition.HCenter))
        {
            left = (int)(targetSize.Width / 2f - imageSize.Width / 2f);
        }
        else if (options.Position.HasFlag(ImagePosition.Right) || (options.Position == ImagePosition.Absolute && inputPosition.Right.HasValue))
        {
            left = targetSize.Width - imageSize.Width - imgMargin;
        }
        else
        {
            left += imgMargin;
        }

        var top = 0;
        if (options.Position.HasFlag(ImagePosition.VCenter))
        {
            top = (int)(targetSize.Height / 2f - imageSize.Height / 2f);
        }
        else if (options.Position.HasFlag(ImagePosition.Bottom) || (options.Position == ImagePosition.Absolute && inputPosition.Bottom.HasValue))
        {
            top = targetSize.Height - imageSize.Height - imgMargin;
        }
        else
        {
            top += imgMargin;
        }

        left += (imgLeft ?? 0) - (imgRight ?? 0);
        top += (imgTop ?? 0) - (imgBottom ?? 0);

        return new[] { left, top };
    }


    /// <summary>
    /// Calculates the size of a rectangular area defined by two points.
    /// </summary>
    /// <param name="topLeft">
    /// The top-left point of the rectangle.
    /// </param>
    /// <param name="bottomRight">
    /// The bottom-right point of the rectangle.
    /// </param>
    /// <returns>
    /// A <see cref="ImageSize"/> representing the width and height of the rectangle.
    /// </returns>
    public static ImageSize CalculateSize(ImagePoint topLeft, ImagePoint bottomRight)
        => new(bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    /// <summary>
    /// Calculates the scaled size of a source dimension to fit within a target dimension while maintaining the aspect ratio.
    /// </summary>
    /// <param name="source">The original size to be scaled, represented as a <see cref="ImageSize"/>.</param>
    /// <param name="target">
    /// The target size within which the source size should fit, represented as a <see cref="ImageSize"/>.
    /// If either width or height of the target is zero, the scaling will be based on the non-zero dimension.
    /// </param>
    /// <returns>
    /// A new <see cref="ImageSize"/> representing the scaled size of the source dimension that fits within the target dimension.
    /// </returns>
    public static ImageSize CalculateSize(ImageSize source, ImageSize target)
    {
        if (target is { Width: 0, Height: 0 } || source.Equals(target))
        {
            return new ImageSize(target.Width, target.Height);
        }

        double widthFactor = 1, heightFactor = 1, factor = 1;
        if (target.Width > 0)
        {
            widthFactor = (double)target.Width / source.Width;
            if (target.Height == 0)
            {
                factor = widthFactor;
            }
        }
        if (target.Height > 0)
        {
            heightFactor = (double)target.Height / source.Height;
            if (target.Width == 0)
            {
                factor = heightFactor;
            }
        }

        if (Math.Abs(factor - 1) < double.Epsilon)
        {
            factor = Math.Min(widthFactor, heightFactor);
        }

        return new ImageSize((int)(source.Width * factor), (int)(source.Height * factor));
    }

    
    /// <summary>
    /// Converts a <see cref="ImageEdgeOffset"/> to a pair of points representing the top-left and bottom-right corners
    /// within a given total size.
    /// </summary>
    /// <param name="imgOffset">
    /// The <see cref="ImageEdgeOffset"/> specifying the imgOffset with optional top, left, bottom, and right offsets.
    /// </param>
    /// <param name="totalSize">
    /// The total size as a <see cref="ImageSize"/> within which the points are calculated.
    /// </param>
    /// <returns>
    /// A tuple containing the top-left and bottom-right points as <see cref="ImagePoint"/>.
    /// </returns>
    public static (ImagePoint TopLeft, ImagePoint BottomRight) ToPoints(ImageEdgeOffset imgOffset, ImageSize totalSize)
    {
        var topLeft = new ImagePoint(imgOffset.Left ?? 0, imgOffset.Top ?? 0);
        var bottomRight = new ImagePoint(totalSize.Width, totalSize.Height) - new ImagePoint(imgOffset.Right ?? 0, imgOffset.Bottom ?? 0);
        return (topLeft, bottomRight);
    }
    /// <summary>
    /// Converts a given imgOffset and total size into a point and size representation.
    /// </summary>
    /// <param name="imgOffset">
    /// The imgOffset, defined by its top, left, bottom, and right boundaries.
    /// </param>
    /// <param name="totalSize">
    /// The total size of the area, used to calculate the resulting point and size.
    /// </param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <see cref="ImagePoint"/> representing the top-left point of the imgOffset.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="ImageSize"/> representing the dimensions of the imgOffset.
    /// </description>
    /// </item>
    /// </list>
    /// </returns>
    public static (ImagePoint TopLeft, ImageSize Size) ToPointSize(ImageEdgeOffset imgOffset, ImageSize totalSize)
    {
        var points = ToPoints(imgOffset, totalSize);
        var calculatedSize = CalculateSize(points.TopLeft, points.BottomRight);
        return (points.TopLeft, calculatedSize);
    }
    /// <summary>
    /// Converts the specified top-left and bottom-right points into a <see cref="ImageEdgeOffset"/> object
    /// relative to the given total size.
    /// </summary>
    /// <param name="topLeft">The top-left point of the imgOffset.</param>
    /// <param name="bottomRight">The bottom-right point of the imgOffset.</param>
    /// <param name="totalSize">The total size used to calculate the relative imgOffset.</param>
    /// <returns>
    /// A <see cref="ImageEdgeOffset"/> object representing the relative imgOffset defined by the given points
    /// and total size.
    /// </returns>
    public static ImageEdgeOffset ToOffset(ImagePoint topLeft, ImagePoint bottomRight, ImageSize totalSize)
    {
        var br = new ImagePoint(totalSize.Width, totalSize.Height) - new ImagePoint(bottomRight.X, bottomRight.Y);
        return new ImageEdgeOffset(topLeft.Y, topLeft.X, br.Y, br.X);
    }

    
    /// <summary>
    /// Calculates the position of a point in pixels based on the given coordinate, unit, target size, and DPI.
    /// </summary>
    /// <param name="point">The coordinate of the point to calculate.</param>
    /// <param name="unit">The unit of measurement for the coordinate (e.g., points, inches, millimeters, percent).</param>
    /// <param name="targetSize">The size of the target image in pixels.</param>
    /// <param name="dpi">
    /// The dots per inch (DPI) value to use for the calculation. If not specified, a default DPI value is used.
    /// </param>
    /// <returns>An <see cref="ImagePoint"/> representing the calculated position in pixels.</returns>
    public static ImagePoint CalculatePoint(Coordinate2D point, LengthUnit unit, ImageSize targetSize, int? dpi = null)
    {
        dpi ??= ImageLayerDefaults.Dpi;

        var x = DimensionsUtility.GetPixels(point.X, unit, targetSize.Width, dpi.Value);
        var y = DimensionsUtility.GetPixels(point.Y, unit, targetSize.Height, dpi.Value);

        return new ImagePoint(x, y);
    }
    /// <summary>
    /// Calculates the edge offset for an image based on the specified position, unit, target size, and DPI.
    /// </summary>
    /// <param name="offset">
    /// The <see cref="Position2D"/> representing the top, left, bottom, and right offsets.
    /// </param>
    /// <param name="unit">
    /// The <see cref="LengthUnit"/> used to interpret the offset values.
    /// </param>
    /// <param name="targetSize">
    /// The <see cref="ImageSize"/> representing the dimensions of the target image.
    /// </param>
    /// <param name="dpi">
    /// The dots per inch (DPI) value used for conversion. If not specified, a default DPI value is used.
    /// </param>
    /// <returns>
    /// An <see cref="ImageEdgeOffset"/> representing the calculated pixel-based edge offsets.
    /// </returns>
    public static ImageEdgeOffset CalculateEdgeOffset(Position2D offset, LengthUnit unit, ImageSize targetSize, int? dpi = null)
    {
        dpi ??= ImageLayerDefaults.Dpi;

        int? top = offset.Top.HasValue
            ? DimensionsUtility.GetPixels(offset.Top.Value, unit, targetSize.Height, dpi.Value)
            : null;
        int? left = offset.Left.HasValue
            ? DimensionsUtility.GetPixels(offset.Left.Value, unit, targetSize.Width, dpi.Value)
            : null;
        int? bottom = offset.Bottom.HasValue
            ? DimensionsUtility.GetPixels(offset.Bottom.Value, unit, targetSize.Height, dpi.Value)
            : null;
        int? right = offset.Right.HasValue
            ? DimensionsUtility.GetPixels(offset.Right.Value, unit, targetSize.Width, dpi.Value)
            : null;

        return new ImageEdgeOffset(top, left, bottom, right);
    }
    /// <summary>
    /// Calculates the size of an image based on the specified dimensions, unit, target size, and DPI.
    /// </summary>
    /// <param name="size">The original size of the image, represented as a <see cref="Size2D"/> structure. Can be <c>null</c>.</param>
    /// <param name="unit">The unit of measurement for the size, such as points, inches, millimeters, or percent.</param>
    /// <param name="targetSize">The target size of the image, represented as an <see cref="ImageSize"/> structure.</param>
    /// <param name="dpi">
    /// The dots per inch (DPI) value used for the calculation. If <c>null</c>, the default DPI value from <see cref="ImageLayerDefaults.Dpi"/> is used.
    /// </param>
    /// <returns>
    /// A new <see cref="ImageSize"/> structure representing the calculated size of the image, or <c>null</c> if the input size is <c>null</c> or has zero width and height.
    /// </returns>
    public static ImageSize? CalculateSize(Size2D? size, LengthUnit unit, ImageSize targetSize, int? dpi = null)
    {
        if (size is null or { Width: 0, Height: 0 })
        {
            return null;
        }

        dpi ??= ImageLayerDefaults.Dpi;

        var width = DimensionsUtility.GetPixels(size.Value.Width, unit, targetSize.Width, dpi.Value);
        var height = DimensionsUtility.GetPixels(size.Value.Height, unit, targetSize.Height, dpi.Value);

        return new ImageSize(width, height);
    }
}
