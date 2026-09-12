using Regira.DAL.SqlServer.Core;
using Regira.DAL.SqlServer.Services;
using Regira.IO.Extensions;
using Regira.IO.Models;

namespace DAL.SqlServer.Testing;

/// <summary>
/// Misconfiguration is reported before anything reaches a server — the connection string below points nowhere
/// </summary>
public class OptionsValidationTests
{
    private const string Unreachable = "Server=unreachable.invalid;Database=shop;Integrated Security=true;Connect Timeout=1";
    private static readonly byte[] Content = [1, 2, 3];

    [Test]
    public void Backup_Requires_A_Backup_Directory()
    {
        var options = new SqlServerOptions { ConnectionString = Unreachable };

        var ex = Assert.ThrowsAsync<ArgumentException>(() => new SqlServerBackupService(options).Backup());
        Assert.That(ex!.Message, Does.Contain(nameof(SqlServerOptions.BackupDirectory)));
    }

    [Test]
    public void Restore_Requires_A_Backup_Directory()
    {
        var options = new SqlServerOptions { ConnectionString = Unreachable };

        var ex = Assert.ThrowsAsync<ArgumentException>(() => new SqlServerRestoreService(options).Restore(Content.ToMemoryFile()));
        Assert.That(ex!.Message, Does.Contain(nameof(SqlServerOptions.BackupDirectory)));
    }

    [Test]
    public void Backup_Requires_Connection_Data()
    {
        var options = new SqlServerOptions { BackupDirectory = @"D:\SqlBackups" };

        Assert.ThrowsAsync<ArgumentException>(() => new SqlServerBackupService(options).Backup());
    }

    [Test]
    public void Backup_Requires_A_Database_Name()
    {
        var options = new SqlServerOptions
        {
            ConnectionString = "Server=unreachable.invalid;Integrated Security=true;Connect Timeout=1",
            BackupDirectory = @"D:\SqlBackups"
        };

        Assert.ThrowsAsync<ArgumentException>(() => new SqlServerBackupService(options).Backup());
    }

    [Test]
    public void Restore_Requires_File_Content()
    {
        var options = new SqlServerOptions { ConnectionString = Unreachable, BackupDirectory = @"D:\SqlBackups" };

        Assert.ThrowsAsync<ArgumentException>(() => new SqlServerRestoreService(options).Restore(new BinaryFileItem()));
    }
}
