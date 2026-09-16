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
            catch (Exception ex) when (ex is DirectoryNotFoundException or UnauthorizedAccessException)
            {
                // the same failure the backup path names, from the other direction
                throw new IOException($"SQL Server reads the backup from {location.ServerPath}, but this process cannot write it at {location.LocalPath}. " +
                    $"Set {nameof(SqlServerOptions.LocalBackupDirectory)} to the same directory as this process reaches it, with write access for this process.", ex);
            }

            // reading the file list first proves SQL Server can open the backup before an existing database is dropped
            var backupFiles = await cn.QueryAsync<BackupFileInfo>("RESTORE FILELISTONLY FROM DISK = @path",
                new { path = location.ServerPath }, commandTimeout: 0);
            var moves = await PlanFileMoves(cn, targetDb, backupFiles);

            // a planned path another database still owns — renamed, detached-and-reattached, or offline — is its data:
            // refuse before anything is dropped, since REPLACE would overwrite an offline database's file without a word
            var taken = (await cn.QueryAsync<(string Database, string Path)>(
                    "SELECT DB_NAME(database_id), physical_name FROM sys.master_files WHERE database_id <> ISNULL(DB_ID(@targetDb), 0)", new { targetDb }))
                .Where(file => moves.Any(move => string.Equals(move.PhysicalName, file.Path, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (taken.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Restoring {targetDb} would write {string.Join(", ", taken.Select(file => $"{file.Path} (a file of database {file.Database})"))}. " +
                    "Rename or remove that database's files first; this restore names its files after the target database.");
            }

            if (exists)
            {
                // an offline database is dropped without its files, so name the ones the restore will not overwrite
                var existingFiles = await cn.QueryAsync<string>("SELECT physical_name FROM sys.master_files WHERE database_id = DB_ID(@targetDb)", new { targetDb });
                var leftOver = existingFiles
                    .Where(path => !moves.Any(move => string.Equals(move.PhysicalName, path, StringComparison.OrdinalIgnoreCase)))
                    .ToArray();

                logger?.LogDebug("Dropping database {Database} to restore over it", targetDb);
                await Drop(cn, targetDb);

                if (leftOver.Length > 0)
                {
                    logger?.LogWarning("Database {Database} was replaced; SQL Server keeps its former files {Files}, which the restore does not reuse", targetDb, leftOver);
                }
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
            // REPLACE overwrites the files the replaced database left behind — it was offline when it was dropped. A new
            // database gets no REPLACE, so a stray file at a planned path fails the restore instead of being overwritten
            var replace = exists ? ", REPLACE" : string.Empty;

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

    /// <summary>
    /// Takes the database offline, ending the sessions still using it, and drops it.
    /// </summary>
    /// <remarks>
    /// OFFLINE rather than SINGLE_USER: this connection is on master, so the single session SINGLE_USER leaves
    /// is free for an application's pool to take before the DROP runs, which then fails and leaves the database
    /// in single-user mode. Nothing can connect to an offline database. The price is that SQL Server keeps an
    /// offline database's files when it drops it — the restore overwrites them where it writes to the same paths
    /// (REPLACE). A database stuck RESTORING has no sessions and refuses ALTER, so it is dropped as it is.
    /// </remarks>
    private static Task Drop(IDbConnection cn, string databaseName)
        => cn.ExecuteAsync("""
            DECLARE @quoted nvarchar(258) = QUOTENAME(@databaseName);
            IF DATABASEPROPERTYEX(@databaseName, 'Status') = N'ONLINE'
                EXEC (N'ALTER DATABASE ' + @quoted + N' SET OFFLINE WITH ROLLBACK IMMEDIATE');
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
