using System.Diagnostics;

namespace Regira.Dimensions;

[DebuggerDisplay("Coordinate2D = [{X},{Y}]")]
public readonly struct Coordinate2D(float x, float y) : IEquatable<Coordinate2D>
{
    public float X { get; } = x;
    public float Y { get; } = y;

    public Coordinate2D(int x, int y) : this(x, (float)y) { }
    public Coordinate2D() : this(0, 0) { }


    // enable adding/subtracting from coordinates
    public static Coordinate2D operator +(Coordinate2D c1, Coordinate2D c2)
        => new(c1.X + c2.X, c1.Y - c2.Y);
    public static Coordinate2D operator -(Coordinate2D c1, Coordinate2D c2)
        => new(c1.X - c2.X, c1.Y - c2.Y);

    public static implicit operator Coordinate2D(int[] coordinate)
        => coordinate.Length switch
        {
            2 => new Coordinate2D(coordinate[0], coordinate[1]),
            _ => throw new ArgumentOutOfRangeException(nameof(coordinate))
        };
    public static implicit operator Coordinate2D(float[] coordinate)
        => coordinate.Length switch
        {
            2 => new Coordinate2D(coordinate[0], coordinate[1]),
            _ => throw new ArgumentOutOfRangeException(nameof(coordinate))
        };
    public static implicit operator float[](Coordinate2D coordinate)
        => [coordinate.X, coordinate.Y];

    // comparison
    public bool Equals(Coordinate2D other)
        => X.Equals(other.X) && Y.Equals(other.Y);
    public override bool Equals(object? obj)
        => obj is Coordinate2D other && Equals(other);
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