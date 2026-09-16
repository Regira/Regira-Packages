using Microsoft.Extensions.Logging;
using Regira.DAL.SqlServer.Core;

namespace Regira.DAL.SqlServer.Services;

/// <summary>
/// One .bak file in the backup directory, addressed as SQL Server sees it and as this process sees it
/// </summary>
internal sealed record BackupLocation(string ServerPath, string LocalPath)
{
    public static BackupLocation Create(SqlServerOptions options, string databaseName)
    {
        if (string.IsNullOrWhiteSpace(options.BackupDirectory))
        {
            throw new ArgumentException($"{nameof(SqlServerOptions.BackupDirectory)} missing: SQL Server writes and reads the backup file itself, so it needs a directory that both SQL Server and this process can reach");
        }

        // a new file per call, so concurrent calls never share one
        var fileName = $"{ServerPaths.ToFileName(databaseName)}_{Guid.NewGuid():N}.bak";
        var localDirectory = string.IsNullOrWhiteSpace(options.LocalBackupDirectory) ? options.BackupDirectory : options.LocalBackupDirectory;
        return new BackupLocation(ServerPaths.Combine(options.BackupDirectory, fileName), Path.Combine(localDirectory, fileName));
    }

    /// <summary>
    /// Deletes the file through <see cref="LocalPath"/>.
    /// </summary>
    /// <returns>Whether this process finds the file at that path — <c>false</c> when it is missing there, which for a
    /// file SQL Server wrote means this process does not reach the directory through <see cref="LocalPath"/></returns>
    public bool DeleteLocalFile(ILogger? logger)
    {
        try
        {
            if (!File.Exists(LocalPath))
            {
                return false;
            }
            File.Delete(LocalPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // cleanup must never hide the outcome of the backup or restore itself
            logger?.LogWarning(ex, "Could not delete backup file {Path}", LocalPath);
            return true;
        }
    }
}
