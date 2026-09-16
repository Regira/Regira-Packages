using Entities.TestApi.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testing.Library.Data;

namespace Entities.Web.Testing.Infrastructure;

/// <summary>
/// One API host per test class. Booting the host is what these tests actually spend their time on — the
/// container is validated on build — so the host is shared by every test in the class while each test still
/// gets a fresh database: xUnit constructs the test class once per test, and the class constructor creates
/// and seeds it.
/// <para>
/// The database file and the attachments folder are per factory instance. That isolation is what lets the
/// test classes run concurrently: with one shared path, each class deleting "the" database at the end of
/// every test would be safe only while nothing else is running.
/// </para>
/// </summary>
public class ContosoApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseFile;

    public string ConnectionString { get; }
    public string AttachmentsDirectory { get; }

    public ContosoApiFactory()
    {
        var id = Guid.NewGuid().ToString("n");
        _databaseFile = Path.Combine(Path.GetTempPath(), $"regira-testapi-{id}.db");
        // Pooling=False: the host outlives the database, which each test drops and recreates in its
        // constructor. A pooled connection would survive that cycle still bound to the dropped file.
        ConnectionString = ApiConfiguration.BuildConnectionString(_databaseFile) + ";Pooling=False";
        AttachmentsDirectory = Path.Combine(Path.GetTempPath(), "testing", id);
    }

    /// <summary>A context on this host's database, for arranging and asserting around the HTTP calls.</summary>
    public ContosoContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ContosoContext>().UseSqlite(ConnectionString).Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(ApiConfiguration.ConnectionStringKey, ConnectionString);
        builder.UseSetting(ApiConfiguration.AttachmentsDirectoryKey, AttachmentsDirectory);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        // Nothing to drain before deleting: the connection string disables pooling, so no connection
        // outlives the request that opened it. (SqliteConnection.ClearAllPools would be process-wide
        // and would reach into sibling test classes' hosts, which are live at this point.)
        try
        {
            File.Delete(_databaseFile);
        }
        catch (IOException)
        {
            // A leftover temp file is not worth failing a green run over.
        }
        try
        {
            if (Directory.Exists(AttachmentsDirectory))
            {
                Directory.Delete(AttachmentsDirectory, true);
            }
        }
        catch (IOException)
        {
        }
    }
}
