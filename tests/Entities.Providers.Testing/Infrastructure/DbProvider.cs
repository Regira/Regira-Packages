namespace Entities.Providers.Testing.Infrastructure;

/// <summary>
/// The EF Core providers the query-pipeline suite runs against.
/// SQLite always runs in-memory; the container-backed providers only run when
/// the <c>REGIRA_PROVIDER_TESTS=containers</c> environment variable is set (and Docker is available).
/// </summary>
public enum DbProvider
{
    Sqlite,
    PostgreSql,
    SqlServer
}
