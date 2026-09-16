using NUnit.Framework;
using Regira.DAL.PostgreSQL.Core;
using Regira.DAL.PostgreSQL.Services;

namespace DAL.PostgreSQL.Testing;

/// <summary>
/// How <see cref="PgBackupService"/> starts <c>pg_dump</c>: the executable itself, with the password in its
/// environment and every name escaped for the argument string — no server and no client binaries needed.
/// </summary>
[TestFixture]
public class PgToolArgumentsTests
{
    [Test]
    public async Task Backup_starts_pg_dump_itself_with_the_password_in_its_environment()
    {
        var processHelper = new CapturingProcessHelper();
        var settings = new PgSettings("db.internal", "sales", "ops", "s3cr%t", "5433");

        await CreateService(settings, processHelper).Backup();

        Assert.Multiple(() =>
        {
            Assert.That(Path.GetFileNameWithoutExtension(processHelper.FileName), Is.EqualTo("pg_dump"));
            Assert.That(processHelper.Arguments, Does.StartWith("--host \"db.internal\" --port \"5433\" --username \"ops\""));
            Assert.That(processHelper.Arguments, Does.EndWith(" \"sales\""));
            Assert.That(processHelper.Arguments, Does.Not.Contain("s3cr%t"));
            Assert.That(processHelper.EnvironmentVariables!["PGPASSWORD"], Is.EqualTo("s3cr%t"));
        });
    }

    [Test]
    public async Task Backup_escapes_quotes_and_the_backslashes_before_them()
    {
        var processHelper = new CapturingProcessHelper();
        // a quote would end the argument early, and a trailing backslash would escape the closing quote
        var settings = new PgSettings("localhost", "sales\"2026\\", "ops%USERNAME%", "secret");

        await CreateService(settings, processHelper, schemas: ["Sa\"les"]).Backup();

        Assert.Multiple(() =>
        {
            Assert.That(processHelper.Arguments, Does.EndWith(" \"sales\\\"2026\\\\\""));
            // the schema is a pg_dump pattern: quoted, with its own quote doubled, then escaped for the argument string
            Assert.That(processHelper.Arguments, Does.Contain("--strict-names --schema \"\\\"Sa\\\"\\\"les\\\"\""));
            // started without a shell, so nothing expands it
            Assert.That(processHelper.Arguments, Does.Contain("--username \"ops%USERNAME%\""));
        });
    }

    private static PgBackupService CreateService(PgSettings settings, CapturingProcessHelper processHelper, ICollection<string>? schemas = null)
        => new(new PgOptions { DbSettings = settings, ToolsDirectory = Path.GetTempPath(), BackupSchemas = schemas }, processHelper);
}
