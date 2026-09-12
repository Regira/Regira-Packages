namespace Regira.DAL.PostgreSQL.Core;

public static class PgDefaults
{
    public static string Host { get; set; } = "localhost";
    public static string Port { get; set; } = "5432";
    public static string Username { get; set; } = "postgres";
    public static string Password { get; set; } = "postgres";
    /// <summary>
    /// Database to connect to for statements that cannot run from a connection to the database they affect
    /// (<c>CREATE DATABASE</c>, <c>DROP DATABASE</c>).
    /// </summary>
    public static string MaintenanceDatabase { get; set; } = "postgres";
}