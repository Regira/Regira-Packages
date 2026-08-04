using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Regira.DAL.Abstractions;
using Regira.DAL.PostgreSQL.Constants;
using Regira.DAL.PostgreSQL.Core;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.System.Abstractions;
using Regira.Utilities;

namespace Regira.DAL.PostgreSQL.Services;

public class PgRestoreService(PgOptions options, IProcessHelper processHelper, ILogger<PgBackupService>? logger) : IDbRestoreService
{
    private readonly string _restoreProcessPath = Path.Combine(options.ToolsDirectory, "pg_restore.exe");

    public async Task Restore(IMemoryFile file)
    {
        var settings = options.DbSettings ?? PgSettings.FromConnectionString(options.ConnectionString ?? throw new ArgumentException("Connection data missing"));
        var cn = new NpgsqlConnection(settings.BuildConnectionString());
        await cn.OpenAsync();

        var targetDb = settings.DatabaseName!;
        if (!options.Overwrite && await Exists(cn, targetDb))
        {
            throw new Exception($"Database {targetDb} already exists.");
        }

        // create db & add postgis extension
        await Create(cn, targetDb);

        var sourcePath = Path.GetTempFileName();
        var fs = File.OpenWrite(sourcePath);
        using var ms = file.GetStream()!;
        await ms.CopyToAsync(fs);

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
        logger?.LogDebug($"Restoring backup...{Environment.NewLine}{cmd}");

        if (!string.IsNullOrEmpty(settings.Password))
        {
            cmd = $@"set PGPASSWORD={settings.Password}
{cmd}
set PGPASSWORD=";
        }

        // execute restore process
        var output = processHelper.ExecuteCommand(cmd);

        if (output.ExitCode != 0)
        {
            // failed
            throw new Exception($"Restore failed (ExitCode {output.ExitCode}): {output.Error}");
        }
    }

    public Task<bool> Exists(IDbConnection cn, string databaseName)
    {
        var sql = $"SELECT EXISTS (SELECT NULL FROM pg_catalog.pg_database WHERE datname = @{databaseName});";
        return cn.ExecuteScalarAsync<bool>(sql, new { databaseName });
    }
    public Task Create(IDbConnection cn, string databaseName)
    {
        var sql = $"CREATE DATABASE {databaseName} WITH TABLESPACE = pg_default;";
        return cn.ExecuteScalarAsync<bool>(sql, new { databaseName });
    }
}