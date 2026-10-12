using Regira.Utilities;

namespace Regira.Office.Models;

/// <summary>
/// Margins in points; <c>ImagePrintInputModel</c> reads them in hundredths of an inch.
/// </summary>
public class Margins : IEquatable<Margins>
{
    public const float DEFAULT_MARGIN = 70.85f; //(~ default int Spire.Doc)

    public float Top { get; set; } = DEFAULT_MARGIN;
    public float Right { get; set; } = DEFAULT_MARGIN;
    public float Bottom { get; set; } = DEFAULT_MARGIN;
    public float Left { get; set; } = DEFAULT_MARGIN;

    /// <summary>
    /// The same margins measured at another DPI (dots per inch): 37.8 at 96 is 28.35 at 72, PDF points.
    /// </summary>
    /// <param name="srcDpi">The DPI these margins are measured at.</param>
    /// <param name="targetDpi">The DPI to measure the returned margins at.</param>
    public Margins ModifyDpi(int srcDpi, int targetDpi)
    {
        return new Margins
        {
            Top = DimensionsUtility.ModifyDpi(Top, srcDpi, targetDpi),
            Right = DimensionsUtility.ModifyDpi(Right, srcDpi, targetDpi),
            Bottom = DimensionsUtility.ModifyDpi(Bottom, srcDpi, targetDpi),
            Left = DimensionsUtility.ModifyDpi(Left, srcDpi, targetDpi)
        };
    }

    public static implicit operator Margins(float margin)
    {
        return new[] { margin };
    }
    /// <summary>
    /// The margins an array gives: one value for every side, two for left and right then top and bottom, three for top, left and right, bottom, four for top, right, bottom, left. Two values are not CSS's order, which puts top and bottom first.
    /// </summary>
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