using Regira.Utilities;
using Regira.Web.Utilities;

namespace Regira.IO.Storage.GitHub;

public static class GitHubExtensions
{
    public static string ToDownloadUri(this string gitUrl, string? folder = null, string? defaultBranch = null)
    {
        var gitSegments = gitUrl
            .Substring("https://api.github.com/repos/".Length)// to be replaced below
            .Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        var owner = gitSegments.First();
        var repo = gitSegments.Skip(1).First();
        // Extract full repo-relative path from the URL rather than using the caller-supplied folder,
        // because folder is Root-relative and would be missing the ContentPath prefix.
        const string contentsMarker = "/contents/";
        var contentsIdx = gitUrl.IndexOf(contentsMarker, StringComparison.OrdinalIgnoreCase);
        var file = contentsIdx >= 0
            ? gitUrl.Substring(contentsIdx + contentsMarker.Length).Split('?').First()
            : $"{folder}/{gitSegments.Last().Split("?".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).First()}".TrimStart('/');

        var query = UriUtility.ToQueryDictionary(gitSegments.Last()
            .Split("?".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Last());
        query.TryGetValue("ref", out var branch);
        if (string.IsNullOrEmpty(branch))
            branch = defaultBranch;

        return $"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{file}";
    }
}