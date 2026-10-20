namespace Regira.Office.Word.Layout;

internal static class MergeJoints
{
    /// <summary>
    /// Whether a merged input starts on the next odd page rather than a new page. An input padded to an even page count
    /// starts on an odd page, as Word's own section break for printing on both sides does, and so does the one after it.
    /// Padding counts the input's pages as created on their own, which holds only when it starts on an odd page; and a
    /// renderer drops the padding page break that ends a section when the next section starts on a new page.
    /// </summary>
    /// <param name="previousIsPadded">Whether the input before it has <see cref="Models.InputOptions.EnforceEvenAmountOfPages"/></param>
    /// <param name="isPadded">Whether the input itself has <see cref="Models.InputOptions.EnforceEvenAmountOfPages"/></param>
    public static bool StartsOnOddPage(bool previousIsPadded, bool isPadded) => previousIsPadded || isPadded;
}
