namespace Regira.IO.Storage.GitHub;

public class GitHubOptions
{
    /// <summary>
    /// The URI of the GitHub repository where content will be stored, in the format "https://api.github.com/repos/{owner}/{repo}". This is required and must be a valid GitHub API endpoint for repository contents.
    /// </summary>
    public string Uri { get; set; } = null!;
    /// <summary>
    /// Optional personal access token or other authentication key to use for GitHub API requests. If not specified, unauthenticated requests will be made, which may be subject to stricter rate limits.
    /// </summary>
    public string? Key { get; set; }
    /// <summary>
    /// Optional user agent string to use for GitHub API requests. If not specified, the name of the executing assembly will be used as the user agent.
    /// </summary>
    public string? UserAgent { get; set; }
    /// <summary>
    /// Optional branch name to use when saving content. If not specified, the default branch (usually "main") will be used.
    /// </summary>
    public string Branch { get; set; } = "main";
    /// <summary>
    /// Optional commit message to use when saving content. If not specified, a default message will be used.
    /// </summary>
    public string? CommitMessage { get; set; }
    /// <summary>
    /// Optional path within the repository where the content will be stored. If not specified, content will be stored at the root of the repository.
    /// </summary>
    public string? ContentPath { get; set; }
}
