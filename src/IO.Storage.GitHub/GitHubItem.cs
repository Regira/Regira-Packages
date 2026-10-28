namespace Regira.IO.Storage.GitHub;

public class GitHubItem
{
    public string Name { get; set; } = null!;
    public string Path { get; set; } = null!;
    public GitHubItemType Type { get; set; }
    public string Url { get; set; } = null!;
    // ReSharper disable once InconsistentNaming
    public string? Download_Url { get; set; }
    public string? Sha { get; set; }
    /// <summary>The file's size in bytes; 0 for a folder.</summary>
    public long Size { get; set; }
    public string? Content { get; set; }
}