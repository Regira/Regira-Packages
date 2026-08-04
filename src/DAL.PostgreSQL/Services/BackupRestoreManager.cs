using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Regira.DAL.PostgreSQL.Constants;
using Regira.DAL.PostgreSQL.Core;
using Regira.System.Abstractions;
using Regira.Utilities;

namespace Regira.DAL.PostgreSQL.Services;

public class BackupRestoreManager
{

    private const string BackupProcessFile = "pg_dump.exe";
    private const string RestoreProcessFile = "pg_restore.exe";
    private readonly IProcessHelper _processHelper;
    private readonly ILogger<BackupRestoreManager>? _logger;
    private readonly string _backupProcessPath;
    private readonly string _restoreProcessPath;

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

        if (string.IsNullOrEmpty(options.ToolsDirectory))
        {
            throw new ArgumentNullException(nameof(options.ToolsDirectory));
        }

        if (!Directory.Exists(options.ToolsDirectory))
        {
            throw new DirectoryNotFoundException(options.ToolsDirectory);
        }

        _backupProcessPath = Path.Combine(options.ToolsDirectory, BackupProcessFile);
        _restoreProcessPath = Path.Combine(options.ToolsDirectory, RestoreProcessFile);
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
        // compose command with args
        var schemasArgs = schemas?.Any() ?? false ? string.Join(" ", schemas.Select(x => $"--schema \"{x}\"")) : null;
        var cmd = (schemas?.Any() ?? false ? BackupCommands.SchemaBackup : BackupCommands.FullBackup)
            .Inject(new
            {
                ProcessPath = _backupProcessPath,
                settings.Host,
                settings.Port,
                settings.Username,
                TargetPath = targetPath,
                SchemasArgs = schemasArgs,
                SourceDb = sourceDb
            })!;

        // create directory
        var backupDir = Path.GetDirectoryName(targetPath)!;
        // ReSharper disable once AssignNullToNotNullAttribute
        Directory.CreateDirectory(backupDir);

        // log without password
        _logger?.LogDebug($"Creating backup...{Environment.NewLine}{cmd}");

        if (!string.IsNullOrEmpty(settings.Password))
        {
            cmd = $@"set PGPASSWORD={settings.Password}
{cmd}
set PGPASSWORD=";
        }
        // execute backup process
        var output = _processHelper.ExecuteCommand(cmd);

        if (output.ExitCode != 0)
        {
            // failed
            throw new Exception($"Backup failed (ExitCode {output.ExitCode}): {output.Error}");
        }
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="settings">Database configuration</param>
    /// <param name="targetDb">Name of target Database</param>
    /// <param name="sourcePath">Path of backup-file to restore</param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task Restore(PgSettings settings, string targetDb, string sourcePath, bool overwrite = false)
    {
        var cn = new NpgsqlConnection(settings.BuildConnectionString());
        await cn.OpenAsync();

        if (!overwrite && await Exists(cn, targetDb))
        {
            throw new Exception($"Database {targetDb} already exists.");
        }

        // create db & add postgis extension
        await Create(cn, targetDb);

        // execute restoring tool
        var cmd = BackupCommands.Restore
            .Inject(new
            {
                ProcessPath = _restoreProcessPath,
                settings.Host,
                settings.Port,
                settings.Username,
                TargetDb = targetDb,
                SourcePath = sourcePath
            })!;

        // log without password
        _logger?.LogDebug($"Restoring backup...{Environment.NewLine}{cmd}");

        if (!string.IsNullOrEmpty(settings.Password))
        {
            cmd = $@"set PGPASSWORD={settings.Password}
{cmd}
set PGPASSWORD=";
        }

        // execute restore process
        var output = _processHelper.ExecuteCommand(cmd);

        if (output.ExitCode != 0)
        {
            // failed
            throw new Exception($"Restore failed (ExitCode {output.ExitCode}): {output.Error}");
        }
    }

    public Task<bool> Exists(IDbConnection cn, string databaseName)
    {
        var sql = $@"SELECT EXISTS (SELECT NULL FROM pg_catalog.pg_database WHERE datname = @{databaseName});";
        return cn.ExecuteScalarAsync<bool>(sql, new { databaseName });
    }
    public Task Create(IDbConnection cn, string databaseName)
    {
        var sql = $@"CREATE DATABASE {databaseName} WITH TABLESPACE = pg_default;";
        return cn.ExecuteScalarAsync<bool>(sql, new { databaseName });
    }
}