# Regira.Web.Analytics — examples

## Minimal HTML-site setup

The default filter tracks browser page loads (GET, `Accept: text/html`, no file extension). All the
host must supply is persistence:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddAnalyticsConfiguration();                    // optional watched botdetector.json
builder.Services.AddRazorPages();
builder.Services.AddAnalytics(builder.Configuration)
    .WithStore<FilePageViewStore>();

var app = builder.Build();
app.UseForwardedHeaders();
app.UseAnalytics();                                     // before UseStaticFiles / SPA fallback
app.UseStaticFiles();
app.MapRazorPages();
app.MapAnalyticsEndpoints();                            // only maps when Analytics:ApiKey is set
app.Run();
```

## A store — JSON-lines file, no dependencies

```csharp
public class FilePageViewStore(IHostEnvironment env) : IPageViewStore<PageView>
{
    public async Task SaveAsync(IReadOnlyList<PageView> views, CancellationToken cancellationToken = default)
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

Implement `IPageViewRetentionStore` and/or `IPageViewStatsStore` on the same class to opt into
retention and the stats endpoint — `WithStore<T>()` wires them automatically. Stores and enrichers are
registered scoped, so a database-backed store can take a DbContext directly. A base-entity store that
serves subclasses through the contravariant `IPageViewStore<in T>` (as below) must persist by runtime
type, the way this one does — the stats endpoint serializes its recent rows the same way.

For a custom entity carrying geolocation columns filled by a background enricher, see the geolocation
sample in the package README.

## Custom filter + contributor — tracking an RPC endpoint

The default filter would skip non-HTML traffic entirely. Replace it, and read request-bound data in a
contributor (`OnCapturing` runs before the endpoint — the only chance to enable body buffering;
`OnCapturedAsync` runs after the response, when the buffered body and `HttpContext.Items` exist):

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
    .WithStore<FilePageViewStore>()   // IPageViewStore<in T>: the base-entity store serves the subclass
    .WithFilter<RpcVisitFilter>()
    .AddContributor<RpcContributor>();
```

## Reading the stats

```bash
curl "https://example.com/analytics/stats?days=30&top=20&includeBots=true" -H "X-Analytics-Key: <key>"
```

Set the key out of source control:

```bash
dotnet user-secrets set "Analytics:ApiKey" "<random>"
```
