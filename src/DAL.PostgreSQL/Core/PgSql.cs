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
    /// Whether the connected role may run <c>CREATE DATABASE</c>: a superuser, or a role with <c>CREATEDB</c>. An owner
    /// without it may still drop its own database.
    /// </summary>
    public const string CanCreateDatabase = "SELECT rolsuper OR rolcreatedb FROM pg_catalog.pg_roles WHERE rolname = current_user;";

    /// <summary>
    /// <c>CREATE DATABASE</c> for <paramref name="databaseName"/>.
    /// </summary>
    public static string CreateDatabase(string databaseName)
        => $"CREATE DATABASE {QuoteIdentifier(databaseName)} WITH TABLESPACE = pg_default;";

    /// <summary>
    /// <c>DROP DATABASE</c> for <paramref name="databaseName"/>. With <paramref name="force"/> the server first ends
    /// the sessions connected to it (<c>WITH (FORCE)</c>, PostgreSQL 13 or later), where it would refuse the drop.
    /// </summary>
    public static string DropDatabase(string databaseName, bool force = false)
        => $"DROP DATABASE IF EXISTS {QuoteIdentifier(databaseName)}{(force ? " WITH (FORCE)" : string.Empty)};";

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
