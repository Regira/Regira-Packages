namespace Regira.IO.Storage.Utilities;

// Called by the Azure, GitHub and SSH storage packages through InternalsVisibleTo: once they are published, its signatures stay, as ConditionalBlocks' do.
/// <summary>
/// Paths separated by <c>/</c>, as the blob, GitHub and SFTP backends name their files: a backslash reads as a
/// <c>/</c>, and an identifier is the path below the backend's root.
/// </summary>
internal static class UrlPath
{
    /// <summary>
    /// The path of <paramref name="filename"/> under <paramref name="root"/>; one already under it stays as it is. With
    /// <paramref name="rooted"/>, a combined path starts with <c>/</c>, as an SFTP path does.
    /// </summary>
    public static string GetUri(string filename, string root, bool rooted = false)
    {
        var processedFilename = ConvertBackSlashes(filename);
        if (processedFilename.StartsWith(root, StringComparison.InvariantCultureIgnoreCase))
        {
            return processedFilename;
        }

        var combined = Combine(root, processedFilename);
        return rooted ? $"/{combined}" : combined;
    }
    public static string GetRelativeUri(string? path, string root, bool rooted = false)
    {
        var processedPath = ConvertBackSlashes(path ?? string.Empty);
        var prefix = GetPrefix(processedPath, root, rooted);
        var filename = GetCleanFileName(processedPath);

        return !string.IsNullOrEmpty(prefix)
            ? Combine(prefix, filename)
            : filename;
    }
    public static string GetCleanFileName(string input)
    {
        var processedInput = ConvertBackSlashes(input);
        var startIndex = processedInput.LastIndexOf('/');

        return startIndex > -1
            ? processedInput.Substring(startIndex + 1)
            : processedInput;
    }
    public static string? GetRelativeFolder(string? input, string root)
    {
        var processedInput = ConvertBackSlashes(input ?? string.Empty).TrimEnd('/');
        var startIndex = processedInput.StartsWith(root)
            ? root.Length
            : 0;
        var endIndex = processedInput.LastIndexOf('/') - startIndex;
        return endIndex <= 0
            ? null
            : processedInput.Substring(startIndex, endIndex).Trim('/');
    }
    public static string? GetPrefix(string input, string root, bool rooted = false)
    {
        var path = GetUri(input, root, rooted);
        var startIndex = path.StartsWith(root)
            ? root.Length
            : 0;
        var endIndex = path.LastIndexOf('/') - startIndex;
        return endIndex <= 0
            ? null
            : path.Substring(startIndex, endIndex).Trim('/');
    }
    public static string ConvertBackSlashes(string input)
        => input.Replace('\\', '/');
    public static string Combine(params string[] segments)
        => string.Join("/", segments
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => ConvertBackSlashes(s).Trim('/'))
            )
            .TrimStart('/');
    /// <summary>The path with its <c>.</c> and <c>..</c> segments resolved and its empty segments dropped.</summary>
    public static string NormalizePath(string path)
    {
        var leadingSlash = path.StartsWith("/");
        var result = new List<string>();
        foreach (var part in path.Split('/'))
        {
            if (part == "..") { if (result.Count > 0) result.RemoveAt(result.Count - 1); }
            else if (part is not "" and not ".") result.Add(part);
        }
        var joined = string.Join("/", result);
        return leadingSlash ? "/" + joined : joined;
    }
    /// <summary>The normalized path, when it lies within <paramref name="root"/>.</summary>
    /// <exception cref="UnauthorizedAccessException">
    /// The path climbs out of the root, or holds a null character, where a server may end it.
    /// </exception>
    public static string EnsureContained(string absolutePath, string root)
    {
        var normalized = NormalizePath(absolutePath);
        if (absolutePath.Contains('\0') || !IsWithin(normalized, root))
        {
            throw new UnauthorizedAccessException($"Path '{absolutePath}' escapes the root context.");
        }
        return normalized;
    }
    /// <summary>
    /// <paramref name="uri"/>, when it addresses a path within <paramref name="root"/> on the root's host: its path as a
    /// client sends it, the dot segments resolved and <c>%2e</c> read as a dot, and as a server reads it once it decodes
    /// the percent-escapes, <c>%2f</c> and <c>%5c</c> read as a <c>/</c>.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">
    /// The address leaves the root, or its path holds a null character, where a server may end it.
    /// </exception>
    public static Uri EnsureContained(Uri uri, Uri root)
    {
        var sameServer = Uri.Compare(uri, root, UriComponents.SchemeAndServer, UriFormat.UriEscaped, StringComparison.OrdinalIgnoreCase) == 0;
        var decodedPath = ConvertBackSlashes(Uri.UnescapeDataString(uri.AbsolutePath));
        if (!sameServer
            || decodedPath.Contains('\0')
            || !IsWithin(NormalizePath(uri.AbsolutePath), root.AbsolutePath)
            || !IsWithin(NormalizePath(decodedPath), Uri.UnescapeDataString(root.AbsolutePath)))
        {
            throw new UnauthorizedAccessException($"Address '{uri.OriginalString}' escapes the root context.");
        }
        return uri;
    }
    private static bool IsWithin(string normalizedPath, string root)
    {
        var normalizedRoot = root.TrimEnd('/');
        return normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.Ordinal) || normalizedPath == normalizedRoot;
    }
}
