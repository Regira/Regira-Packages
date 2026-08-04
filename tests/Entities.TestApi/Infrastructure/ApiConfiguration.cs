namespace Entities.TestApi.Infrastructure;

public static class ApiConfiguration
{
    // Own file per test project: a shared temp name collides when the solution's test projects run in
    // parallel, and the loser fails with "the process cannot access the file" rather than anything
    // diagnostic. Entities.Web.Testing drives this API, so it reads the path from here instead of
    // rebuilding it — the two must point at the same database.
    public static string DatabaseFile = Path.Combine(Path.GetTempPath(), "regira-testapi.db");
    // Foreign Keys=True: SQLite doesn't enforce FKs by default — enforce them like a real provider would,
    // so constraint-violation paths (EntityConstraintException → 409) are exercisable in tests
    public static string ConnectionString = $"Filename={DatabaseFile};Foreign Keys=True";
    public static string AttachmentsDirectory = Path.Combine(Path.GetTempPath(), "testing", Guid.NewGuid().ToString("n"));
}