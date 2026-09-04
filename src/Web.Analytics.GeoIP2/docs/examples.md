# Regira.Web.Analytics.GeoIP2 — examples

## A store that keeps the geo columns

```csharp
public class MyGeoStore(IHostEnvironment env) : IPageViewStore<PageView>
{
    public async Task SaveAsync(IReadOnlyList<PageView> views, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(env.ContentRootPath, "App_Data", "pageviews.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);
        foreach (var view in views)
            // By runtime type, or the geo columns of a subclass are dropped.
            await writer.WriteLineAsync(JsonSerializer.Serialize(view, view.GetType()));
    }
}
```

## Faking the lookup in tests

```csharp
public class FixedLocation : IGeoLocationService
{
    public GeoLocation? Lookup(IPAddress? ip) => new("BE", "Belgium", "Ghent");
}
```

```csharp
var services = new ServiceCollection();
services.AddSingleton<IGeoLocationService, FixedLocation>();       // kept: AddGeoIP2 uses TryAdd
services.AddAnalytics<GeoPageView>(new ConfigurationBuilder().Build())
    .WithStore<MyGeoStore>()
    .AddGeoIP2(new ConfigurationBuilder().Build());
```

## Country-level breakdown in a stats store

```csharp
public static class GeoStats
{
    public static IReadOnlyList<KeyCount> TopCountries(IEnumerable<GeoPageView> views, int top)
        => views.Where(v => v.CountryCode != null)
            .GroupBy(v => v.Country ?? v.CountryCode)
            .Select(g => new KeyCount(g.Key, g.Count()))
            .OrderByDescending(k => k.Views)
            .Take(top)
            .ToList();
}
```

Expose it under `PageViewStats.Breakdowns["country"]` and the stats endpoint serves it unchanged.
