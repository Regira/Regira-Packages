namespace Regira.Office.Word.Models;

/// <summary>
/// How <see cref="Abstractions.IWordMerger"/> joins the documents it merges.
/// </summary>
public record MergeOptions
{
    /// <summary>
    /// Runs every document after the first on from the previous one's last page. By default each starts on a new page.
    /// </summary>
    public bool FollowOn { get; set; }
}
