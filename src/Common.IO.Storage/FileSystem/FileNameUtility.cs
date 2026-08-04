using System.Text.RegularExpressions;

namespace Regira.IO.Storage.FileSystem;

public static class FileNameUtility
{
    public static string GetAbsoluteUri(string filename, string? root)
    {
        var path = string.IsNullOrWhiteSpace(root) || filename.StartsWith(root, StringComparison.InvariantCultureIgnoreCase)
            ? filename
            : Combine(root, filename);
        return ConvertForwardSlashes(path);
    }
    public static string GetRelativeUri(string input, string? root)
    {
        var path = GetAbsoluteUri(input, root);
        var prefix = GetRelativeFolder(path, root);
        var filename = GetCleanFileName(path);
        var prefixedFilename = !string.IsNullOrEmpty(prefix)
            ? Combine(prefix, filename)
            : filename;
        return ConvertForwardSlashes(prefixedFilename);
    }
    public static string GetCleanFileName(string input)
    {
        return Path.GetFileName(input);
    }
    public static string? GetRelativeFolder(string input, string? root)
    {
        var processedInput = ConvertForwardSlashes(input);
        var startIndex = !string.IsNullOrWhiteSpace(root) && processedInput.StartsWith(root)
            ? root!.Length
            : 0;
        var endIndex = processedInput.LastIndexOf(Path.DirectorySeparatorChar) - startIndex;
        return endIndex <= 0
            ? null
            : processedInput.Substring(startIndex, endIndex).Trim(Path.DirectorySeparatorChar);
    }
    // Normalize both slash styles to the platform separator: on Windows this yields '\' (unchanged
    // behaviour), on Linux/macOS it yields '/'. The old hard-coded '\' produced paths where '\' is a
    // literal filename character on POSIX filesystems, so writes escaped their root and 500'd.
    public static string ConvertForwardSlashes(string input)
    {
        return input.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
    }
    public static string Combine(params string?[] segments)
    {
        var validSegments = segments.Where(s => !string.IsNullOrEmpty(s)).ToArray();
        return Path.Combine(validSegments!);
    }
    /// <summary>
    /// Extracts <c>\\server\share</c> from a UNC path (also accepts forward slashes and the
    /// <c>\\?\UNC\</c> long-path prefix) — returns null for non-UNC input.
    /// </summary>
    public static string? GetUncShareRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        var normalized = path.Replace('/', '\\');
        const string longUncPrefix = @"\\?\UNC\";
        if (normalized.StartsWith(longUncPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = @"\\" + normalized.Substring(longUncPrefix.Length);
        }
        if (!normalized.StartsWith(@"\\") || normalized.StartsWith(@"\\?\") || normalized.StartsWith(@"\\.\"))
        {
            return null;
        }
        var segments = normalized.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2
            ? $@"\\{segments[0]}\{segments[1]}"
            : null;
    }

    public static string EnsureContained(string absolutePath, string root)
    {
        var normalized = Path.GetFullPath(absolutePath);
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!normalized.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalized.TrimEnd(Path.DirectorySeparatorChar), normalizedRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Path '{absolutePath}' escapes the root context.");
        }
        return normalized;
    }

    public static bool IsFile(string uri, string root)
    {
        uri = GetAbsoluteUri(uri, root);
        var attr = File.GetAttributes(uri);
        return (attr & FileAttributes.Directory) != FileAttributes.Directory;
    }
    public static bool IsDirectory(string uri, string root)
    {
        uri = GetAbsoluteUri(uri, root);
        var attr = File.GetAttributes(uri);
        return (attr & FileAttributes.Directory) == FileAttributes.Directory;
    }


    public static string[] ReservedWords =>
    [
        "CON", "PRN", "AUX", "CLOCK$", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];
    //http://stackoverflow.com/questions/309485/c-sharp-sanitize-file-name#answer-12924582
    public static string SanitizeFilename(string filename, string replacement = "_XXX_")
    {
        var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        var invalidReStr = $@"[{invalidChars}]+"; // "<>\|

        var segments = ConvertForwardSlashes(filename)
            .Split('\\')
            .Select(s => Regex.Replace(s, invalidReStr, "_"))
            .Select(s => ReservedWords.Contains(s, StringComparer.InvariantCultureIgnoreCase) ? replacement : s);

        return string.Join("\\", segments);
    }
}