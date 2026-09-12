using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Regira.DAL.Abstractions;
using Regira.DAL.SqlServer.Core;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;

namespace Regira.DAL.SqlServer.Services;

/// <summary>
/// Restores a .bak file into the database named in the connection with RESTORE DATABASE, through <see cref="SqlServerOptions.BackupDirectory"/>
/// </summary>
public class SqlServerRestoreService(SqlServerOptions options, ILogger<SqlServerRestoreService>? logger = null) : IDbRestoreService
{
    public async Task Restore(IMemoryFile file)
    {
        if (!file.HasContent())
        {
            throw new ArgumentException("File has no content", nameof(file));
        }

        var builder = options.CreateConnectionStringBuilder();
        var targetDb = builder.GetDatabaseName();
        var location = BackupLocation.Create(options, targetDb);
        // the target may not exist yet, and SQL Server refuses to restore a database this connection is using
        builder.InitialCatalog = "master";

        await using var cn = new SqlConnection(builder.ConnectionString);
        await cn.OpenAsync();

        var exists = await Exists(cn, targetDb);
        if (exists && !options.Overwrite)
        {
            throw new InvalidOperationException($"Database {targetDb} already exists.");
        }

        try
        {
            try
            {
                await using var source = file.GetStream()!;
                await using var target = File.Create(location.LocalPath);
                await source.CopyToAsync(target);
            }
            catch (Exception ex) when (ex is DirectoryNotFoundException or UnauthorizedAccessException or IOException)
            {
                // the same failure the backup path names, from the other direction
                throw new IOException($"SQL Server reads the backup from {location.ServerPath}, but this process cannot write it at {location.LocalPath}. " +
                    $"Set {nameof(SqlServerOptions.LocalBackupDirectory)} to the same directory as this process reaches it, with write access for this process.", ex);
            }

            // reading the file list first proves SQL Server can open the backup before an existing database is dropped
            var backupFiles = await cn.QueryAsync<BackupFileInfo>("RESTORE FILELISTONLY FROM DISK = @path",
                new { path = location.ServerPath }, commandTimeout: 0);
            var moves = await PlanFileMoves(cn, targetDb, backupFiles);

            if (exists)
            {
                logger?.LogDebug("Dropping database {Database} to restore over it", targetDb);
                await Drop(cn, targetDb);
            }

            var parameters = new DynamicParameters(new { targetDb, path = location.ServerPath });
            var moveClauses = new List<string>();
            foreach (var (logicalName, physicalName) in moves)
            {
                var i = moveClauses.Count;
                parameters.Add($"logical{i}", logicalName);
                parameters.Add($"physical{i}", physicalName);
                moveClauses.Add($"MOVE @logical{i} TO @physical{i}");
            }
            // REPLACE also overwrites files left at the target paths, e.g. by a database that was offline when it was dropped
            var replace = options.Overwrite ? ", REPLACE" : string.Empty;

            logger?.LogDebug("Restoring database {Database} from {Path}", targetDb, location.ServerPath);
            await cn.ExecuteAsync($"RESTORE DATABASE @targetDb FROM DISK = @path WITH {string.Join(", ", moveClauses)}{replace}",
                parameters, commandTimeout: 0);
        }
        finally
        {
            location.DeleteLocalFile(logger);
        }
    }

    public Task<bool> Exists(IDbConnection cn, string databaseName)
        => cn.ExecuteScalarAsync<bool>("SELECT CAST(CASE WHEN DB_ID(@databaseName) IS NULL THEN 0 ELSE 1 END AS bit)", new { databaseName });

    /// <summary>
    /// Every file goes to the server's default data or log directory, named after the target database,
    /// so a copy restored beside its source never collides with the files the source still uses
    /// </summary>
    private static async Task<List<(string LogicalName, string PhysicalName)>> PlanFileMoves(IDbConnection cn, string targetDb, IEnumerable<BackupFileInfo> backupFiles)
    {
        var (dataDirectory, logDirectory) = await cn.QuerySingleAsync<(string?, string?)>(
            "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)) AS DataPath, CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000)) AS LogPath");
        var fileName = ServerPaths.ToFileName(targetDb);

        var moves = new List<(string, string)>();
        int dataFiles = 0, logFiles = 0;
        foreach (var backupFile in backupFiles)
        {
            var isLog = backupFile.Type == "L";
            string suffix;
            if (isLog)
            {
                logFiles++;
                suffix = logFiles == 1 ? "_log" : $"_log{logFiles}";
            }
            else
            {
                dataFiles++;
                suffix = dataFiles == 1 ? string.Empty : $"_{dataFiles}";
            }

            var directory = (isLog ? logDirectory : dataDirectory)
                ?? ServerPaths.GetDirectory(backupFile.PhysicalName)
                ?? throw new InvalidOperationException($"No directory to restore {backupFile.LogicalName} into");
            moves.Add((backupFile.LogicalName, ServerPaths.Combine(directory, fileName + suffix + ServerPaths.GetExtension(backupFile.PhysicalName))));
        }
        return moves;
    }

    private static Task Drop(IDbConnection cn, string databaseName)
        // ROLLBACK IMMEDIATE ends the sessions still using it; a database stuck RESTORING has none and refuses ALTER
        => cn.ExecuteAsync("""
            DECLARE @quoted nvarchar(258) = QUOTENAME(@databaseName);
            IF DATABASEPROPERTYEX(@databaseName, 'Status') = N'ONLINE'
                EXEC (N'ALTER DATABASE ' + @quoted + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE');
            EXEC (N'DROP DATABASE ' + @quoted);
            """, new { databaseName }, commandTimeout: 0);

    /// <summary>
    /// One row of RESTORE FILELISTONLY
    /// </summary>
    private sealed class BackupFileInfo
    {
        public string LogicalName { get; set; } = null!;
        public string PhysicalName { get; set; } = null!;
        /// <summary>
        /// D = data, L = log, S = FILESTREAM or memory-optimized container, F = full-text catalog
        /// </summary>
        public string Type { get; set; } = null!;
    }
}
