# Regira.Web.Analytics.GeoIP2

Geolocation for [Regira.Web.Analytics](../Web.Analytics): a background enricher that resolves country
and city from a **local** MaxMind GeoIP2/GeoLite2 database, so visitor addresses never leave the
server. It runs on the unmasked client IP before the analytics writer truncates it — the seam the core
package leaves open on purpose — and it is the only place the `MaxMind.GeoIP2` dependency lives.

## Setup

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAnalytics<GeoPageView>(builder.Configuration)   // shipped entity with geo columns
    .WithStore<MyGeoStore>()
    .AddGeoIP2(builder.Configuration);
```

```json
"Analytics": {
  "GeoIP2": { "DatabasePath": "App_Data" }
}
```

`DatabasePath` is an `.mmdb` file or a directory holding one; a directory prefers a City database over
a Country one, so dropping in a City download upgrades from country-level to city-level without a
config change. Relative paths resolve against the content root, then the application base directory.
No path, or no file found, disables the lookup with a log line — rows simply keep empty geo columns.
The database is a licensed MaxMind download (GeoLite2 is free with an account); deploy it next to the
app, never commit it.

## Your own entity

Implement `IGeoPageView` on any page-view entity and the enricher fills it:

```csharp
public class SitePageView : PageView, IGeoPageView
{
    [MaxLength(2)]   public string? CountryCode { get; set; }
    [MaxLength(128)] public string? Country { get; set; }
    [MaxLength(128)] public string? City { get; set; }
    [MaxLength(64)]  public string? Campaign { get; set; }
}
```

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAnalytics<SitePageView>(builder.Configuration)
    .WithStore<MyGeoStore>()   // IPageViewStore<in T>: a store of the base entity serves the subclass
    .AddGeoIP2(builder.Configuration);
```

## Swapping the lookup

`AddGeoIP2` registers `IGeoLocationService` with `TryAdd`, so a service registered before it — another
provider, or a fake in tests — is kept and the MaxMind reader is never opened.

## Privacy

Lookups skip loopback, link-local and private ranges. The core package's IP masking still applies:
the full address is used for the lookup and only the truncated form is stored.
