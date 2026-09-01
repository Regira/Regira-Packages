# Regira Web.Analytics AI Agent Instructions

> Abstract visitor analytics for ASP.NET Core: a filterable middleware captures page views into a
> bounded queue, a background writer enriches and batches them, and consumer-registered hooks decide
> everything specific — what counts as a visit, what extra data a row carries, and where rows go.
> The package ships **no storage, no geolocation, no extra dependencies**.

## Installation

```xml
<PackageReference Include="Regira.Web.Analytics" Version="6.*" />
```

## Registration

```csharp
builder.AddAnalyticsConfiguration();                       // optional: watched botdetector.json in the content root
builder.Services.AddAnalytics(builder.Configuration)       // binds the "Analytics" section
    .WithStore<MyPageViewStore>();                         // REQUIRED for tracking — no store, no tracking (warning logged)

var app = builder.Build();
app.UseForwardedHeaders();                                 // host's job — the package never configures forwarded headers
app.UseAnalytics();                                        // AFTER UseForwardedHeaders, BEFORE UseStaticFiles/UseDefaultFiles/SPA fallback
...
app.MapAnalyticsEndpoints();                               // optional GET /analytics/stats (needs ApiKey + IPageViewStatsStore)
```

`AddAnalytics<TPageView>(...)` takes a custom entity (`TPageView : class, IPageView, new()` — derive
from `PageView` or implement `IPageView` from scratch). One entity type
per host — a second call naming a different one throws. When `Analytics:Enabled` is `false`, the
builder's methods no-op, so registration code needs no guards.

## The hooks — pick by *when* the data exists

| Hook | Timing | Register | Use for |
|---|---|---|---|
| `IVisitFilter` | in-request | `WithFilter<T>()` (replaces default) | `ShouldTrack(HttpRequest)` pre-endpoint (path is pre-rewrite), `ShouldRecord(HttpContext)` post-response (default: 200/304) |
| `IVisitContributor<TPageView>` | in-request: `OnCapturing` before the endpoint, `OnCapturedAsync` after the response | `AddContributor<T>()` | data only the live HttpContext has — request body (call `Request.EnableBuffering()` in `OnCapturing`, read+rewind in `OnCapturedAsync`), `HttpContext.Items` from other middleware |
| `IPageViewEnricher<TPageView>` | background writer, **before** IP masking | `AddEnricher<T>()` | enrichment from the unmasked `PendingPageView.ClientIp` — the geolocation seam. No HttpContext here |
| `IPageViewStore<TPageView>` | background writer, per batch | `WithStore<T>()` | persistence — file, database, queue; the package does not care |
| `IPageViewRetentionStore` | background writer, every 24h | auto via `WithStore<T>()` when implemented | purge rows older than `RetentionDays`, scoped to `SiteName` |
| `IPageViewStatsStore` | stats endpoint | auto via `WithStore<T>()` when implemented | aggregation for `GET /analytics/stats` |

**Lifetimes:** stores and enrichers are registered *scoped* and resolved from a fresh DI scope per
batch → scoped dependencies (a DbContext) work; pre-register the store to pick another lifetime.
Contributors are *singletons* — the middleware constructor-injects them from the root provider, so
scoped dependencies don't fit there. The `WithStore(Func<IServiceProvider, ...>)` factory overload
does **not** auto-wire stats/retention — register those yourself. Contributor/enricher exceptions are
logged and swallowed; a store exception loses that batch but never kills the writer loop.

## Custom entity

Derive from `PageView` for host-specific dimensions; the pipeline fills the base properties, your
contributors/enrichers fill the rest. Canonical example — geolocation:

```csharp
public class GeoPageView : PageView { public string? CountryCode { get; set; } /* Country, City */ }
// IPageViewEnricher<GeoPageView> resolves them from pending.ClientIp (unmasked, pre-persist)
builder.Services.AddAnalytics<GeoPageView>(builder.Configuration)
    .WithStore<GeoStore>().AddEnricher<GeoEnricher>();
```

## Configuration (`Analytics` section)

`Enabled` (true) · `SiteName` (entry assembly; discriminator for shared stores) · `MaskIpAddress`
(true; /24 / /48) · `RecordBots` (true; flagged not dropped) · `RetentionDays` (365; needs retention
store; 0 = keep) · `ApiKey` (empty = stats route not mapped; header `X-Analytics-Key`) · `IgnorePaths`
([]) · `QueueCapacity`/`BatchSize`/`FlushIntervalSeconds` (10000/200/5) ·
`BotDetection:{MinUserAgentLength (12), IncludeDefaultMarkers (true), Markers, Exceptions}`.

Bot markers are compiled in (~90 crawler/tool/preview agents); configured markers merge on top.
`AddAnalyticsConfiguration()` layers an optional watched `botdetector.json` (content root) for
restart-free additions.

## Stats endpoint

`GET /analytics/stats?days=&top=&includeBots=&site=` — mapped only when `Analytics:ApiKey` is set and
the store implements `IPageViewStatsStore`; the key travels in `X-Analytics-Key`. Recent rows
serialize as their runtime type, so custom entity columns appear. The route is not `AllowAnonymous` —
a fallback authorization policy applies on top of the key check.

## Gotchas

- **No store registered** → `UseAnalytics` logs a warning and adds no middleware; nothing is recorded.
- **Ordering is load-bearing**: after `UseForwardedHeaders` (else rows carry the proxy IP), before
  static files (else those requests are invisible). Behind HTTPS redirection, redirects are excluded by
  the default `ShouldRecord` (only 200/304), so visits are not double-counted.
- **Default filter is HTML-only**: GET + `Accept: text/html` + no dot in the last path segment, minus
  the built-in prefixes `/favicon`, `/.well-known`, `/robots.txt`, `/sitemap`, `/analytics` (prefix
  matches — `/sitemapping` is skipped too). API, RPC, or MCP traffic needs `WithFilter<T>()` or
  nothing is recorded.
- **Timing of hooks**: request-bound data must be taken in a contributor (in-request); by the time an
  enricher runs, the HttpContext is gone. Body reads need `EnableBuffering()` in `OnCapturing` — after
  the endpoint consumed the body it cannot be re-read otherwise.
- **Privacy stance**: no cookie, no session id; the full IP exists in memory only (enrichers), the
  store sees the masked form unless `MaskIpAddress` is switched off.
