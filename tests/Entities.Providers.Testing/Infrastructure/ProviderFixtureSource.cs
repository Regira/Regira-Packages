using System.Collections;
using NUnit.Framework;

namespace Entities.Providers.Testing.Infrastructure;

/// <summary>
/// Supplies the provider parameter to <c>[TestFixtureSource]</c>. All three providers are always listed
/// so the test runner reports them explicitly; the container-backed ones are skipped (Assert.Ignore) in
/// the fixture's OneTimeSetUp when <c>REGIRA_PROVIDER_TESTS=containers</c> is unset or Docker is unavailable.
/// </summary>
public class ProviderFixtureSource : IEnumerable
{
    public IEnumerator GetEnumerator()
    {
        yield return new TestFixtureData(DbProvider.Sqlite).SetArgDisplayNames(nameof(DbProvider.Sqlite));
        yield return new TestFixtureData(DbProvider.PostgreSql).SetArgDisplayNames(nameof(DbProvider.PostgreSql));
        yield return new TestFixtureData(DbProvider.SqlServer).SetArgDisplayNames(nameof(DbProvider.SqlServer));
    }
}
