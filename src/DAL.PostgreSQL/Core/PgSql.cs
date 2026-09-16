namespace Regira.DAL.PostgreSQL.Core;

/// <summary>
/// SQL for the database-level operations shared by <see cref="Regira.DAL.PostgreSQL.Services.PgRestoreService"/>
/// and <see cref="Regira.DAL.PostgreSQL.Services.BackupRestoreManager"/>.
/// </summary>
internal static class PgSql
{
    /// <summary>
    /// Looks a database up in <c>pg_database</c>. The name is supplied as the <c>databaseName</c> parameter.
    /// </summary>
    public const string DatabaseExists = "SELECT EXISTS (SELECT NULL FROM pg_catalog.pg_database WHERE datname = @databaseName);";

    /// <summary>
    /// <c>CREATE DATABASE</c> for <paramref name="databaseName"/>.
    /// </summary>
    public static string CreateDatabase(string databaseName)
        => $"CREATE DATABASE {QuoteIdentifier(databaseName)} WITH TABLESPACE = pg_default;";

    /// <summary>
    /// <c>DROP DATABASE</c> for <paramref name="databaseName"/>.
    /// </summary>
    public static string DropDatabase(string databaseName)
        => $"DROP DATABASE IF EXISTS {QuoteIdentifier(databaseName)};";

    /// <summary>
    /// Quotes an identifier: wrapped in double quotes, with embedded double quotes doubled.
    /// </summary>
    /// <remarks>
    /// An identifier can never be a query parameter, so a name reaches the server as part of the statement.
    /// Unquoted, a name containing a hyphen (<c>staging-db</c>) is a syntax error and a name with capitals is
    /// silently folded to lower case.
    /// </remarks>
    public static string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException("Identifier is required", nameof(identifier));
        }

        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }
}
