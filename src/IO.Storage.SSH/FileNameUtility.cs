using Regira.IO.Storage.Utilities;

namespace Regira.IO.Storage.SSH;

/// <summary><c>/</c>-separated paths under the backend's root; one implementation, shared by the storage backends.</summary>
public static class FileNameUtility
{
    public static string GetUri(string filename, string root)
        => UrlPath.GetUri(filename, root, rooted: true);
    public static string GetRelativeUri(string path, string root)
        => UrlPath.GetRelativeUri(path, root, rooted: true);
    public static string GetCleanFileName(string input)
        => UrlPath.GetCleanFileName(input);
    public static string? GetRelativeFolder(string input, string root)
        => UrlPath.GetRelativeFolder(input, root);
    public static string? GetPrefix(string input, string root)
        => UrlPath.GetPrefix(input, root, rooted: true);
    public static string ConvertBackSlashes(string input)
        => UrlPath.ConvertBackSlashes(input);
    public static string Combine(params string[] segments)
        => UrlPath.Combine(segments);
    public static string NormalizePath(string path)
        => UrlPath.NormalizePath(path);
    public static string EnsureContained(string absolutePath, string root)
        => UrlPath.EnsureContained(absolutePath, root);
}
