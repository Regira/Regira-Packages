using MySqlConnector;
using Regira.DAL.Abstractions;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;

namespace Regira.DAL.MySQL.MySqlBackup;

public class MySqlRestoreService(MySqlBackupOptions options) : IDbRestoreService
{
    public async Task Restore(IMemoryFile file)
    {
        var connectionString = options.ConnectionString ?? options.DbSettings?.BuildConnectionString() ?? throw new ArgumentException("Connection data missing");

        await using var connection = new MySqlConnection(connectionString);
        await using var command = connection.CreateCommand();
        using var mySqlBackup = new MySqlConnector.MySqlBackup(command);

        await connection.OpenAsync();
        using var ms = file.GetStream();
        mySqlBackup.ImportFromStream(ms);
        await connection.CloseAsync();
    }
}