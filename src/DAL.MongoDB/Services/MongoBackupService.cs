using Microsoft.Extensions.Logging;
using Regira.DAL.Abstractions;
using Regira.DAL.MongoDB.Constants;
using Regira.DAL.MongoDB.Core;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.System.Abstractions;
using Regira.Utilities;

namespace Regira.DAL.MongoDB.Services;

public class MongoBackupService(MongoOptions options, IProcessHelper processHelper, ILogger<MongoBackupService>? logger = null) : IDbBackupService
{
    /// <summary>
    /// Keeps code compiled against the constructor without a logger running.
    /// </summary>
    public MongoBackupService(MongoOptions options, IProcessHelper processHelper) : this(options, processHelper, null)
    {
    }

    private readonly string _backupProcessPath = Path.Combine(options.ToolsDirectory, OperatingSystem.IsWindows() ? "mongodump.exe" : "mongodump");

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
                logger?.LogWarning(ex, "Could not delete temporary file {Path}", targetPath);
            }
        }
    }

    private async Task<IMemoryFile> CreateDump(string targetPath)
    {
        var settings = options.DbSettings ?? MongoSettings.FromConnectionString(options.ConnectionString ?? throw new ArgumentException("Connection data missing"));

        // the connection travels in a file of its own: a URI can carry secrets besides the password
        using var configFile = MongoToolConfigFile.Create(settings);
        var args = BackupCommands.Backup
            .Inject(new
            {
                TargetPath = targetPath,
                ConfigPath = configFile.FilePath
            })!;

        logger?.LogDebug("Creating backup of {Uri} with {ProcessPath} {Arguments}", settings.BuildRedactedConnectionString(), _backupProcessPath, args);

        // execute backup process, capturing what the tool has to say: without it a failure reports an exit code and nothing else
        var output = processHelper.ExecuteFile(_backupProcessPath, waitForOutput: true, arguments: args);

        if (output.ExitCode != 0)
        {
            // failed
            throw new Exception($"Backup failed (ExitCode {output.ExitCode}): {ToolOutput.Tail(output.Error)}");
        }

        // read the dump into memory so the temporary file can be removed
        var bytes = await File.ReadAllBytesAsync(targetPath);
        return bytes.ToMemoryFile();
    }
}
