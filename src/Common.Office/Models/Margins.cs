namespace Regira.Office.Models;

/// <summary>
/// Margin in points
/// </summary>
public class Margins : IEquatable<Margins>
{
    public const float DEFAULT_MARGIN = 70.85f; //(~ default int Spire.Doc)

    public float Top { get; set; } = DEFAULT_MARGIN;
    public float Right { get; set; } = DEFAULT_MARGIN;
    public float Bottom { get; set; } = DEFAULT_MARGIN;
    public float Left { get; set; } = DEFAULT_MARGIN;

    public static implicit operator Margins(float margin)
    {
        return new[] { margin };
    }
    public static implicit operator Margins(float[] margins)
    {
        if ((margins.Length) <= 0)
        {
            throw new ArgumentNullException(nameof(margins));
        }

        if (margins.Length > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(margins), margins.Length, "Max 4 margins");
        }

        if (margins.Length == 1)
        {
            return new Margins { Top = margins[0], Right = margins[0], Bottom = margins[0], Left = margins[0] };
        }

        if (margins.Length == 2)
        {
            return new Margins { Top = margins[1], Right = margins[0], Bottom = margins[1], Left = margins[0] };
        }

        if (margins.Length == 3)
        {
            return new Margins { Top = margins[0], Right = margins[1], Bottom = margins[2], Left = margins[1] };
        }

        return new Margins { Top = margins[0], Right = margins[1], Bottom = margins[2], Left = margins[3] };
    }

    public bool Equals(Margins? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Top.Equals(other.Top) && Right.Equals(other.Right) && Bottom.Equals(other.Bottom) && Left.Equals(other.Left);
    }
}