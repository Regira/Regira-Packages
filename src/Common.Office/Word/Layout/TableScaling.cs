namespace Regira.Office.Word.Layout;

// Called by the Spire, Syncfusion, Aspose and Gotenberg Word backends through InternalsVisibleTo: once they are published, its signatures stay, as ConditionalBlocks' do.
/// <summary>
/// Which tables <see cref="Models.ConversionOptions.AutoScaleTables"/> fits to a new text width.
/// </summary>
internal static class TableScaling
{
    /// <summary>
    /// Whether the table spans the text width: it is at most 10% narrower than it. A narrower table keeps its designed
    /// width, and so does one whose width is unknown.
    /// </summary>
    /// <param name="textWidth">The text width before the page setting</param>
    /// <param name="tableWidth">The table's width, in the same unit</param>
    public static bool SpansTextWidth(double textWidth, double tableWidth)
        => tableWidth > 0 && textWidth / tableWidth - 1 < .1;
}
