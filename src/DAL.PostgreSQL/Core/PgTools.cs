using System.Text;
using System.Text.RegularExpressions;
using Regira.DAL.PostgreSQL.Constants;

namespace Regira.DAL.PostgreSQL.Core;

/// <summary>
/// Locates <c>pg_dump</c> and <c>pg_restore</c> and composes their arguments. The services start the executable
/// directly, so no shell reads the arguments: a percent sign or a quote in a database, user or schema name reaches
/// the tool as written, on every platform.
/// </summary>
internal static class PgTools
{
    private static readonly Regex Placeholder = new(@"\{(\w+)\}", RegexOptions.Compiled);

    public static string DumpPath(string toolsDirectory)
        => Path.Combine(toolsDirectory, OperatingSystem.IsWindows() ? "pg_dump.exe" : "pg_dump");
    public static string RestorePath(string toolsDirectory)
        => Path.Combine(toolsDirectory, OperatingSystem.IsWindows() ? "pg_restore.exe" : "pg_restore");

    public static string BackupArguments(PgSettings settings, string? sourceDb, string targetPath, ICollection<string>? schemas)
    {
        var hasSchemas = schemas?.Any() ?? false;
        return Fill(hasSchemas ? BackupCommands.SchemaBackup : BackupCommands.FullBackup, new Dictionary<string, string>
        {
            ["Host"] = Escape(settings.Host),
            ["Port"] = Escape(settings.Port),
            ["Username"] = Escape(settings.Username),
            ["TargetPath"] = Escape(targetPath),
            ["SchemasArgs"] = hasSchemas ? string.Join(" ", schemas!.Select(x => $"--schema \"{Escape(x)}\"")) : string.Empty,
            ["SourceDb"] = Escape(sourceDb)
        });
    }

    public static string ListArchiveArguments(string sourcePath)
        => Fill(BackupCommands.ListArchive, new Dictionary<string, string> { ["SourcePath"] = Escape(sourcePath) });

    public static string RestoreArguments(PgSettings settings, string targetDb, string sourcePath)
        => Fill(BackupCommands.Restore, new Dictionary<string, string>
        {
            ["Host"] = Escape(settings.Host),
            ["Port"] = Escape(settings.Port),
            ["Username"] = Escape(settings.Username),
            ["TargetDb"] = Escape(targetDb),
            ["SourcePath"] = Escape(sourcePath)
        });

    /// <summary>
    /// The password travels in the process environment — <c>pg_dump</c> and <c>pg_restore</c> read
    /// <c>PGPASSWORD</c> — so it never appears in the arguments.
    /// </summary>
    public static IDictionary<string, string> Environment(PgSettings settings)
    {
        var environment = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(settings.Password))
        {
            environment["PGPASSWORD"] = settings.Password;
        }
        return environment;
    }

    /// <summary>
    /// Escapes a value for use between the double quotes of a process argument string, as the C runtime on Windows
    /// and .NET on every other platform split it: a quote is preceded by a backslash, and the backslashes before a
    /// quote — the closing one included — are doubled. Any other backslash stands for itself.
    /// </summary>
    internal static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = new StringBuilder(value.Length);
        var backslashes = 0;
        foreach (var c in value)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            escaped.Append('\\', c == '"' ? backslashes * 2 + 1 : backslashes);
            escaped.Append(c);
            backslashes = 0;
        }
        escaped.Append('\\', backslashes * 2);
        return escaped.ToString();
    }

    /// <summary>
    /// Fills every placeholder in one pass, so a value that happens to contain <c>{Name}</c> is never filled in turn.
    /// </summary>
    private static string Fill(string template, IReadOnlyDictionary<string, string> values)
        => Placeholder.Replace(template, m => values.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Value);
}
