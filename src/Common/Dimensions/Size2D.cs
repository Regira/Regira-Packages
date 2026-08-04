using System.Diagnostics;

namespace Regira.Dimensions;

/// <summary>
/// Represents a two-dimensional size with width and height components.
/// </summary>
/// <remarks>
/// This struct provides functionality for performing mathematical operations on dimensions,
/// such as rounding, flooring, and scaling. It also supports implicit conversions from and to
/// various data types, including arrays and integers, for ease of use in different scenarios.
/// </remarks>
[DebuggerDisplay("Size2D = [{Width},{Height}]")]
public struct Size2D(float width = 0, float height = 0) : IEquatable<Size2D>
{
    public float Width { get; set; } = width;
    public float Height { get; set; } = height;


    public Size2D Round(int decimals = 0)
    {
        return new()
        {
            Width = (float)Math.Round(Width, decimals),
            Height = (float)Math.Round(Height, decimals)
        };
    }
    public Size2D Floor()
    {
        return new()
        {
            Width = (float)Math.Floor(Width),
            Height = (float)Math.Floor(Height)
        };
    }


    // enable multiply/division of dimensions
    public static Size2D operator *(Size2D dim, float n)
    {
        return new()
        {
            Width = dim.Width * n,
            Height = dim.Height * n
        };
    }
    public static Size2D operator /(Size2D dim, float n)
    {
        return new()
        {
            Width = dim.Width / n,
            Height = dim.Height / n
        };
    }


    public static implicit operator Size2D(int x)
    {
        return new Size2D(x, x);
    }
    public static implicit operator Size2D(int[] xy)
    {
        if (xy.Length != 2)
        {
            throw new ArgumentOutOfRangeException(nameof(xy));
        }

        return new Size2D { Width = xy[0], Height = xy[1] };
    }
    // make compatible with float[]
    public static implicit operator Size2D(float[] xy)
    {
        if (xy.Length != 2)
        {
            throw new ArgumentOutOfRangeException(nameof(xy));
        }

        return new Size2D { Width = xy[0], Height = xy[1] };
    }
    public static implicit operator float[](Size2D dimension)
    {
        return [dimension.Width, dimension.Height];
    }

    // comparison
    public static bool operator ==(Size2D dim1, Size2D dim2)
    {
        return Math.Abs(dim1.Width - dim2.Width) < float.Epsilon
               && Math.Abs(dim1.Height - dim2.Height) < float.Epsilon;
    }
    public static bool operator !=(Size2D dim1, Size2D dim2)
    {
        return !(dim1 == dim2);
    }
    public bool Equals(Size2D other)
    {
        return Math.Abs(Width - other.Width) < float.Epsilon && Math.Abs(Height - other.Height) < float.Epsilon;
    }
    public override bool Equals(object? obj)
    {
        return obj != null && Equals((Size2D)obj);
    }
    public override int GetHashCode()
    {
        unchecked
        {
            // ReSharper disable NonReadonlyMemberInGetHashCode
            return (Width.GetHashCode() * 397) ^ Height.GetHashCode();
            // ReSharper restore NonReadonlyMemberInGetHashCode
        }
    }
}