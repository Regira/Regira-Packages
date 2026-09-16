using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Regira.DAL.PostgreSQL.Core;
using Regira.System.Abstractions;

namespace Regira.DAL.PostgreSQL.Services;

public class BackupRestoreManager
{
    private readonly IProcessHelper _processHelper;
    private readonly ILogger<BackupRestoreManager>? _logger;
    private readonly string _backupProcessPath;
    private readonly string _restoreProcessPath;
    private readonly string _maintenanceDatabase;

    /// <summary>
    /// Manager for backing up and restoring a PostgreSQL Database
    /// </summary>
    /// <param name="processHelper">Helper class to start the backing up or restore process</param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public BackupRestoreManager(IProcessHelper processHelper, PgOptions options, ILogger<BackupRestoreManager>? logger = null)
    {
        _processHelper = processHelper;
        _logger = logger;
        _maintenanceDatabase = options.MaintenanceDatabase ?? PgDefaults.MaintenanceDatabase;

        if (string.IsNullOrEmpty(options.ToolsDirectory))
        {
            throw new ArgumentNullException(nameof(options.ToolsDirectory));
        }

        if (!Directory.Exists(options.ToolsDirectory))
        {
            throw new DirectoryNotFoundException(options.ToolsDirectory);
        }

        _backupProcessPath = PgTools.DumpPath(options.ToolsDirectory);
        _restoreProcessPath = PgTools.RestorePath(options.ToolsDirectory);
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="settings">Database configuration</param>
    /// <param name="sourceDb">Name of source Database</param>
    /// <param name="targetPath">Path of the backup-file to create</param>
    /// <param name="schemas"></param>
    /// <exception cref="Exception"></exception>
    public void Backup(PgSettings settings, string sourceDb, string targetPath, IList<string>? schemas = null)
    {
        var args = PgTools.BackupArguments(settings, sourceDb, targetPath, schemas);

        // create directory
        var backupDir = Path.GetDirectoryName(targetPath)!;
        // ReSharper disable once AssignNullToNotNullAttribute
        Directory.CreateDirectory(backupDir);

        // holds no password
        _logger?.LogDebug("Creating backup with {ProcessPath} {Arguments}", _backupProcessPath, args);

        // execute backup process, capturing what pg_dump has to say: without it a failure reports an exit code and nothing else
        var output = _processHelper.ExecuteFile(_backupProcessPath, PgTools.Environment(settings), waitForOutput: true, arguments: args);

        if (output.ExitCode != 0)
        {
            // failed
            throw new Exception($"Backup failed (ExitCode {output.ExitCode}): {PgTools.Tail(output.Error)}");
        }
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="settings">Database configuration</param>
    /// <param name="targetDb">Name of target Database</param>
    /// <param name="sourcePath">Path of backup-file to restore</param>
    /// <param name="overwrite">Replaces the target database when it already exists: it is dropped and recreated,
    /// so anything the backup does not contain is lost. Without it, restoring onto an existing database fails.</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task Restore(PgSettings settings, string targetDb, string sourcePath, bool overwrite = false)
    {
        // creating or dropping a database cannot happen from a connection to that same database
        var maintenanceSettings = new PgSettings(
            settings.Host,
            _maintenanceDatabase,
            settings.Username,
            settings.Password,
            settings.Port
        );

        await using var cn = new NpgsqlConnection(PgTools.MaintenanceConnectionString(maintenanceSettings));
        await cn.OpenAsync();

        var exists = await Exists(cn, targetDb);
        if (exists && !overwrite)
        {
            throw new Exception($"Database {targetDb} already exists.");
        }

        // read the archive before the target database is touched: dropping it for a backup that turns out to be
        // unreadable would leave neither
        ValidateArchive(sourcePath);

        if (exists)
        {
            await Drop(cn, targetDb);
        }

        // create db
        await Create(cn, targetDb);

        // execute restoring tool
        var args = PgTools.RestoreArguments(settings, targetDb, sourcePath);

        // holds no password
        _logger?.LogDebug("Restoring backup with {ProcessPath} {Arguments}", _restoreProcessPath, args);

        // execute restore process, capturing what pg_restore has to say: without it a failure reports an exit code and nothing else
        var output = _processHelper.ExecuteFile(_restoreProcessPath, PgTools.Environment(settings), waitForOutput: true, arguments: args);

        if (output.ExitCode != 0)
        {
            // failed
            throw new Exception($"Restore failed (ExitCode {output.ExitCode}): {PgTools.Tail(output.Error)}");
        }
    }

    /// <summary>
    /// Reads the archive's table of contents (<c>pg_restore --list</c>), which reaches no server. A file that cannot
    /// be read here cannot be restored either, and finding that out first is what keeps a failed restore from
    /// costing the database it was meant to replace.
    /// </summary>
    /// <param name="sourcePath">Path of the backup-file to read</param>
    /// <exception cref="Exception">The file is not an archive <c>pg_restore</c> can read</exception>
    private void ValidateArchive(string sourcePath)
    {
        var output = _processHelper.ExecuteFile(_restoreProcessPath, waitForOutput: true, arguments: PgTools.ListArchiveArguments(sourcePath));

        if (output.ExitCode != 0)
        {
            throw new Exception($"Backup is not a readable archive (ExitCode {output.ExitCode}): {PgTools.Tail(output.Error)}");
        }
    }

    /// <summary>
    /// Checks whether a database exists.
    /// </summary>
    /// <param name="cn">Open connection to any database on the server</param>
    /// <param name="databaseName">Name of the database to look for</param>
    public Task<bool> Exists(IDbConnection cn, string databaseName)
        => cn.ExecuteScalarAsync<bool>(PgSql.DatabaseExists, new { databaseName });
    /// <summary>
    /// Creates a database.
    /// </summary>
    /// <param name="cn">Open connection to another database on the same server</param>
    /// <param name="databaseName">Name of the database to create</param>
    public Task Create(IDbConnection cn, string databaseName)
        => cn.ExecuteAsync(PgSql.CreateDatabase(databaseName));
    /// <summary>
    /// Drops a database if it exists. PostgreSQL refuses this while other sessions are connected to it.
    /// </summary>
    /// <param name="cn">Open connection to another database on the same server</param>
    /// <param name="databaseName">Name of the database to drop</param>
    public Task Drop(IDbConnection cn, string databaseName)
        => cn.ExecuteAsync(PgSql.DropDatabase(databaseName));
}