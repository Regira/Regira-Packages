using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regira.Web.Analytics.Config;
using Regira.Web.Analytics.Middleware;
using Regira.Web.Analytics.Models;
using Regira.Web.Analytics.Services;

namespace Regira.Web.Analytics;

/// <summary>
/// Host entry points: <see cref="AddAnalyticsConfiguration"/>, <see cref="AddAnalytics{TPageView}"/>,
/// <see cref="UseAnalytics"/> (+ <c>MapAnalyticsEndpoints</c>). Ordering contract: UseAnalytics after
/// UseForwardedHeaders (this package never configures forwarded headers itself) and before static files.
/// </summary>
public static class AnalyticsExtensions
{
    public const string SectionName = "Analytics";

    private const string BotDetectorFileName = "botdetector.json";

    /// <summary>Adds an optional, watched botdetector.json from the content root, for restart-free marker edits.</summary>
    public static IHostApplicationBuilder AddAnalyticsConfiguration(this IHostApplicationBuilder builder)
    {
        var path = Path.Combine(builder.Environment.ContentRootPath, BotDetectorFileName);
        builder.Configuration.AddJsonFile(path, optional: true, reloadOnChange: true);
        return builder;
    }

    /// <summary>Registers visit tracking with the default <see cref="PageView"/> entity.</summary>
    public static AnalyticsBuilder<PageView> AddAnalytics(this IServiceCollection services,
        IConfiguration configuration, Action<AnalyticsConfig>? configure = null)
        => services.AddAnalytics<PageView>(configuration, configure);

    /// <summary>
    /// Registers visit tracking, bound from the Analytics section, with a custom entity type. Chain the
    /// store and hooks off the returned builder. One entity type per host; a second call naming a
    /// different one throws.
    /// </summary>
    public static AnalyticsBuilder<TPageView> AddAnalytics<TPageView>(this IServiceCollection services,
        IConfiguration configuration, Action<AnalyticsConfig>? configure = null)
        where TPageView : class, IPageView, new()
    {
        var existing = services
            .FirstOrDefault(d => d.ServiceType == typeof(AnalyticsRegistration))?
            .ImplementationInstance as AnalyticsRegistration;
        if (existing != null && existing.ViewType != typeof(TPageView))
            throw new InvalidOperationException(
                $"AddAnalytics was already called for {existing.ViewType.Name}; one analytics pipeline per host.");

        var config = configuration.GetSection(SectionName).Get<AnalyticsConfig>() ?? new AnalyticsConfig();
        configure?.Invoke(config);
        config.SiteName = ResolveSiteName(config.SiteName);

        var builder = new AnalyticsBuilder<TPageView>(services, config.Enabled);

        // A repeated call with the same entity changes nothing.
        if (existing != null)
            return builder;

        services.AddSingleton(config);

        if (!config.Enabled)
            return builder;

        services.AddSingleton(new AnalyticsRegistration
        {
            ViewType = typeof(TPageView),
            MiddlewareType = typeof(VisitTrackingMiddleware<TPageView>),
            StoreServiceType = typeof(IPageViewStore<TPageView>)
        });

        services.Configure<BotDetectionConfig>(configuration.GetSection($"{SectionName}:BotDetection"));
        services.AddSingleton<BotDetector>();

        services.AddSingleton<PageViewQueue<TPageView>>();
        services.TryAddSingleton<IVisitFilter, HtmlPageVisitFilter>();
        services.AddHostedService<PageViewWriter<TPageView>>();

        return builder;
    }

    /// <summary>Puts visit tracking in the pipeline. Without a registered store this warns and adds nothing.</summary>
    public static IApplicationBuilder UseAnalytics(this IApplicationBuilder app)
    {
        var config = app.ApplicationServices.GetRequiredService<AnalyticsConfig>();
        if (!config.Enabled)
            return app;

        var registration = app.ApplicationServices.GetRequiredService<AnalyticsRegistration>();
        if (!IsRegistered(app.ApplicationServices, registration.StoreServiceType))
        {
            app.ApplicationServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Regira.Web.Analytics")
                .LogWarning("Analytics: no {StoreType} registered — visit tracking is inactive",
                    registration.StoreServiceType.Name);
            return app;
        }

        app.UseMiddleware(registration.MiddlewareType);
        return app;
    }

    /// <summary>Configured name, or the entry assembly name; truncated to 64 chars.</summary>
    private static string ResolveSiteName(string? configured)
    {
        var name = configured?.Trim();
        if (string.IsNullOrEmpty(name))
            name = Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown";

        return name.Length <= 64 ? name : name[..64];
    }

    /// <summary>Registration check that must not instantiate anything (a scoped store would throw from the root).</summary>
    internal static bool IsRegistered(IServiceProvider provider, Type serviceType)
    {
        var query = provider.GetService<IServiceProviderIsService>();
        return query?.IsService(serviceType) ?? provider.GetService(serviceType) != null;
    }
}