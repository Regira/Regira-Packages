using Microsoft.Extensions.Configuration;

namespace Entities.TestApi.Infrastructure;

public static class ApiConfiguration
{
    // Configuration keys a test host overrides (via IWebHostBuilder.UseSetting) to get a database and an
    // attachments folder of its own. Running the API standalone sets neither and falls back to the
    // defaults below.
    public const string ConnectionStringKey = "TestApi:ConnectionString";
    public const string AttachmentsDirectoryKey = "TestApi:AttachmentsDirectory";

    // Own file per test project: a shared temp name collides when the solution's test projects run in
    // parallel, and the loser fails with "the process cannot access the file" rather than anything
    // diagnostic.
    public static string DatabaseFile = Path.Combine(Path.GetTempPath(), "regira-testapi.db");
    // Foreign Keys=True: SQLite doesn't enforce FKs by default — enforce them like a real provider would,
    // so constraint-violation paths (EntityConstraintException → 409) are exercisable in tests
    public static string ConnectionString = BuildConnectionString(DatabaseFile);
    public static string AttachmentsDirectory = Path.Combine(Path.GetTempPath(), "testing", Guid.NewGuid().ToString("n"));

    public static string BuildConnectionString(string databaseFile) => $"Filename={databaseFile};Foreign Keys=True";

    public static string ResolveConnectionString(IConfiguration configuration)
        => configuration[ConnectionStringKey] ?? ConnectionString;
    public static string ResolveAttachmentsDirectory(IConfiguration configuration)
        => configuration[AttachmentsDirectoryKey] ?? AttachmentsDirectory;
}