# Regira Web.Analytics.GeoIP2 AI Agent Instructions

> Geolocation enricher for `Regira.Web.Analytics`: country/city from a **local** MaxMind
> GeoIP2/GeoLite2 `.mmdb`. The only package in the family that depends on `MaxMind.GeoIP2`.

## Installation

```xml
<PackageReference Include="Regira.Web.Analytics" Version="6.*" />
<PackageReference Include="Regira.Web.Analytics.GeoIP2" Version="6.*" />
```

## Registration

```csharp no-compile
builder.Services.AddAnalytics<GeoPageView>(builder.Configuration)   // or your own IGeoPageView entity
    .WithStore<MyStore>()
    .AddGeoIP2(builder.Configuration);                               // binds Analytics:GeoIP2
```

```json
"Analytics": { "GeoIP2": { "DatabasePath": "App_Data" } }
```

- Entity constraint: `class, IPageView, IGeoPageView, new()`. `GeoPageView` (= `PageView` + `CountryCode`,
  `Country`, `City`) is shipped; implement `IGeoPageView` on a custom entity instead.
- `DatabasePath`: `.mmdb` file or directory (City preferred over Country); relative to content root, then
  base directory. Missing → lookup disabled with a log line, columns stay null, nothing throws.
- Registers `IGeoLocationService` with `TryAdd` → pre-register your own (or a fake) to replace MaxMind.
- No-op when `Analytics:Enabled` is `false`; a repeat `AddGeoIP2` keeps the first configuration.
- Runs as an `IPageViewEnricher` (background, unmasked IP, before masking). Loopback/link-local/private
  addresses are skipped.

## Gotchas

- The `.mmdb` is a licensed MaxMind download — deploy it to the server, never commit it. Without a City
  database `City` stays null.
- A store shared across entity types must serialize by runtime type or the geo columns are lost.
- Behind a reverse proxy, forwarded headers must be configured by the host or every lookup sees the
  proxy's address.
