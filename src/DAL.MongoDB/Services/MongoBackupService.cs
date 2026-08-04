using Regira.DAL.Abstractions;
using Regira.DAL.MongoDB.Constants;
using Regira.DAL.MongoDB.Core;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.System.Abstractions;
using Regira.Utilities;

namespace Regira.DAL.MongoDB.Services;

public class MongoBackupService(MongoOptions options, IProcessHelper processHelper) : IDbBackupService
{
    private readonly string _backupProcessPath = Path.Combine(options.ToolsDirectory, "mongodump.exe");

    public Task<IMemoryFile> Backup()
    {
        var targetPath = Path.GetTempFileName();
        var settings = options.DbSettings ?? MongoSettings.FromConnectionString(options.ConnectionString ?? throw new ArgumentException("Connection data missing"));
        var cmd = BackupCommands.Backup
            .Inject(new
            {
                ProcessPath = _backupProcessPath,
                Uri = $"mongodb://{settings.Host}:{settings.Port}/{settings.DatabaseName}".TrimEnd('/'),
                TargetPath = targetPath
            })!;

        if (!string.IsNullOrEmpty(settings.Password))
        {
            // ToDo
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