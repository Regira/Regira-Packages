using System.Diagnostics;

namespace Regira.Dimensions;

/// <summary>
/// Represents a two-dimensional position with optional top, left, bottom, and right offsets.
/// </summary>
/// <remarks>
/// This struct is commonly used to define positional offsets in two-dimensional space, 
/// such as margins or padding, and supports implicit conversions from arrays of integers or floats.
/// It works similarly to the CSS <c>position: absolute</c> property, allowing precise control over 
/// the placement of elements relative to their container.
/// </remarks>
[DebuggerDisplay("Position2D = [{Top},{Left},{Bottom},{Right}]")]
public struct Position2D(float? top, float? left, float? bottom, float? right) : IEquatable<Position2D>
{
    /// <summary>
    /// Sets the distance from the top edge.
    /// </summary>
    public float? Top { get; set; } = top;
    /// <summary>
    /// Sets the distance from the left edge.
    /// </summary>
    public float? Left { get; set; } = left;
    /// <summary>
    /// Sets the distance from the bottom edge.
    /// </summary>
    public float? Bottom { get; set; } = bottom;
    /// <summary>
    /// Sets the distance from the right edge.
    /// </summary>
    public float? Right { get; set; } = right;

    public Position2D(float top, float left) : this(top, left, null, null) { }
    public Position2D() : this(0, 0) { }

    public static implicit operator Position2D(int[] pos)
        => pos.Length switch
        {
            2 => new Position2D(pos[0], pos[1]),
            4 => new Position2D(pos[0], pos[1], pos[2], pos[3]),
            _ => throw new ArgumentOutOfRangeException(nameof(pos))
        };
    public static implicit operator Position2D(float[] pos)
        => pos.Length switch
        {
            2 => new Position2D(pos[0], pos[1]),
            4 => new Position2D(pos[0], pos[1], pos[2], pos[3]),
            _ => throw new ArgumentOutOfRangeException(nameof(pos))
        };
    public static implicit operator float?[](Position2D pos)
        => [pos.Top, pos.Left, pos.Bottom, pos.Right];

    // comparison
    public bool Equals(Position2D other)
        => Top.Equals(other.Top) && Left.Equals(other.Left) && Bottom.Equals(other.Bottom) && Right.Equals(other.Right);
    public override bool Equals(object? obj)
        => obj is Position2D other && Equals(other);
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Top.GetHashCode();
            hashCode = (hashCode * 397) ^ Left.GetHashCode();
            hashCode = (hashCode * 397) ^ Right.GetHashCode();
            hashCode = (hashCode * 397) ^ Bottom.GetHashCode();
            return hashCode;
        }
    }

    /// <summary>
    /// Formatted as [Top, Left, Bottom, Right]
    /// </summary>
    /// <returns></returns>
    public override string ToString()
        => $"[{Top}, {Left}, {Bottom}, {Right}]";
}