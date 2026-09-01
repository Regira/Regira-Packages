# Regira Web.Analytics — examples

## Minimal setup (HTML site, default entity)

```csharp
using Regira.Web.Analytics;
using Regira.Web.Analytics.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.AddAnalyticsConfiguration();
builder.Services.AddRazorPages();
builder.Services.AddAnalytics(builder.Configuration)
    .WithStore<JsonLinesPageViewStore>();

var app = builder.Build();
app.UseForwardedHeaders();
app.UseAnalytics();
app.UseStaticFiles();
app.MapRazorPages();
app.MapAnalyticsEndpoints();
app.Run();
```

```json
"Analytics": {
  "SiteName": "MySite",
  "IgnorePaths": [ "/api", "/css", "/js" ]
}
```

## Zero-dependency store (JSON lines)

```csharp
using Regira.Web.Analytics.Models;
using Regira.Web.Analytics.Services;

public class JsonLinesPageViewStore(IHostEnvironment env) : IPageViewStore<PageView>
{
    public async Task SaveAsync(IReadOnlyList<PageView> views, CancellationToken ct = default)
    {
        var path = Path.Combine(env.ContentRootPath, "App_Data", "pageviews.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);
        foreach (var view in views)
            // By runtime type: serializing the declared PageView would drop a subclass's own columns.
            await writer.WriteLineAsync(JsonSerializer.Serialize(view, view.GetType()));
    }
}
```

Implement `IPageViewStatsStore` / `IPageViewRetentionStore` on the same class and
`WithStore<JsonLinesPageViewStore>()` registers all three. A base-entity store reused for a subclass via
the contravariant `IPageViewStore<in T>` must persist by runtime type, as above.

## Custom entity + contributor + background enricher

```csharp
public class SitePageView : PageView
{
    [MaxLength(32)]  public string? Experiment { get; set; }   // A/B variant, from the request
    [MaxLength(128)] public string? Network { get; set; }      // ISP / hosting provider, from the IP
}

public interface INetworkLookup
{
    Task<string?> FindAsync(IPAddress? ip, CancellationToken ct = default);   // your resolver
}

public class ExperimentContributor : IVisitContributor<SitePageView>
{
    public ValueTask OnCapturedAsync(HttpContext context, SitePageView view)
    {
        view.Experiment = context.Request.Cookies["ab-variant"];
        return ValueTask.CompletedTask;
    }
}

public class NetworkEnricher(INetworkLookup lookup) : IPageViewEnricher<SitePageView>
{
    public async ValueTask EnrichAsync(PendingPageView<SitePageView> pending, CancellationToken ct = default)
        => pending.View.Network = await lookup.FindAsync(pending.ClientIp, ct);   // unmasked address, memory only
}
```

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAnalytics<SitePageView>(builder.Configuration)
    .WithStore<JsonLinesPageViewStore>()   // IPageViewStore<in T>: the base-entity store serves the subclass
    .AddContributor<ExperimentContributor>()
    .AddEnricher<NetworkEnricher>();
```

Geolocation is the same shape but ships ready-made: add `Regira.Web.Analytics.GeoIP2`, implement
`IGeoPageView` on the entity (or use its `GeoPageView`) and call `.AddGeoIP2(builder.Configuration)`.

## Non-HTML traffic: custom filter + in-request contributor

```csharp
public class RpcVisitFilter : IVisitFilter
{
    public bool ShouldTrack(HttpRequest request)
        => HttpMethods.IsPost(request.Method) && request.Path == "/rpc";
    public bool ShouldRecord(HttpContext context)
        => context.Response.StatusCode is 200 or 202;
}

public class RpcPageView : PageView
{
    [MaxLength(64)] public string? Operation { get; set; }
}

public class RpcContributor : IVisitContributor<RpcPageView>
{
    public void OnCapturing(HttpContext context, RpcPageView view)
        => context.Request.EnableBuffering();          // before the endpoint reads the body

    public async ValueTask OnCapturedAsync(HttpContext context, RpcPageView view)
    {
        if (!context.Request.Body.CanSeek)
            return;
        context.Request.Body.Position = 0;
        try
        {
            using var doc = await JsonDocument.ParseAsync(context.Request.Body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("method", out var method))
                view.Operation = method.GetString();
        }
        catch (JsonException)
        {
            // malformed body — the visit still counts, just without the operation
        }
    }
}
```

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAnalytics<RpcPageView>(builder.Configuration)
    .WithStore<JsonLinesPageViewStore>()
    .WithFilter<RpcVisitFilter>()
    .AddContributor<RpcContributor>();
```

## Stats endpoint

```bash
dotnet user-secrets set "Analytics:ApiKey" "<random>"
curl "https://example.com/analytics/stats?days=30&top=20&includeBots=true" -H "X-Analytics-Key: <random>"
```

Response: `{ site, since, days, stats: { humanViews, botViews, perDay, topPaths, topReferrers,
perSite, recent, breakdowns } }` — `breakdowns` holds the store's own dimensions (`country`, `tool`, ...);
`recent` rows serialize as their runtime type.
