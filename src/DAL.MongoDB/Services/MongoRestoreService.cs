using Regira.DAL.Abstractions;
using Regira.DAL.MongoDB.Constants;
using Regira.DAL.MongoDB.Core;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.System.Abstractions;
using Regira.Utilities;

namespace Regira.DAL.MongoDB.Services;

public class MongoRestoreService(MongoOptions options, IProcessHelper processHelper) : IDbRestoreService
{
    private readonly string _restoreProcessPath = Path.Combine(options.ToolsDirectory, "mongorestore.exe");
    public async Task Restore(IMemoryFile file)
    {
        var settings = options.DbSettings ?? MongoSettings.FromConnectionString(options.ConnectionString ?? throw new ArgumentException("Connection data missing"));

        var sourcePath = Path.GetTempFileName();

        var fs = File.OpenWrite(sourcePath);
        using var ms = file.GetStream()!;
        await ms.CopyToAsync(fs);

        // execute restoring tool
        var cmd = BackupCommands.Restore
            .Inject(new
            {
                ProcessPath = _restoreProcessPath,
                Uri = $"mongodb://{settings.Host}:{settings.Port}/{settings.DatabaseName}",
                SourcePath = sourcePath
            })!;

        if (!string.IsNullOrEmpty(settings.Password))
        {
            // ToDo
        }

        // execute restore process
        var output = processHelper.ExecuteCommand(cmd);


        if (output.ExitCode != 0)
        {
            // failed
            throw new Exception($"Restore failed (ExitCode {output.ExitCode}): {output.Error}");
        }
    }
}