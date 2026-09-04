# Regira Web.Analytics.GeoIP2 — examples

## Shipped entity

```csharp
using Regira.Web.Analytics;
using Regira.Web.Analytics.GeoIP2;
using Regira.Web.Analytics.GeoIP2.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAnalytics<GeoPageView>(builder.Configuration)
    .WithStore<MyStore>()
    .AddGeoIP2(builder.Configuration);
```

```json
"Analytics": { "GeoIP2": { "DatabasePath": "App_Data" } }
```

```csharp
public class MyStore : IPageViewStore<PageView>   // yours — persist by runtime type to keep subclass columns
{
    public Task SaveAsync(IReadOnlyList<PageView> views, CancellationToken ct = default) => Task.CompletedTask;
}
```

## Custom entity

```csharp
public class SitePageView : PageView, IGeoPageView
{
    public string? CountryCode { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? Campaign { get; set; }
}
```

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAnalytics<SitePageView>(builder.Configuration)
    .WithStore<MyStore>()   // IPageViewStore<in T>: a store of the base entity serves the subclass
    .AddGeoIP2(builder.Configuration, geo => geo.DatabasePath = "App_Data/GeoLite2-City.mmdb");
```

## Fake lookup (tests, or another provider)

```csharp
public class FixedLocation : IGeoLocationService
{
    public GeoLocation? Lookup(IPAddress? ip) => new("BE", "Belgium", "Ghent");
}
```

```csharp
var services = new ServiceCollection();
services.AddSingleton<IGeoLocationService, FixedLocation>();   // before AddGeoIP2 — TryAdd keeps it
services.AddAnalytics<GeoPageView>(new ConfigurationBuilder().Build())
    .WithStore<MyStore>()
    .AddGeoIP2(new ConfigurationBuilder().Build());
```

## Country/city breakdowns for the stats endpoint

```csharp
public static class GeoBreakdowns
{
    public static IReadOnlyDictionary<string, IReadOnlyList<KeyCount>> Build(IEnumerable<GeoPageView> views, int top)
        => new Dictionary<string, IReadOnlyList<KeyCount>>
        {
            ["country"] = views.Where(v => v.CountryCode != null)
                .GroupBy(v => v.Country ?? v.CountryCode)
                .Select(g => new KeyCount(g.Key, g.Count()))
                .OrderByDescending(k => k.Views).Take(top).ToList(),
            ["city"] = views.Where(v => v.City != null)
                .GroupBy(v => v.City)
                .Select(g => new KeyCount(g.Key, g.Count()))
                .OrderByDescending(k => k.Views).Take(top).ToList()
        };
}
```

Assign the result to `PageViewStats.Breakdowns` and the stats endpoint serves it unchanged.
