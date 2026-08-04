using System.Diagnostics;

namespace Regira.Media.Drawing.Dimensions;

/// <summary>
/// Represents the dimensions of an image, defined by its width and height.
/// </summary>
[DebuggerDisplay("ImageSize = [{Width},{Height}]")]
public struct ImageSize(int width = 0, int height = 0) : IEquatable<ImageSize>
{
    public int Width { get; set; } = width;
    public int Height { get; set; } = height;

    public static ImageSize Empty => new(0, 0);


    // enable multiply/division of dimensions
    public static ImageSize operator *(ImageSize dim, int n)
    {
        return new()
        {
            Width = dim.Width * n,
            Height = dim.Height * n
        };
    }
    public static ImageSize operator /(ImageSize dim, int n)
    {
        return new()
        {
            Width = dim.Width / n,
            Height = dim.Height / n
        };
    }

    // conversions
    public static implicit operator ImageSize(int x)
    {
        return new ImageSize(x, x);
    }
    public static implicit operator ImageSize(int[] xy)
    {
        if (xy.Length != 2)
        {
            throw new ArgumentOutOfRangeException(nameof(xy));
        }

        return new ImageSize { Width = xy[0], Height = xy[1] };
    }
    public static implicit operator int[](ImageSize dimension)
    {
        return [dimension.Width, dimension.Height];
    }

    // comparison
    public static bool operator ==(ImageSize dim1, ImageSize dim2)
        => Math.Abs(dim1.Width - dim2.Width) == 0 && Math.Abs(dim1.Height - dim2.Height) == 0;
    public static bool operator !=(ImageSize dim1, ImageSize dim2)
        => !(dim1 == dim2);

    public bool Equals(ImageSize other)
        => Math.Abs(Width - other.Width) == 0 && Math.Abs(Height - other.Height) == 0;
    public override bool Equals(object? obj)
        => obj != null && Equals((ImageSize)obj);
    public override int GetHashCode()
    {
        unchecked
        {
            return Width.GetHashCode() * 397 ^ Height.GetHashCode();
        }
    }

    /// <summary>
    /// Formatted as [Width, Height]
    /// </summary>
    /// <returns></returns>
    public override string ToString()
        => $"[{Width}, {Height}]";
}