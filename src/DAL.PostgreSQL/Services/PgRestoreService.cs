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

public class PgRestoreService(PgOptions options, IProcessHelper processHelper, ILogger<PgRestoreService>? logger = null) : IDbRestoreService
{
    private readonly string _restoreProcessPath = Path.Combine(options.ToolsDirectory, OperatingSystem.IsWindows() ? "pg_restore.exe" : "pg_restore");

    public async Task Restore(IMemoryFile file)
    {
        var settings = options.DbSettings ?? PgSettings.FromConnectionString(options.ConnectionString ?? throw new ArgumentException("Connection data missing"));
        var targetDb = settings.DatabaseName ?? throw new ArgumentException("Database name missing");

        // creating or dropping a database cannot happen from a connection to that same database
        var maintenanceSettings = new PgSettings(
            settings.Host,
            options.MaintenanceDatabase ?? PgDefaults.MaintenanceDatabase,
            settings.Username,
            settings.Password,
            settings.Port
        );

        await using var cn = new NpgsqlConnection(maintenanceSettings.BuildConnectionString());
        await cn.OpenAsync();

        var exists = await Exists(cn, targetDb);
        if (exists && !options.Overwrite)
        {
            throw new Exception($"Database {targetDb} already exists.");
        }

        // pg_restore opens the backup itself, so it must be written and closed before the process starts
        var sourcePath = Path.GetTempFileName();
        try
        {
            await file.SaveAs(sourcePath);

            // read the archive before the target database is touched: dropping it for a backup that turns out to be
            // unreadable would leave neither
            ValidateArchive(sourcePath);

            if (exists)
            {
                await Drop(cn, targetDb);
            }

            // create db
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

            logger?.LogDebug($"Restoring backup...{Environment.NewLine}{cmd}");

            // the password travels in the process environment, so it never reaches the generated script
            var environment = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(settings.Password))
            {
                environment["PGPASSWORD"] = settings.Password;
            }

            // execute restore process, capturing what pg_restore has to say: without it a failure reports an exit code and nothing else
            var output = processHelper.ExecuteCommand(cmd, environment, waitForOutput: true);

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
                logger?.LogWarning(ex, $"Could not delete temporary file {sourcePath}");
            }
        }
    }

    /// <summary>
    /// Reads the archive's table of contents (<c>pg_restore --list</c>), which reaches no server. A file that cannot
    /// be read here cannot be restored either, and finding that out first is what keeps a failed restore from
    /// costing the database it was meant to replace.
    /// </summary>
    /// <param name="sourcePath">Path of the backup-file to read</param>
    /// <exception cref="Exception">The file is not an archive <c>pg_restore</c> can read</exception>
    private void ValidateArchive(string sourcePath)
    {
        var cmd = BackupCommands.ListArchive
            .Inject(new
            {
                ProcessPath = _restoreProcessPath,
                SourcePath = sourcePath
            })!;

        var output = processHelper.ExecuteCommand(cmd, waitForOutput: true);

        if (output.ExitCode != 0)
        {
            throw new Exception($"Backup is not a readable archive (ExitCode {output.ExitCode}): {output.Error}");
        }
    }

    /// <summary>
    /// Checks whether a database exists.
    /// </summary>
    /// <param name="cn">Open connection to any database on the server</param>
    /// <param name="databaseName">Name of the database to look for</param>
    public Task<bool> Exists(IDbConnection cn, string databaseName)
        => cn.ExecuteScalarAsync<bool>(PgSql.DatabaseExists, new { databaseName });
    /// <summary>
    /// Creates a database.
    /// </summary>
    /// <param name="cn">Open connection to another database on the same server</param>
    /// <param name="databaseName">Name of the database to create</param>
    public Task Create(IDbConnection cn, string databaseName)
        => cn.ExecuteAsync(PgSql.CreateDatabase(databaseName));
    /// <summary>
    /// Drops a database if it exists. PostgreSQL refuses this while other sessions are connected to it.
    /// </summary>
    /// <param name="cn">Open connection to another database on the same server</param>
    /// <param name="databaseName">Name of the database to drop</param>
    public Task Drop(IDbConnection cn, string databaseName)
        => cn.ExecuteAsync(PgSql.DropDatabase(databaseName));
}
