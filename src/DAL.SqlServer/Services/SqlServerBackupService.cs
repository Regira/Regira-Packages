using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Regira.DAL.Abstractions;
using Regira.DAL.SqlServer.Core;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;

namespace Regira.DAL.SqlServer.Services;

/// <summary>
/// Backs up the database named in the connection with BACKUP DATABASE, through <see cref="SqlServerOptions.BackupDirectory"/>
/// </summary>
public class SqlServerBackupService(SqlServerOptions options, ILogger<SqlServerBackupService>? logger = null) : IDbBackupService
{
    public async Task<IMemoryFile> Backup()
    {
        var builder = options.CreateConnectionStringBuilder();
        var databaseName = builder.GetDatabaseName();
        var location = BackupLocation.Create(options, databaseName);

        await using var cn = new SqlConnection(builder.ConnectionString);
        await cn.OpenAsync();
        var written = false;
        try
        {
            logger?.LogDebug("Backing up database {Database} to {Path}", databaseName, location.ServerPath);
            // COPY_ONLY: an on-demand backup must not reset the differential base or the log chain of a scheduled backup plan
            await cn.ExecuteAsync("BACKUP DATABASE @databaseName TO DISK = @path WITH COPY_ONLY",
                new { databaseName, path = location.ServerPath }, commandTimeout: 0);
            written = true;

            var bytes = await ReadBackupFile(location);
            return bytes.ToMemoryFile();
        }
        finally
        {
            if (!location.DeleteLocalFile(logger) && written)
            {
                // this process cannot reach the file — the case LocalBackupDirectory exists for — so SQL Server removes it
                await DeleteServerFile(cn, location);
            }
        }
    }

    /// <summary>
    /// Asks SQL Server to delete a backup file this process cannot reach — a path this service composed itself. Both
    /// procedures need sysadmin; without it the file stays, and saying where is all that is left to do.
    /// </summary>
    private async Task DeleteServerFile(SqlConnection cn, BackupLocation location)
    {
        try
        {
            // xp_delete_files exists from SQL Server 2019; xp_delete_file, before it, is the only way, though a
            // later server's backup does not pass its format check
            await cn.ExecuteAsync("""
                IF OBJECT_ID(N'master.sys.xp_delete_files') IS NOT NULL
                    EXEC master.sys.xp_delete_files @path;
                ELSE
                    EXEC master.sys.xp_delete_file 0, @path;
                """, new { path = location.ServerPath });
        }
        catch (SqlException ex)
        {
            logger?.LogWarning(ex, "The backup file {Path} is left on the server: this process cannot reach it at {LocalPath}, and SQL Server did not delete it",
                location.ServerPath, location.LocalPath);
        }
    }

    private static async Task<byte[]> ReadBackupFile(BackupLocation location)
    {
        try
        {
            return await File.ReadAllBytesAsync(location.LocalPath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
        {
            throw new IOException($"SQL Server wrote the backup to {location.ServerPath}, but this process cannot read it at {location.LocalPath}. " +
                $"Set {nameof(SqlServerOptions.LocalBackupDirectory)} to the same directory as this process reaches it, with read access for this process.", ex);
        }
    }
}
