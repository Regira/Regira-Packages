using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.Normalizing;
using Regira.Entities.EFcore.Primers;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace Entities.Providers.Testing.Infrastructure;

/// <summary>
/// Owns the per-provider database lifecycle for a fixture instance:
/// <list type="bullet">
///   <item>SQLite: a single kept-open in-memory connection (like <c>tests\Entities.Testing</c>).</item>
///   <item>PostgreSQL / SQL Server: a Testcontainers container, started in <see cref="InitializeAsync"/>.</item>
/// </list>
/// Provider selection is gated:
/// <list type="bullet">
///   <item>SQLite always runs.</item>
///   <item>The container providers are <see cref="Assert.Ignore(string)">ignored</see> unless
///   <c>REGIRA_PROVIDER_TESTS=containers</c> is set.</item>
///   <item>Container startup is wrapped in try/catch so a machine without Docker skips (Ignore) rather than fails.</item>
/// </list>
/// A single container is shared per fixture instance (started once in OneTimeSetUp, disposed in OneTimeTearDown).
/// </summary>
public sealed class ProviderHarness(DbProvider provider) : IAsyncDisposable
{
    public const string EnvVar = "REGIRA_PROVIDER_TESTS";
    public const string EnableValue = "containers";

    public DbProvider Provider => provider;

    private SqliteConnection? _sqliteConnection;
    private PostgreSqlContainer? _postgresContainer;
    private MsSqlContainer? _mssqlContainer;
    private string? _connectionString;

    public static bool ContainersEnabled =>
        string.Equals(
            Environment.GetEnvironmentVariable(EnvVar),
            EnableValue,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Prepares the underlying database. Call from the fixture's OneTimeSetUp.
    /// Skips (Assert.Ignore) container providers when they're disabled or Docker is unavailable.
    /// </summary>
    public async Task InitializeAsync()
    {
        switch (provider)
        {
            case DbProvider.Sqlite:
                // Kept open for the fixture lifetime so the in-memory database survives between queries.
                _sqliteConnection = new SqliteConnection("Filename=:memory:");
                _sqliteConnection.Open();
                break;

            case DbProvider.PostgreSql:
                AssertContainersEnabled();
                try
                {
                    _postgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
                        .Build();
                    await _postgresContainer.StartAsync();
                    _connectionString = _postgresContainer.GetConnectionString();
                }
                catch (Exception ex)
                {
                    Assert.Ignore($"PostgreSQL container could not start (Docker unavailable?): {ex.Message}");
                }
                break;

            case DbProvider.SqlServer:
                AssertContainersEnabled();
                try
                {
                    _mssqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                        .Build();
                    await _mssqlContainer.StartAsync();
                    _connectionString = _mssqlContainer.GetConnectionString();
                }
                catch (Exception ex)
                {
                    Assert.Ignore($"SQL Server container could not start (Docker unavailable?): {ex.Message}");
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider");
        }
    }

    private static void AssertContainersEnabled()
    {
        Assume.That(ContainersEnabled,
            $"Skipped: set {EnvVar}={EnableValue} to run container-backed provider tests.");
        // Assume.That already halts as Ignore/Inconclusive, but keep an explicit Ignore as a clear signal.
        if (!ContainersEnabled)
        {
            Assert.Ignore($"Skipped: set {EnvVar}={EnableValue} to run container-backed provider tests.");
        }
    }

    /// <summary>
    /// Builds a fresh ServiceProvider wired with UseEntities().For&lt;Widget&gt;().UseDefaults() over the
    /// harness's provider, with the primer + normalizer interceptors so Created / NormalizedContent /
    /// NormalizedTitle are populated on save. A default capability-interface sort is registered so the
    /// interface-cast sorting path is exercised.
    /// </summary>
    public ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddDbContext<WidgetContext>((sp, db) =>
        {
            switch (provider)
            {
                case DbProvider.Sqlite:
                    db.UseSqlite(_sqliteConnection!);
                    break;
                case DbProvider.PostgreSql:
                    db.UseNpgsql(_connectionString!);
                    break;
                case DbProvider.SqlServer:
                    db.UseSqlServer(_connectionString!);
                    break;
            }

            // interceptors + UTC convention are auto-wired by UseEntities(e => e.UseDefaults()) below
        });

        services
            .UseEntities<WidgetContext>(e => e.UseDefaults())
            .For<Widget>(e =>
            {
                // Register the capability-interface default sort explicitly: this is the
                // ((IHasNormalizedTitle)x).NormalizedTitle cast-in-expression-tree that can fail to translate.
                e.SortBy(query => query.SortQuery<Widget, int>());
            });

        return services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        if (_sqliteConnection is not null)
        {
            _sqliteConnection.Close();
            await _sqliteConnection.DisposeAsync();
        }

        if (_postgresContainer is not null)
        {
            await _postgresContainer.DisposeAsync();
        }

        if (_mssqlContainer is not null)
        {
            await _mssqlContainer.DisposeAsync();
        }
    }
}
