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

        try
        {
            await using var cn = new SqlConnection(builder.ConnectionString);
            await cn.OpenAsync();

            logger?.LogDebug("Backing up database {Database} to {Path}", databaseName, location.ServerPath);
            // COPY_ONLY: an on-demand backup must not reset the differential base or the log chain of a scheduled backup plan
            await cn.ExecuteAsync("BACKUP DATABASE @databaseName TO DISK = @path WITH COPY_ONLY",
                new { databaseName, path = location.ServerPath }, commandTimeout: 0);

            var bytes = await ReadBackupFile(location);
            return bytes.ToMemoryFile();
        }
        finally
        {
            location.DeleteLocalFile(logger);
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
