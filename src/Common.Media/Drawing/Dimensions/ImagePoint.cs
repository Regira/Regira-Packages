using Regira.Dimensions;
using System.Diagnostics;

namespace Regira.Media.Drawing.Dimensions;

[DebuggerDisplay("ImagePoint = [{X},{Y}]")]
public readonly struct ImagePoint(int x, int y) : IEquatable<ImagePoint>
{
    public int X { get; } = x;
    public int Y { get; } = y;

    public ImagePoint() : this(0, 0) { }


    // enable adding/subtracting from points
    public static ImagePoint operator +(ImagePoint c1, ImagePoint c2)
        => new(c1.X + c2.X, c1.Y - c2.Y);
    public static ImagePoint operator -(ImagePoint c1, ImagePoint c2)
        => new(c1.X - c2.X, c1.Y - c2.Y);

    public static implicit operator ImagePoint(Coordinate2D point)
        => new((int)point.X, (int)point.Y);
    public static implicit operator ImagePoint(int[] point)
        => point.Length switch
        {
            2 => new ImagePoint(point[0], point[1]),
            _ => throw new ArgumentOutOfRangeException(nameof(point))
        };
    public static implicit operator int[](ImagePoint point)
        => [point.X, point.Y];

    // comparison
    public bool Equals(ImagePoint other)
        => X.Equals(other.X) && Y.Equals(other.Y);
    public override bool Equals(object? obj)
        => obj is ImagePoint other && Equals(other);
    public override int GetHashCode()
    {
        unchecked
        {
            return (X.GetHashCode() * 397) ^ Y.GetHashCode();
        }
    }

    /// <summary>
    /// Formatted as [X, Y]
    /// </summary>
    /// <returns></returns>
    public override string ToString()
        => $"[{X}, {Y}]";
}