using Microsoft.Extensions.Logging;
using Regira.DAL.Abstractions;
using Regira.DAL.PostgreSQL.Constants;
using Regira.DAL.PostgreSQL.Core;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.System.Abstractions;
using Regira.Utilities;

namespace Regira.DAL.PostgreSQL.Services;

public class PgBackupService(PgOptions options, IProcessHelper processHelper, ILogger<PgBackupService>? logger = null) : IDbBackupService
{
    private readonly string _backupProcessPath = Path.Combine(options.ToolsDirectory, OperatingSystem.IsWindows() ? "pg_dump.exe" : "pg_dump");

    public async Task<IMemoryFile> Backup()
    {
        var targetPath = Path.GetTempFileName();
        try
        {
            return await CreateDump(targetPath);
        }
        finally
        {
            try
            {
                File.Delete(targetPath);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, $"Could not delete temporary file {targetPath}");
            }
        }
    }

    private async Task<IMemoryFile> CreateDump(string targetPath)
    {
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

        logger?.LogDebug($"Creating backup...{Environment.NewLine}{cmd}");

        // the password travels in the process environment, so it never reaches the generated script
        var environment = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(settings.Password))
        {
            environment["PGPASSWORD"] = settings.Password;
        }
        // execute backup process, capturing what pg_dump has to say: without it a failure reports an exit code and nothing else
        var output = processHelper.ExecuteCommand(cmd, environment, waitForOutput: true);

        if (output.ExitCode != 0)
        {
            // failed
            throw new Exception($"Backup failed (ExitCode {output.ExitCode}): {output.Error}");
        }

        // read the dump into memory so the temporary file can be removed
        var bytes = await File.ReadAllBytesAsync(targetPath);
        return bytes.ToMemoryFile();
    }
}