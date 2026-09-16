using Microsoft.Data.SqlClient;

namespace Regira.DAL.SqlServer.Core;

internal static class SqlServerOptionsExtensions
{
    public static SqlConnectionStringBuilder CreateConnectionStringBuilder(this SqlServerOptions options)
    {
        var connectionString = options.DbSettings?.BuildConnectionString()
            ?? options.ConnectionString
            ?? throw new ArgumentException("Connection data missing");
        // BACKUP and RESTORE refuse to run inside a transaction, so never join the caller's ambient TransactionScope
        return new SqlConnectionStringBuilder(connectionString) { Enlist = false };
    }

    public static string GetDatabaseName(this SqlConnectionStringBuilder builder)
        => string.IsNullOrEmpty(builder.InitialCatalog)
            ? throw new ArgumentException("Database name missing")
            : builder.InitialCatalog;
}
