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
        _connectionString = connectionString!;

        var backupDirectory = Environment.GetEnvironmentVariable(BackupDirectoryVariable);
        _ownsBackupDirectory = string.IsNullOrWhiteSpace(backupDirectory);
        _backupDirectory = _ownsBackupDirectory ? Directory.CreateTempSubdirectory("regira-sqlserver-").FullName : backupDirectory!;

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

    // unpooled, so a session the restore kills never returns to a pool
    private async Task<SqlConnection> Open(string database)
    {
        var cn = new SqlConnection(new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = database, Pooling = false }.ConnectionString);
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
