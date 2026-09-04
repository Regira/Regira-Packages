using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Web.Analytics.Config;
using Regira.Web.Analytics.Models;
using Regira.Web.Analytics.Services;

namespace Regira.Web.Analytics.Endpoints;

public static class AnalyticsEndpoints
{
    private const string KeyHeader = "X-Analytics-Key";

    /// <summary>
    /// Maps GET /analytics/stats — only when Analytics:ApiKey is configured and an
    /// <see cref="IPageViewStatsStore"/> is registered; otherwise the route is not mapped at all.
    /// </summary>
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var config = app.ServiceProvider.GetRequiredService<AnalyticsConfig>();

        if (!config.Enabled || string.IsNullOrWhiteSpace(config.ApiKey))
            return app;

        if (!AnalyticsExtensions.IsRegistered(app.ServiceProvider, typeof(IPageViewStatsStore)))
            return app;

        var jsonOptions = BuildSerializerOptions(app.ServiceProvider);

        app.MapGet("/analytics/stats",
                (HttpContext httpContext, IPageViewStatsStore store, AnalyticsConfig analyticsConfig,
                    int days = 30, bool includeBots = false, int top = 20, string? site = null)
                    => GetStats(httpContext, store, analyticsConfig, jsonOptions, days, includeBots, top, site))
            .WithName("GetAnalyticsStats")
            .ExcludeFromDescription();

        return app;
    }

    private static async Task<IResult> GetStats(HttpContext httpContext, IPageViewStatsStore store,
        AnalyticsConfig config, JsonSerializerOptions jsonOptions,
        int days, bool includeBots, int top, string? site)
    {
        var providedKey = httpContext.Request.Headers[KeyHeader].ToString();
        if (!KeysMatch(providedKey, config.ApiKey!))
            return Results.Unauthorized();

        days = Math.Clamp(days, 1, 730);
        top = Math.Clamp(top, 1, 100);
        var since = DateTime.UtcNow.Date.AddDays(-days + 1);

        // Answer for this site unless asked otherwise; "*" spans all sites in a shared store.
        var siteName = string.IsNullOrWhiteSpace(site) ? config.SiteName : site.Trim();

        var stats = await store.GetStatsAsync(new PageViewStatsQuery
        {
            SinceUtc = since,
            SiteName = siteName == "*" ? null : siteName,
            IncludeBots = includeBots,
            Top = top
        }, httpContext.RequestAborted);

        return Results.Json(new
        {
            site = siteName,
            since,
            days,
            stats
        }, jsonOptions);
    }

    /// <summary>Constant-time comparison over fixed-length hashes, so the key cannot be probed by timing.</summary>
    private static bool KeysMatch(string provided, string expected)
    {
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    /// <summary>The host's JSON options plus the runtime-type converter for <c>Recent</c>.</summary>
    private static JsonSerializerOptions BuildSerializerOptions(IServiceProvider provider)
    {
        var hostOptions = provider.GetService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()?.Value.SerializerOptions;
        var options = hostOptions != null
            ? new JsonSerializerOptions(hostOptions)
            : new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(RuntimeTypePageViewConverter.Instance);
        return options;
    }

    /// <summary>
    /// Serializes an <see cref="IPageView"/> as its runtime type, so a custom entity's own columns
    /// appear in the stats response instead of being cut down to the interface.
    /// </summary>
    private sealed class RuntimeTypePageViewConverter : JsonConverter<IPageView>
    {
        public static readonly RuntimeTypePageViewConverter Instance = new();

        // Exact match only: the concrete type must serialize normally, or Write recurses forever.
        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(IPageView);

        public override IPageView Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, IPageView value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}