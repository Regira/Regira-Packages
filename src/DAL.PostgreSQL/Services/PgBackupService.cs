using Microsoft.Extensions.Logging;
using Regira.DAL.Abstractions;
using Regira.DAL.PostgreSQL.Core;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.System.Abstractions;

namespace Regira.DAL.PostgreSQL.Services;

public class PgBackupService(PgOptions options, IProcessHelper processHelper, ILogger<PgBackupService>? logger = null) : IDbBackupService
{
    private readonly string _backupProcessPath = PgTools.DumpPath(options.ToolsDirectory);

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
        var args = PgTools.BackupArguments(settings, settings.DatabaseName, targetPath, options.BackupSchemas);

        // holds no password
        logger?.LogDebug("Creating backup with {ProcessPath} {Arguments}", _backupProcessPath, args);

        // execute backup process, capturing what pg_dump has to say: without it a failure reports an exit code and nothing else
        var output = processHelper.ExecuteFile(_backupProcessPath, PgTools.Environment(settings), waitForOutput: true, arguments: args);

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