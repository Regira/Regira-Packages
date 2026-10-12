using Regira.Caching;
using Regira.Caching.Abstractions;
using Regira.Caching.Runtime;

namespace Common.Testing.Caching;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class CacheProviderTests
{
    // both providers share a process-wide store, so every test works under a prefix of its own
    private static IEnumerable<TestCaseData> Providers()
    {
        yield return new TestCaseData((Func<string, ICacheProvider>)(prefix => new DictionaryCacheProvider(prefix))).SetArgDisplayNames(nameof(DictionaryCacheProvider));
        yield return new TestCaseData((Func<string, ICacheProvider>)(prefix => new MemoryCacheProvider(new MemoryCacheProvider.Options { Prefix = prefix }))).SetArgDisplayNames(nameof(MemoryCacheProvider));
    }
    private static string NewPrefix() => $"{Guid.NewGuid():N}:";

    [TestCaseSource(nameof(Providers))]
    public void Keys_Lists_What_Was_Set(Func<string, ICacheProvider> create)
    {
        var cache = create(NewPrefix());
        cache.Set("BE", "Belgium");
        cache.Set("NL", "Netherlands");

        Assert.That(cache.Keys, Is.EquivalentTo(new[] { "BE", "NL" }));
    }

    [TestCaseSource(nameof(Providers))]
    public void Keys_Leaves_Out_A_Removed_Item(Func<string, ICacheProvider> create)
    {
        var cache = create(NewPrefix());
        cache.Set("BE", "Belgium");
        cache.Set("NL", "Netherlands");

        cache.Remove("BE");

        Assert.That(cache.Keys, Is.EquivalentTo(new[] { "NL" }));
    }

    [TestCaseSource(nameof(Providers))]
    public void RemoveAll_Removes_Every_Item(Func<string, ICacheProvider> create)
    {
        var cache = create(NewPrefix());
        cache.Set("BE", "Belgium");
        cache.Set("NL", "Netherlands");

        cache.RemoveAll();

        Assert.That(cache.Get<string>("BE"), Is.Null);
        Assert.That(cache.Get<string>("NL"), Is.Null);
        Assert.That(cache.Keys, Is.Empty);
    }

    [TestCaseSource(nameof(Providers))]
    public void RemoveAll_Keeps_Another_Prefix(Func<string, ICacheProvider> create)
    {
        var cache = create(NewPrefix());
        var other = create(NewPrefix());
        cache.Set("BE", "Belgium");
        other.Set("BE", "België");

        cache.RemoveAll();

        Assert.That(other.Get<string>("BE"), Is.EqualTo("België"));
    }

    [TestCaseSource(nameof(Providers))]
    public void Enumerating_Returns_Every_Item(Func<string, ICacheProvider> create)
    {
        var cache = create(NewPrefix());
        cache.Set("BE", "Belgium");
        cache.Set("NL", "Netherlands");

        var items = cache.Cast<KeyValuePair<string, object?>>().ToList();

        Assert.That(items, Is.EquivalentTo(new Dictionary<string, object?> { ["BE"] = "Belgium", ["NL"] = "Netherlands" }));
    }
}
