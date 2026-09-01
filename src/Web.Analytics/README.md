# Regira.Web.Analytics

Abstract visitor analytics for ASP.NET Core. A filterable middleware captures page views into a bounded
queue; a background writer enriches them and hands them — in batches, off the request thread — to
whatever persistence the host registers. The package ships **no storage, no geolocation and no extra
dependencies**: only `Regira.Web` and the ASP.NET Core framework reference. Everything specific is a
hook the consumer plugs in through DI.

## What is recorded

One row per qualifying request (`PageView`): UTC timestamp, site name, path, query string, referrer
(raw + external host), utm source, user agent, a masked client IP, a bot flag and the status code.
Deliberately nothing that ties two visits to the same person: no cookie, no session id, no full IP
address (masking to /24 / /48 is on by default; enrichers see the full address in memory only).

## Quick start

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddAnalyticsConfiguration();                 // optional: watched botdetector.json
builder.Services.AddAnalytics(builder.Configuration)
    .WithStore<MyPageViewStore>();                   // your persistence — required for tracking

var app = builder.Build();
app.UseForwardedHeaders();                           // host's concern; must come first
app.UseAnalytics();                                  // before static files / SPA fallback
app.MapAnalyticsEndpoints();                         // optional stats route, see below
```

```json
"Analytics": {
  "SiteName": "MySite",
  "IgnorePaths": [ "/api", "/css", "/js" ]
}
```

A minimal store is any class implementing the save hook — append to a file, insert into a database,
forward to a queue; the package does not care:

```csharp
public class MyPageViewStore : IPageViewStore<PageView>
{
    public Task SaveAsync(IReadOnlyList<PageView> views, CancellationToken cancellationToken = default)
        => Task.CompletedTask;   // yours: file append, database insert, queue publish, ...
}
```

## The hooks

| Hook | Runs | Use for |
|---|---|---|
| `IVisitFilter` | in-request, around the pipeline | which requests count (`ShouldTrack`) and which responses keep them (`ShouldRecord`, default 200/304). Default `HtmlPageVisitFilter` tracks browser page loads; replace it via `WithFilter<T>()` for API/RPC traffic |
| `IVisitContributor<TPageView>` | in-request, before + after the endpoint | anything only the live `HttpContext` has: request-body details (enable buffering in `OnCapturing`), items other middleware stashed. Add via `AddContributor<T>()` |
| `IPageViewEnricher<TPageView>` | background writer, before masking | enrichment from the **unmasked** client IP — a geolocation lookup, typically. Add via `AddEnricher<T>()` |
| `IPageViewStore<TPageView>` | background writer, per batch | persistence. Register via `WithStore<T>()`; without it, tracking stays inactive (with a warning) |
| `IPageViewRetentionStore` | background writer, every 24h | optional purge of rows older than `RetentionDays` |
| `IPageViewStatsStore` | stats endpoint | optional aggregation powering `GET /analytics/stats` |

`WithStore<TStore>()` registers the retention and stats interfaces automatically when `TStore`
implements them. **Lifetimes:** the store and enrichers are registered *scoped* and resolved from a
fresh service scope per batch, so scoped dependencies (a DbContext) work without ceremony —
pre-register the store yourself to pick another lifetime. Contributors are *singletons*: the
middleware constructor-injects them once from the root provider, so scoped dependencies don't fit
there. A `WithStore(Func<IServiceProvider, ...>)` factory overload exists for construction the
container cannot do; it does **not** auto-wire the stats/retention interfaces — register those
yourself.

## A custom entity — geolocation as the example

The pipeline is generic over the entity. Derive from `PageView`, register the subclass, and fill your
properties from a contributor (request-bound data) or enricher (IP-bound data):

```csharp
public record GeoLocation(string? CountryCode, string? Country, string? City);

// Your resolver — MaxMind GeoIP2, IP2Location, a web service, ...; the dependency stays in your project.
public interface IGeoLookup
{
    Task<GeoLocation?> FindAsync(IPAddress? ip, CancellationToken cancellationToken = default);
}

public class GeoPageView : PageView
{
    [MaxLength(2)]   public string? CountryCode { get; set; }
    [MaxLength(128)] public string? Country { get; set; }
    [MaxLength(128)] public string? City { get; set; }
}

public class GeoEnricher(IGeoLookup lookup) : IPageViewEnricher<GeoPageView>
{
    public async ValueTask EnrichAsync(PendingPageView<GeoPageView> pending, CancellationToken cancellationToken = default)
    {
        // Runs before the IP is masked — the one place the full address is available.
        var found = await lookup.FindAsync(pending.ClientIp, cancellationToken);
        pending.View.CountryCode = found?.CountryCode;
        pending.View.Country = found?.Country;
        pending.View.City = found?.City;
    }
}
```

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAnalytics<GeoPageView>(builder.Configuration)
    .WithStore<MyPageViewStore>()   // IPageViewStore<in T>: a base-entity store serves any subclass
    .AddEnricher<GeoEnricher>();
```

One entity type per host; a second `AddAnalytics` naming a different one throws. The constraint is
`class, IPageView, new()` — deriving from `PageView` is the convenient route, but any class
implementing `IPageView` works. A base-entity store reused for a subclass like this must persist by
runtime type or the subclass's columns are silently lost — see the JSON-lines store in
`docs/examples.md`.

## Configuration (`Analytics` section)

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | `false` registers nothing but the config; the builder's methods no-op |
| `SiteName` | entry assembly name | discriminator when several hosts share one store |
| `MaskIpAddress` | `true` | truncate to /24 (IPv4) / /48 (IPv6) before storing |
| `RecordBots` | `true` | keep crawler rows (flagged `IsBot`) instead of dropping them |
| `RetentionDays` | `365` | purge cutoff; needs an `IPageViewRetentionStore`; `0` keeps everything |
| `ApiKey` | empty | `X-Analytics-Key` for the stats route; empty = route not mapped |
| `IgnorePaths` | `[]` | extra prefixes the default filter skips |
| `QueueCapacity` / `BatchSize` / `FlushIntervalSeconds` | `10000` / `200` / `5` | queue bound and write batching |
| `BotDetection:MinUserAgentLength` | `12` | shorter/absent user agents are flagged; `0` disables |
| `BotDetection:IncludeDefaultMarkers` | `true` | merge the built-in ~90 crawler markers under yours |
| `BotDetection:Markers` / `Exceptions` | `[]` | your additions; exceptions clear an agent before markers run |

The default `HtmlPageVisitFilter` tracks GET requests whose `Accept` contains `text/html` and whose
last path segment has no dot, and skips the built-in prefixes `/favicon`, `/.well-known`,
`/robots.txt`, `/sitemap` and `/analytics`. These are prefix matches — a page route like
`/sitemapping` would be skipped too; a host with such routes registers its own filter. `IgnorePaths`
adds the host's own prefixes on top.

Bot markers are compiled into the package; `AddAnalyticsConfiguration()` additionally loads an
optional, watched `botdetector.json` from the content root so new crawlers can be flagged without a
restart.

## Stats endpoint

`MapAnalyticsEndpoints()` maps `GET /analytics/stats` only when `Analytics:ApiKey` is set **and** an
`IPageViewStatsStore` is registered. The key travels in the `X-Analytics-Key` header (constant-time
comparison). Query: `days` (1–730), `top` (1–100), `includeBots`, `site` (`*` spans all sites). The
response wraps the store's `PageViewStats`: totals, per-day counts, top paths/referrers, per-site,
recent rows, plus store-defined `Breakdowns` (a geo-aware store exposes `country`, an RPC host might
expose `tool`). Recent rows serialize as their runtime type, so a custom entity's own columns appear.
The route relies on the key check alone and is not marked `AllowAnonymous` — a host with a fallback
authorization policy will require that authorization on top.

## Pipeline ordering

`UseAnalytics()` after `UseForwardedHeaders()` — this package does not touch forwarded-headers
configuration, so behind a proxy the host must, or every row records the proxy's IP — and before
`UseStaticFiles()`/`UseDefaultFiles()` or any SPA fallback, which answer requests the middleware would
otherwise never see.
