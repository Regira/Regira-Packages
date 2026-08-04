using System.Diagnostics;

namespace Regira.Media.Drawing.Dimensions;

/// <summary>
/// Represents edge-based offsets for an image, specifying the distance from the top, left, bottom, and right boundaries.<br />
/// This structure functions similarly to the CSS position: absolute model, where each property defines the offset from the respective edge of the containing element.<br />
/// It allows precise placement of an image or layer within a container by setting any combination of the four edge distances.
/// </summary>
[DebuggerDisplay("ImageOffset = [{Top},{Left},{Bottom},{Right}]")]
public struct ImageEdgeOffset(int? top, int? left, int? bottom, int? right) : IEquatable<ImageEdgeOffset>
{
    /// <summary>
    /// Sets the distance from the top edge.
    /// </summary>
    public int? Top { get; set; } = top;
    /// <summary>
    /// Sets the distance from the left edge.
    /// </summary>
    public int? Left { get; set; } = left;
    /// <summary>
    /// Sets the distance from the bottom edge.
    /// </summary>
    public int? Bottom { get; set; } = bottom;
    /// <summary>
    /// Sets the distance from the right edge.
    /// </summary>
    public int? Right { get; set; } = right;

    public ImageEdgeOffset(int top, int left) : this(top, left, null, null) { }
    public ImageEdgeOffset() : this(0, 0) { }

    
    // conversions
    public static implicit operator ImageEdgeOffset(int[] pos)
        => pos.Length switch
        {
            2 => new ImageEdgeOffset(pos[0], pos[1]),
            4 => new ImageEdgeOffset(pos[0], pos[1], pos[2], pos[3]),
            _ => throw new ArgumentOutOfRangeException(nameof(pos))
        };
    public static implicit operator int?[](ImageEdgeOffset pos)
        => [pos.Top, pos.Left, pos.Bottom, pos.Right];

    // comparison
    public bool Equals(ImageEdgeOffset other)
        => Top.Equals(other.Top) && Left.Equals(other.Left) && Bottom.Equals(other.Bottom) && Right.Equals(other.Right);
    public override bool Equals(object? obj)
        => obj is ImageEdgeOffset other && Equals(other);
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Top.GetHashCode();
            hashCode = hashCode * 397 ^ Left.GetHashCode();
            hashCode = hashCode * 397 ^ Right.GetHashCode();
            hashCode = hashCode * 397 ^ Bottom.GetHashCode();
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