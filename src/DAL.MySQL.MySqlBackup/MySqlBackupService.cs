using MySqlConnector;
using Regira.DAL.Abstractions;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;

namespace Regira.DAL.MySQL.MySqlBackup;

public class MySqlBackupService(MySqlBackupOptions options) : IDbBackupService
{
    public async Task<IMemoryFile> Backup()
    {
        var connectionString = options.ConnectionString ?? options.DbSettings?.BuildConnectionString() ?? throw new ArgumentException("Connection data missing");

        await using var connection = new MySqlConnection(connectionString);
        await using var command = connection.CreateCommand();
        using var mySqlBackup = new MySqlConnector.MySqlBackup(command);

        await connection.OpenAsync();
        var ms = new MemoryStream();
        mySqlBackup.ExportToMemoryStream(ms);
        await connection.CloseAsync();

        return ms.ToMemoryFile();
    }
}