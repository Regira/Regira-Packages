using Microsoft.Extensions.Logging;
using Regira.DAL.Abstractions;
using Regira.DAL.PostgreSQL.Constants;
using Regira.DAL.PostgreSQL.Core;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.System.Abstractions;
using Regira.Utilities;

namespace Regira.DAL.PostgreSQL.Services;

public class PgBackupService(PgOptions options, IProcessHelper processHelper, ILogger<PgBackupService>? logger) : IDbBackupService
{
    private readonly string _backupProcessPath = Path.Combine(options.ToolsDirectory, "pg_dump.exe");

    public Task<IMemoryFile> Backup()
    {
        var targetPath = Path.GetTempFileName();
        var settings = options.DbSettings ?? PgSettings.FromConnectionString(options.ConnectionString ?? throw new ArgumentException("Connection data missing"));
        // compose command with args
        var schemasArgs = options.BackupSchemas?.Any() ?? false ? string.Join(" ", options.BackupSchemas.Select(x => $"--schema \"{x}\"")) : null;
        var cmd = (options.BackupSchemas?.Any() ?? false ? BackupCommands.SchemaBackup : BackupCommands.FullBackup)
            .Inject(new
            {
                ProcessPath = _backupProcessPath,
                settings.Host,
                settings.Port,
                settings.Username,
                TargetPath = targetPath,
                SchemasArgs = schemasArgs,
                SourceDb = settings.DatabaseName
            })!;

        // log without password
        logger?.LogDebug($"Creating backup...{Environment.NewLine}{cmd}");

        if (!string.IsNullOrEmpty(settings.Password))
        {
            cmd = $@"set PGPASSWORD={settings.Password}
{cmd}
set PGPASSWORD=";
        }
        // execute backup process
        var output = processHelper.ExecuteCommand(cmd);

        if (output.ExitCode != 0)
        {
            // failed
            throw new Exception($"Backup failed (ExitCode {output.ExitCode}): {output.Error}");
        }

        var fs = File.OpenRead(targetPath);
        var file = fs.ToMemoryFile();
        return Task.FromResult(file);
    }
}