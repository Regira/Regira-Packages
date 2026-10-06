using Regira.Office.Word.Models;

namespace Regira.Office.Word.Layout;

/// <summary>How a merged input starts, after the one before it.</summary>
internal enum MergeJoint
{
    /// <summary>On the previous input's last page</summary>
    Continuous,
    /// <summary>On a new page</summary>
    NewPage,
    /// <summary>On the next odd page, a blank one inserted when needed</summary>
    OddPage
}

internal static class MergeJoints
{
    /// <summary>
    /// How a merged input starts: on a new page, or on the previous one's last page when asked to
    /// (<see cref="MergeOptions.FollowOn"/>). An input padded to an even page count starts on an odd page, as Word's own
    /// section break for printing on both sides does, and so does the one after it. Padding counts the input's pages as
    /// created on their own, which holds only when it starts on an odd page; and a renderer drops the padding page break
    /// that ends a section when the next section starts on a new page, or the next input fills it when it follows on.
    /// </summary>
    /// <param name="options">How the merge was asked to join its inputs</param>
    /// <param name="previousIsPadded">Whether the input before it has <see cref="InputOptions.EnforceEvenAmountOfPages"/></param>
    /// <param name="isPadded">Whether the input itself has <see cref="InputOptions.EnforceEvenAmountOfPages"/></param>
    public static MergeJoint Of(MergeOptions? options, bool previousIsPadded, bool isPadded)
        => previousIsPadded || isPadded ? MergeJoint.OddPage
            : options?.FollowOn == true ? MergeJoint.Continuous
            : MergeJoint.NewPage;
}
