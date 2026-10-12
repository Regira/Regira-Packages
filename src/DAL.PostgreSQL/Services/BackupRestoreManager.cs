using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
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
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        PgBackupService.DumpTo(_processHelper, _backupProcessPath, settings, sourceDb, targetPath, schemas, _logger);
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
    public Task Restore(PgSettings settings, string targetDb, string sourcePath, bool overwrite = false)
        => PgRestoreService.RestoreFrom(_processHelper, _restoreProcessPath, settings, targetDb, sourcePath, _maintenanceDatabase, overwrite, _logger);

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