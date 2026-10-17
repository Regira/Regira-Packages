namespace Regira.Office.Word.Layout;

/// <summary>
/// How <see cref="Models.ConversionOptions.AutoScalePictures"/> scales a picture when a page setting changes the text width.
/// </summary>
internal static class PictureScaling
{
    /// <summary>The largest width or height of a Word shape, in points: 22 inches.</summary>
    public const double MaxShapeSize = 1584;

    /// <summary>
    /// The factor a picture is scaled by: as much as the text width changes, stopping where the picture would pass
    /// <see cref="MaxShapeSize"/>, so it keeps its proportions at the largest size Word holds. A picture already past the
    /// limit is brought to it, whichever way the text width moves: Spire.Doc and Aspose.Words set no size past it, so a
    /// picture scaled by the text width alone would throw there. A text width that is unknown before or after the page
    /// setting, or that the setting leaves as it was, scales nothing (1), and a backend then leaves the picture as it is,
    /// one past the limit too.
    /// </summary>
    /// <param name="originalTextWidth">The text width before the page setting, in points</param>
    /// <param name="newTextWidth">The text width after it, in points</param>
    /// <param name="width">The picture's width, in points</param>
    /// <param name="height">The picture's height, in points</param>
    public static double Factor(double originalTextWidth, double newTextWidth, double width, double height)
    {
        if (originalTextWidth <= 0 || newTextWidth <= 0 || newTextWidth == originalTextWidth)
        {
            return 1;
        }

        var factor = newTextWidth / originalTextWidth;
        return width > 0 && height > 0
            ? Math.Min(factor, Math.Min(MaxShapeSize / width, MaxShapeSize / height))
            : factor;
    }

    /// <summary>
    /// The picture's size scaled by <paramref name="factor"/>, no side past <see cref="MaxShapeSize"/>. A factor that
    /// stops a side at the limit takes it a rounding error past for about one size in twenty, and Spire.Doc and
    /// Aspose.Words throw for the smallest step past it.
    /// </summary>
    /// <param name="width">The picture's width, in points</param>
    /// <param name="height">The picture's height, in points</param>
    /// <param name="factor">What <see cref="Factor"/> gives</param>
    public static (double Width, double Height) Size(double width, double height, double factor)
        => (Math.Min(width * factor, MaxShapeSize), Math.Min(height * factor, MaxShapeSize));
}
