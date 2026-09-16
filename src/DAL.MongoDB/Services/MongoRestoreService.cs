using Microsoft.Extensions.Logging;
using Regira.DAL.Abstractions;
using Regira.DAL.MongoDB.Constants;
using Regira.DAL.MongoDB.Core;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.System.Abstractions;
using Regira.Utilities;

namespace Regira.DAL.MongoDB.Services;

public class MongoRestoreService(MongoOptions options, IProcessHelper processHelper, ILogger<MongoRestoreService>? logger = null) : IDbRestoreService
{
    /// <summary>
    /// Keeps code compiled against the constructor without a logger running.
    /// </summary>
    public MongoRestoreService(MongoOptions options, IProcessHelper processHelper) : this(options, processHelper, null)
    {
    }

    private readonly string _restoreProcessPath = Path.Combine(options.ToolsDirectory, OperatingSystem.IsWindows() ? "mongorestore.exe" : "mongorestore");
    public async Task Restore(IMemoryFile file)
    {
        var settings = options.DbSettings ?? MongoSettings.FromConnectionString(options.ConnectionString ?? throw new ArgumentException("Connection data missing"));

        var sourcePath = Path.GetTempFileName();
        try
        {
            // close the archive before mongorestore opens it
            await using (var fs = File.OpenWrite(sourcePath))
            await using (var ms = file.GetStream()!)
            {
                await ms.CopyToAsync(fs);
            }

            // execute restoring tool
            // the connection travels in a file of its own: a URI can carry secrets besides the password
            using var configFile = MongoToolConfigFile.Create(settings);
            var args = BackupCommands.Restore
                .Inject(new
                {
                    SourcePath = sourcePath,
                    ConfigPath = configFile.FilePath
                })!;

            logger?.LogDebug("Restoring backup to {Uri} with {ProcessPath} {Arguments}", settings.BuildRedactedConnectionString(), _restoreProcessPath, args);

            // execute restore process, capturing what the tool has to say: without it a failure reports an exit code and nothing else
            var output = processHelper.ExecuteFile(_restoreProcessPath, waitForOutput: true, arguments: args);

            if (output.ExitCode != 0)
            {
                // failed
                throw new Exception($"Restore failed (ExitCode {output.ExitCode}): {ToolOutput.Tail(output.Error)}");
            }
        }
        finally
        {
            try
            {
                File.Delete(sourcePath);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Could not delete temporary file {Path}", sourcePath);
            }
        }
    }
}
