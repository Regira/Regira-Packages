using System.Diagnostics;
using System.Transactions;
using Dapper;
using Microsoft.Data.SqlClient;
using Regira.DAL.SqlServer.Core;
using Regira.DAL.SqlServer.Services;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;

namespace DAL.SqlServer.Testing;

/// <summary>
/// Backs up and restores real databases, so it is skipped unless <c>REGIRA_SQLSERVER_CONNECTION</c> holds a connection
/// string for a login that may create databases. LocalDB will do:
/// <c>Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true</c>.
/// The backup directory defaults to a new temp directory, which only a server running under this user's account
/// (LocalDB) can reach — set <c>REGIRA_SQLSERVER_BACKUP_DIRECTORY</c> for any other server.
/// Every database created here is named <c>RegiraBackupTest_*</c> and dropped afterwards.
/// </summary>
[Category("LocalDb")]
public class BackupRestoreTests
{
    private const string ConnectionVariable = "REGIRA_SQLSERVER_CONNECTION";
    private const string BackupDirectoryVariable = "REGIRA_SQLSERVER_BACKUP_DIRECTORY";

    private readonly string _sourceDb = $"RegiraBackupTest_{Guid.NewGuid():N}"[..25];
    private readonly List<string> _databases = [];
    private string _connectionString = null!;
    private string _backupDirectory = null!;
    private bool _ownsBackupDirectory;
    private IMemoryFile _backup = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Ignore($"Skipped: set {ConnectionVariable} to a SQL Server connection string to run the backup/restore tests.");
        }
        _connectionString = connectionString;

        if (LocalDbInstanceName() != null && !LocalDbInstalled())
        {
            // the repository's runsettings point here by default, and not every machine has LocalDB
            Assert.Ignore($"Skipped: {ConnectionVariable} names a LocalDB instance, and LocalDB is not installed. Point it at another SQL Server to run the backup/restore tests.");
        }

        var backupDirectory = Environment.GetEnvironmentVariable(BackupDirectoryVariable);
        _ownsBackupDirectory = string.IsNullOrWhiteSpace(backupDirectory);
        _backupDirectory = _ownsBackupDirectory ? Directory.CreateTempSubdirectory("regira-sqlserver-").FullName : backupDirectory!;

        await WarmUpServer();

        _databases.Add(_sourceDb);
        await Execute("master", $"CREATE DATABASE [{_sourceDb}]");
        await Execute(_sourceDb, """
            CREATE TABLE dbo.Products (Id int IDENTITY PRIMARY KEY, Title nvarchar(100) NOT NULL);
            INSERT dbo.Products (Title) VALUES (N'Apple'), (N'Pear'), (N'Plum');
            """);

        _backup = await new SqlServerBackupService(Options(_sourceDb)).Backup();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_databases.Count > 0)
        {
            // the services' pooled connections still hold sessions on the source database
            SqlConnection.ClearAllPools();
            foreach (var database in _databases)
            {
                await Execute("master", """
                    IF DB_ID(@database) IS NOT NULL
                    BEGIN
                        DECLARE @quoted nvarchar(258) = QUOTENAME(@database);
                        IF DATABASEPROPERTYEX(@database, 'Status') = N'ONLINE'
                            EXEC (N'ALTER DATABASE ' + @quoted + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE');
                        EXEC (N'DROP DATABASE ' + @quoted);
                    END
                    """, new { database });
            }
        }
        if (_ownsBackupDirectory && Directory.Exists(_backupDirectory))
        {
            Directory.Delete(_backupDirectory, recursive: true);
        }
    }


    [Test]
    public async Task Restores_A_Copy_Beside_Its_Source()
    {
        var copy = NewDatabaseName();

        await new SqlServerRestoreService(Options(copy)).Restore(_backup);

        var copyCount = await CountProducts(copy);
        var sourceCount = await CountProducts(_sourceDb);
        var files = await PhysicalFileNames(copy);
        Assert.Multiple(() =>
        {
            Assert.That(copyCount, Is.EqualTo(3));
            Assert.That(sourceCount, Is.EqualTo(3), "the source database must be left as it was");
            Assert.That(files, Is.EquivalentTo(new[] { $"{copy}.mdf", $"{copy}_log.ldf" }));
        });
    }

    [Test]
    public async Task Refuses_An_Existing_Database_Without_Overwrite()
    {
        var copy = NewDatabaseName();
        await new SqlServerRestoreService(Options(copy)).Restore(_backup);
        await Execute(copy, "INSERT dbo.Products (Title) VALUES (N'Quince')");

        Assert.ThrowsAsync<InvalidOperationException>(() => new SqlServerRestoreService(Options(copy)).Restore(_backup));
        Assert.That(await CountProducts(copy), Is.EqualTo(4));
    }

    [Test]
    public async Task Overwrite_Replaces_A_Database_That_Is_Still_In_Use()
    {
        var copy = NewDatabaseName();
        await new SqlServerRestoreService(Options(copy)).Restore(_backup);
        await Execute(copy, "INSERT dbo.Products (Title) VALUES (N'Quince')");
        // a session still using the database, like a running application's
        await using var session = await Open(copy);

        await new SqlServerRestoreService(Options(copy, overwrite: true)).Restore(_backup);

        Assert.That(await CountProducts(copy), Is.EqualTo(3));
    }

    [Test]
    public async Task Overwrite_Keeps_The_Existing_Database_When_The_File_Is_No_Backup()
    {
        var copy = NewDatabaseName();
        await new SqlServerRestoreService(Options(copy)).Restore(_backup);
        var notABackup = "not a backup"u8.ToArray().ToMemoryFile();

        Assert.ThrowsAsync<SqlException>(() => new SqlServerRestoreService(Options(copy, overwrite: true)).Restore(notABackup));
        Assert.That(await CountProducts(copy), Is.EqualTo(3));
    }

    [Test]
    public async Task Runs_Inside_An_Ambient_Transaction()
    {
        var copy = NewDatabaseName();

        using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            var backup = await new SqlServerBackupService(Options(_sourceDb)).Backup();
            await new SqlServerRestoreService(Options(copy)).Restore(backup);
            // not completed: anything that joined the transaction is rolled back
        }

        Assert.That(await CountProducts(copy), Is.EqualTo(3));
    }

    [Test]
    public async Task Leaves_No_File_In_The_Backup_Directory()
    {
        var copy = NewDatabaseName();

        var backup = await new SqlServerBackupService(Options(_sourceDb)).Backup();
        await new SqlServerRestoreService(Options(copy)).Restore(backup);

        Assert.That(Directory.GetFiles(_backupDirectory, $"{_sourceDb}*"), Is.Empty);
    }


    /// <summary>
    /// Replaces a database while an application keeps reconnecting to it. A reconnect that lands between ending the
    /// sessions and the drop would take the database and fail the drop; that window sits inside one batch and has
    /// not been hit on LocalDB, so this guards the restore under that load rather than reproducing the race.
    /// </summary>
    [Test]
    public async Task Overwrite_Replaces_A_Database_An_Application_Keeps_Reconnecting_To()
    {
        var copy = NewDatabaseName();
        await new SqlServerRestoreService(Options(copy)).Restore(_backup);
        await Execute(copy, "INSERT dbo.Products (Title) VALUES (N'Quince')");

        // an application's pool, reconnecting the moment a session is refused or killed
        using var stop = new CancellationTokenSource();
        var pooled = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = copy, ConnectTimeout = 1 }.ConnectionString;
        var application = Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    await using var cn = new SqlConnection(pooled);
                    await cn.OpenAsync(stop.Token);
                    await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Products");
                }
                catch (Exception ex) when (ex is SqlException or OperationCanceledException or InvalidOperationException)
                {
                    // refused while the database is being replaced
                }
            }
        })).ToArray();

        try
        {
            await new SqlServerRestoreService(Options(copy, overwrite: true)).Restore(_backup);
        }
        finally
        {
            await stop.CancelAsync();
            await Task.WhenAll(application);
            SqlConnection.ClearPool(new SqlConnection(pooled));
        }

        Assert.That(await CountProducts(copy), Is.EqualTo(3));
    }

    [Test]
    public async Task Refuses_To_Write_Over_The_Files_Of_An_Offline_Database()
    {
        var copy = NewDatabaseName();
        // an offline database whose files carry the names the restore gives the copy's — a rename leaves exactly that
        var owner = NewDatabaseName();
        await using (var cn = await Open("master"))
        {
            var (dataDirectory, logDirectory) = await cn.QuerySingleAsync<(string, string)>(
                "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)), CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000))");
            await cn.ExecuteAsync($"""
                CREATE DATABASE [{owner}]
                    ON (NAME = N'{owner}', FILENAME = N'{Path.Combine(dataDirectory, copy + ".mdf")}')
                    LOG ON (NAME = N'{owner}_log', FILENAME = N'{Path.Combine(logDirectory, copy + "_log.ldf")}')
                """);
        }
        await Execute(owner, "CREATE TABLE dbo.Kept (Id int); INSERT dbo.Kept VALUES (1);");
        await Execute("master", $"ALTER DATABASE [{owner}] SET OFFLINE WITH ROLLBACK IMMEDIATE");

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => new SqlServerRestoreService(Options(copy, overwrite: true)).Restore(_backup));

        await Execute("master", $"ALTER DATABASE [{owner}] SET ONLINE");
        await using var check = await Open(owner);
        var kept = await check.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Kept");
        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain(owner));
            Assert.That(kept, Is.EqualTo(1), "the other database's data must be untouched");
        });
    }

    [Test]
    public async Task A_Backup_This_Process_Cannot_Read_Is_Removed_From_The_Server()
    {
        var options = Options(_sourceDb);
        // the misconfiguration the error message is written for
        options.LocalBackupDirectory = Path.Combine(_backupDirectory, "not-where-sql-server-writes");
        var logger = new WarningLogger<SqlServerBackupService>();

        Assert.ThrowsAsync<IOException>(() => new SqlServerBackupService(options, logger).Backup());

        Assert.Multiple(() =>
        {
            Assert.That(Directory.GetFiles(_backupDirectory, $"{_sourceDb}*"), Is.Empty);
            // the service read the deletion back and found the file gone
            Assert.That(logger.Warnings, Is.Empty);
        });
    }

    private sealed class WarningLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<string> Warnings { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= Microsoft.Extensions.Logging.LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }


    private SqlServerOptions Options(string database, bool overwrite = false) => new()
    {
        ConnectionString = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = database }.ConnectionString,
        BackupDirectory = _backupDirectory,
        Overwrite = overwrite
    };

    private string NewDatabaseName()
    {
        var name = $"{_sourceDb}_{_databases.Count}";
        _databases.Add(name);
        return name;
    }

    private const string LocalDbPrefix = "(localdb)" + "\\";

    /// <summary>
    /// Opens the first connection of the run, and recovers the instance once if that fails.
    /// <para>
    /// LocalDB starts its instance on the first connection, and that start can fail outright with
    /// "SQL Server process failed to start" when a previous run left a half-started instance behind.
    /// The instance is then wedged: it stays wedged for every later connection, so retrying the
    /// connection alone recovers nothing and only makes the failure take longer. Stopping it with
    /// <c>-k</c> and starting it again is what clears it, so that is what this does — once, and only
    /// for a connection string that actually names a LocalDB instance, since it would otherwise be
    /// stopping a server someone else owns.
    /// </para>
    /// </summary>
    private async Task WarmUpServer()
    {
        var instance = LocalDbInstanceName();
        try
        {
            await using var cn = await Open("master");
            return;
        }
        catch (SqlException) when (instance is not null)
        {
            RestartLocalDb(instance);
        }

        // Whatever the restart did, the connection is the verdict: if the instance is still unreachable
        // the original kind of SqlException surfaces here and the run fails on it.
        await using var retried = await Open("master");
    }

    /// <summary>The instance name when this run targets LocalDB, otherwise null.</summary>
    private string? LocalDbInstanceName()
    {
        var dataSource = new SqlConnectionStringBuilder(_connectionString).DataSource;
        return dataSource.StartsWith(LocalDbPrefix, StringComparison.OrdinalIgnoreCase)
            ? dataSource[LocalDbPrefix.Length..]
            : null;
    }

    /// <summary>Whether the <c>sqllocaldb</c> tool that comes with every LocalDB installation can be started.</summary>
    private static bool LocalDbInstalled()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("sqllocaldb", "versions")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            });
            process?.StandardOutput.ReadToEnd();
            process?.WaitForExit(milliseconds: 60_000);
            return process is { ExitCode: 0 };
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Best effort: a missing or failing sqllocaldb is not itself the failure to report, so the retrying
    /// connection is left to decide. Started without a shell, so nothing is written to a temporary script.
    /// </summary>
    private static void RestartLocalDb(string instance)
    {
        Run("stop", instance, "-k");
        Run("start", instance);
        return;

        static void Run(params string[] arguments)
        {
            try
            {
                var psi = new ProcessStartInfo("sqllocaldb") { UseShellExecute = false, CreateNoWindow = true };
                foreach (var argument in arguments)
                {
                    psi.ArgumentList.Add(argument);
                }

                using var process = Process.Start(psi);
                process?.WaitForExit(milliseconds: 60_000);
            }
            catch (Exception ex)
            {
                TestContext.Out.WriteLine($"sqllocaldb {string.Join(' ', arguments)} failed: {ex.Message}");
            }
        }
    }

    // A LocalDB instance shuts itself down when idle, so the first connection of a run starts one — and
    // that start competes with every other test assembly for the machine. Measured at 22s on a loaded
    // box, past the 15s default, which surfaces as "the timeout period elapsed while attempting to
    // consume the pre-login handshake" or a bare "SQL Server process failed to start" rather than as
    // anything naming a timeout. Raised only when the supplied string asks for less.
    private const int MinimumConnectTimeoutSeconds = 60;

    // unpooled, so a session the restore kills never returns to a pool
    private async Task<SqlConnection> Open(string database)
    {
        var builder = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = database, Pooling = false };
        if (builder.ConnectTimeout < MinimumConnectTimeoutSeconds)
        {
            builder.ConnectTimeout = MinimumConnectTimeoutSeconds;
        }

        var cn = new SqlConnection(builder.ConnectionString);
        await cn.OpenAsync();
        return cn;
    }

    private async Task Execute(string database, string sql, object? parameters = null)
    {
        await using var cn = await Open(database);
        await cn.ExecuteAsync(sql, parameters);
    }

    private async Task<int> CountProducts(string database)
    {
        await using var cn = await Open(database);
        return await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Products");
    }

    private async Task<string[]> PhysicalFileNames(string database)
    {
        await using var cn = await Open("master");
        var paths = await cn.QueryAsync<string>("SELECT physical_name FROM sys.master_files WHERE database_id = DB_ID(@database)", new { database });
        return paths.Select(path => Path.GetFileName(path)).ToArray();
    }
}
