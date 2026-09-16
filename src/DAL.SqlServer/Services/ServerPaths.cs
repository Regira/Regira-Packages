namespace Regira.DAL.SqlServer.Services;

/// <summary>
/// Paths on the SQL Server host, which need not use this process's separator — a Linux container behind a Windows client
/// </summary>
internal static class ServerPaths
{
    private static readonly char[] Separators = ['\\', '/'];
    private const string InvalidFileNameChars = "\\/:*?\"<>|";

    public static string Combine(string directory, string fileName)
    {
        var separator = directory.Contains('/') && !directory.Contains('\\') ? '/' : '\\';
        return directory.TrimEnd(Separators) + separator + fileName;
    }

    public static string? GetDirectory(string path)
    {
        var index = path.LastIndexOfAny(Separators);
        return index < 0 ? null : path[..index];
    }

    public static string GetExtension(string path)
    {
        var fileName = path[(path.LastIndexOfAny(Separators) + 1)..];
        var index = fileName.LastIndexOf('.');
        return index < 0 ? string.Empty : fileName[index..];
    }

    /// <summary>
    /// A database name can hold characters a file name cannot
    /// </summary>
    public static string ToFileName(string databaseName)
        => string.Concat(databaseName.Select(c => char.IsControl(c) || InvalidFileNameChars.Contains(c) ? '_' : c));
}
