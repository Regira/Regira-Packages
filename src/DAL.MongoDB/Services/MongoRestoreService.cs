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
            // the password goes into a file of its own, the URI carries everything else
            using var passwordFile = MongoPasswordFile.Create(settings);
            var args = BackupCommands.Restore
                .Inject(new
                {
                    Uri = settings.BuildConnectionString(includePassword: false),
                    SourcePath = sourcePath,
                    ConfigArgs = passwordFile?.ConfigArgument
                })!;

            // holds no password
            logger?.LogDebug("Restoring backup with {ProcessPath} {Arguments}", _restoreProcessPath, args);

            // execute restore process, capturing what the tool has to say: without it a failure reports an exit code and nothing else
            var output = processHelper.ExecuteFile(_restoreProcessPath, waitForOutput: true, arguments: args);

            if (output.ExitCode != 0)
            {
                // failed
                throw new Exception($"Restore failed (ExitCode {output.ExitCode}): {output.Error}");
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
