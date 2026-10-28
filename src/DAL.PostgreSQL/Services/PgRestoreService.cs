using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Regira.DAL.Abstractions;
using Regira.DAL.PostgreSQL.Core;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.System.Abstractions;

namespace Regira.DAL.PostgreSQL.Services;

public class PgRestoreService(PgOptions options, IProcessHelper processHelper, ILogger<PgRestoreService>? logger = null) : IDbRestoreService
{
    private readonly string _restoreProcessPath = PgTools.RestorePath(options.ToolsDirectory);

    public async Task Restore(IMemoryFile file)
    {
        var settings = options.DbSettings ?? PgSettings.FromConnectionString(options.ConnectionString ?? throw new ArgumentException("Connection data missing"));
        var targetDb = settings.DatabaseName ?? throw new ArgumentException("Database name missing");

        // pg_restore opens the backup itself, so it must be written and closed before the process starts
        var sourcePath = Path.GetTempFileName();
        try
        {
            await file.SaveAs(sourcePath);
            await RestoreFrom(processHelper, _restoreProcessPath, settings, targetDb, sourcePath, options.MaintenanceDatabase, options.Overwrite, logger);
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

    /// <summary>
    /// Restores the backup at <paramref name="sourcePath"/> as <paramref name="targetDb"/>, creating it, or replacing
    /// it with <paramref name="overwrite"/>, from a connection to the maintenance database; <c>pg_restore</c> gets the
    /// password in its environment. <see cref="BackupRestoreManager"/> restores through it too.
    /// <para>
    /// Replacing needs PostgreSQL 13 or later and a role that may create databases and end the sessions on the target:
    /// it ends them (<c>DROP DATABASE … WITH (FORCE)</c>) and clears the Npgsql pools of connections opened from a
    /// connection string in this process, so an app can restore its own database while it runs. A query running on
    /// the target at that moment fails, and a pool this does not reach (an app's own <c>NpgsqlDataSource</c>, another
    /// process) fails its next query once, unless its <c>Clear()</c> is called after the restore.
    /// </para>
    /// </summary>
    /// <exception cref="Exception">The database exists without <paramref name="overwrite"/>, the role may not create it again, the backup cannot be read, or the tool fails</exception>
    internal static async Task RestoreFrom(IProcessHelper processHelper, string restorePath, PgSettings settings, string targetDb, string sourcePath,
        string? maintenanceDatabase, bool overwrite, ILogger? logger)
    {
        // creating or dropping a database cannot happen from a connection to that same database
        var maintenanceSettings = new PgSettings(
            settings.Host,
            maintenanceDatabase ?? PgDefaults.MaintenanceDatabase,
            settings.Username,
            settings.Password,
            settings.Port
        );

        await using var cn = new NpgsqlConnection(PgTools.MaintenanceConnectionString(maintenanceSettings));
        await cn.OpenAsync();

        var exists = await cn.ExecuteScalarAsync<bool>(PgSql.DatabaseExists, new { databaseName = targetDb });
        if (exists && !overwrite)
        {
            throw new Exception($"Database {targetDb} already exists.");
        }

        // read the archive before the target database is touched: dropping it for a backup that turns out to be
        // unreadable would leave neither
        ValidateArchive(processHelper, restorePath, sourcePath);

        if (exists)
        {
            // an owner without CREATEDB may drop its database but not create it again, which would leave neither
            if (!await cn.ExecuteScalarAsync<bool>(PgSql.CanCreateDatabase))
            {
                throw new Exception($"Database {targetDb} is not replaced: the role {settings.Username} may not create databases (CREATEDB).");
            }
            // an app restoring its own database always has sessions on it, its own pool among them: end them, then
            // drop the pooled connections of this process that the server has just closed
            await cn.ExecuteAsync(PgSql.DropDatabase(targetDb, force: true));
            NpgsqlConnection.ClearAllPools();
        }
        await cn.ExecuteAsync(PgSql.CreateDatabase(targetDb));

        var args = PgTools.RestoreArguments(settings, targetDb, sourcePath);

        // holds no password
        logger?.LogDebug("Restoring backup with {ProcessPath} {Arguments}", restorePath, args);

        // capturing what pg_restore has to say: without it a failure reports an exit code and nothing else
        var output = processHelper.ExecuteFile(restorePath, PgTools.Environment(settings), waitForOutput: true, arguments: args);

        if (output.ExitCode != 0)
        {
            throw new Exception($"Restore failed (ExitCode {output.ExitCode}): {PgTools.Tail(output.Error)}");
        }
    }

    /// <summary>
    /// Reads the archive's table of contents (<c>pg_restore --list</c>), which reaches no server. A file that cannot
    /// be read here cannot be restored either, and finding that out first is what keeps a failed restore from
    /// costing the database it was meant to replace.
    /// </summary>
    /// <exception cref="Exception">The file is not an archive <c>pg_restore</c> can read</exception>
    private static void ValidateArchive(IProcessHelper processHelper, string restorePath, string sourcePath)
    {
        var output = processHelper.ExecuteFile(restorePath, waitForOutput: true, arguments: PgTools.ListArchiveArguments(sourcePath));

        if (output.ExitCode != 0)
        {
            throw new Exception($"Backup is not a readable archive (ExitCode {output.ExitCode}): {PgTools.Tail(output.Error)}");
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
