namespace Regira.IO.Storage.GitHub;

public static class FileNameUtility
{
    public static string GetUri(string filename, string root)
    {
        var processedFilename = ConvertBackSlashes(filename);
        if (processedFilename.StartsWith(root, StringComparison.InvariantCultureIgnoreCase))
        {
            return processedFilename;
        }

        return Combine(root, processedFilename);
    }
    public static string GetRelativeUri(string? path, string root)
    {
        var processedPath = ConvertBackSlashes(path ?? string.Empty);
        var prefix = GetPrefix(processedPath, root);
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
    public static string? GetPrefix(string input, string root)
    {
        var path = GetUri(input, root);
        var startIndex = path.StartsWith(root)
            ? root.Length
            : 0;
        var endIndex = path.LastIndexOf('/') - startIndex;
        return endIndex <= 0
            ? null
            : path.Substring(startIndex, endIndex).Trim('/');
    }
    public static string ConvertBackSlashes(string input)
    {
        return input.Replace('\\', '/');
    }
    public static string Combine(params string[] segments)
    {
        return string.Join("/", segments
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => ConvertBackSlashes(s).Trim('/'))
            )
            .TrimStart('/');
    }
}