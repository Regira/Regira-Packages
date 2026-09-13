using Npgsql;
using NUnit.Framework;
using Regira.DAL.PostgreSQL.Core;
using Regira.DAL.PostgreSQL.Services;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.System.Abstractions;
using Testcontainers.PostgreSql;

namespace DAL.PostgreSQL.Testing;

/// <summary>
/// Runs the database operations <see cref="PgRestoreService"/> performs against a real PostgreSQL server:
/// looking a database up, creating it, dropping it, and the preparation <see cref="PgRestoreService.Restore"/>
/// does before handing the backup to <c>pg_restore</c>. The tool itself is stubbed
/// (<see cref="CapturingProcessHelper"/>), so no PostgreSQL client binaries are needed on the host.
/// </summary>
/// <remarks>
/// Gated like <c>tests\Entities.Providers.Testing</c>: the fixture is skipped unless
/// <c>REGIRA_PROVIDER_TESTS=containers</c> is set, and skips rather than fails when Docker is unavailable.
/// </remarks>
[TestFixture]
[Category("Containers")]
public class PgDatabaseTests
{
    public const string EnvVar = "REGIRA_PROVIDER_TESTS";
    public const string EnableValue = "containers";

    // Database names as the package's guides write them: a hyphen needs a quoted identifier, and so does
    // anything that is not lower case.
    private const string HyphenatedDb = "staging-db";
    private const string MixedCaseDb = "Staging_Db";

    // Opt-in container reuse: with REGIRA_CONTAINER_REUSE=1 the container is left running between runs,
    // so a repeat run attaches to it instead of starting a fresh one. It also needs
    // testcontainers.reuse.enable=true in ~/.testcontainers.properties. Off by default, because a reused
    // container carries its previous state into the next run. See CONTRIBUTING.md.
    private static bool ReuseContainers => Environment.GetEnvironmentVariable("REGIRA_CONTAINER_REUSE") == "1";

    private PostgreSqlContainer? _container;
    private PgSettings _server = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnvVar), EnableValue, StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore($"Skipped: set {EnvVar}={EnableValue} to run container-backed tests.");
        }

        try
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").WithReuse(ReuseContainers).Build();
            await _container.StartAsync();
        }
        catch (Exception ex)
        {
            Assert.Ignore($"PostgreSQL container could not start (Docker unavailable?): {ex.Message}");
        }

        var cn = new NpgsqlConnectionStringBuilder(_container!.GetConnectionString());
        _server = new PgSettings(cn.Host, cn.Database, cn.Username, cn.Password, cn.Port.ToString());
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_container == null)
        {
            return;
        }

        await using var cn = await OpenServerConnection();
        var service = CreateService(HyphenatedDb);
        await service.Drop(cn, HyphenatedDb);
        await service.Drop(cn, MixedCaseDb);
    }


    [Test]
    public async Task Exists_is_false_for_an_unknown_database()
    {
        await using var cn = await OpenServerConnection();

        Assert.That(await CreateService(HyphenatedDb).Exists(cn, HyphenatedDb), Is.False);
    }

    [Test]
    public async Task Create_accepts_a_hyphenated_name()
    {
        await using var cn = await OpenServerConnection();
        var service = CreateService(HyphenatedDb);

        await service.Create(cn, HyphenatedDb);

        Assert.That(await service.Exists(cn, HyphenatedDb), Is.True);
    }

    [Test]
    public async Task Create_preserves_the_case_of_the_name()
    {
        await using var cn = await OpenServerConnection();
        var service = CreateService(MixedCaseDb);

        await service.Create(cn, MixedCaseDb);

        var exactExists = await service.Exists(cn, MixedCaseDb);
        var loweredExists = await service.Exists(cn, MixedCaseDb.ToLowerInvariant());

        Assert.Multiple(() =>
        {
            Assert.That(exactExists, Is.True);
            // an unquoted CREATE DATABASE would have folded the name to lower case
            Assert.That(loweredExists, Is.False);
        });
    }

    [Test]
    public async Task Drop_removes_the_database()
    {
        await using var cn = await OpenServerConnection();
        var service = CreateService(HyphenatedDb);
        await service.Create(cn, HyphenatedDb);

        await service.Drop(cn, HyphenatedDb);

        Assert.That(await service.Exists(cn, HyphenatedDb), Is.False);
    }

    [Test]
    public async Task Drop_ignores_a_database_that_is_not_there()
    {
        await using var cn = await OpenServerConnection();

        Assert.DoesNotThrowAsync(() => CreateService(HyphenatedDb).Drop(cn, HyphenatedDb));
    }


    [Test]
    public async Task Restore_creates_the_target_database()
    {
        await CreateService(HyphenatedDb, new CapturingProcessHelper()).Restore(Backup());

        await using var cn = await OpenServerConnection();
        Assert.That(await CreateService(HyphenatedDb).Exists(cn, HyphenatedDb), Is.True);
    }

    [Test]
    public async Task Restore_fails_on_an_existing_database_without_overwrite()
    {
        await using var cn = await OpenServerConnection();
        await CreateService(HyphenatedDb).Create(cn, HyphenatedDb);

        var ex = Assert.ThrowsAsync<Exception>(() => CreateService(HyphenatedDb, new CapturingProcessHelper()).Restore(Backup()));

        Assert.That(ex!.Message, Does.Contain(HyphenatedDb));
    }

    [Test]
    public async Task Restore_with_overwrite_recreates_an_existing_database()
    {
        await using var cn = await OpenServerConnection();
        await CreateService(HyphenatedDb).Create(cn, HyphenatedDb);

        await CreateService(HyphenatedDb, new CapturingProcessHelper(), overwrite: true).Restore(Backup());

        Assert.That(await CreateService(HyphenatedDb).Exists(cn, HyphenatedDb), Is.True);
    }

    [Test]
    public async Task Restore_hands_the_tool_a_closed_complete_file_and_removes_it_afterwards()
    {
        var contents = new byte[] { 1, 2, 3, 4, 5 };
        var processHelper = new CapturingProcessHelper();

        await CreateService(HyphenatedDb, processHelper).Restore(contents.ToMemoryFile());

        Assert.Multiple(() =>
        {
            // read by the stub while the "tool" was running: an unflushed or still-open file fails here
            Assert.That(processHelper.SourceBytes, Is.EqualTo(contents));
            Assert.That(File.Exists(processHelper.SourcePath!), Is.False, "temporary backup file was left behind");
        });
    }


    [Test]
    public async Task Restore_leaves_the_database_alone_when_the_backup_cannot_be_read()
    {
        await using var cn = await OpenServerConnection();
        await CreateService(HyphenatedDb).Create(cn, HyphenatedDb);
        // stands in for a corrupt or truncated archive: `pg_restore --list` cannot read it
        var processHelper = new CapturingProcessHelper { ListExitCode = 1 };

        Assert.ThrowsAsync<Exception>(() => CreateService(HyphenatedDb, processHelper, overwrite: true).Restore(Backup()));

        // the database the backup was meant to replace is still there
        Assert.That(await CreateService(HyphenatedDb).Exists(cn, HyphenatedDb), Is.True);
    }

    [Test]
    public async Task Restore_passes_the_password_through_the_environment()
    {
        var processHelper = new CapturingProcessHelper();

        await CreateService(HyphenatedDb, processHelper).Restore(Backup());

        var environment = processHelper.EnvironmentVariables;
        Assert.That(environment, Is.Not.Null);
        Assert.That(environment!["PGPASSWORD"], Is.EqualTo(_server.Password));
        // the value would be in the command as `set "PGPASSWORD=..."` if the service put it there
        Assert.That(processHelper.Command, Does.Not.Contain("PGPASSWORD"));
    }

    private static IMemoryFile Backup() => new byte[] { 1, 2, 3, 4, 5 }.ToMemoryFile();

    private PgRestoreService CreateService(string targetDb, IProcessHelper? processHelper = null, bool overwrite = false)
        => new(
            new PgOptions
            {
                DbSettings = new PgSettings(_server.Host, targetDb, _server.Username, _server.Password, _server.Port),
                ToolsDirectory = Path.GetTempPath(),
                Overwrite = overwrite
            },
            processHelper ?? new CapturingProcessHelper()
        );

    private async Task<NpgsqlConnection> OpenServerConnection()
    {
        var cn = new NpgsqlConnection(_server.BuildConnectionString());
        await cn.OpenAsync();
        return cn;
    }
}
