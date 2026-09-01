using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regira.Web.Analytics.Models;
using Regira.Web.Analytics.Services;

namespace Regira.Web.Analytics;

/// <summary>
/// Registration surface returned by <c>AddAnalytics</c>. When analytics is disabled by configuration,
/// every method is a no-op.
/// </summary>
public class AnalyticsBuilder<TPageView>(IServiceCollection services, bool enabled)
    where TPageView : class, IPageView, new()
{
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Registers the persistence hook — scoped, so a store may take scoped dependencies (a DbContext);
    /// stats and retention interfaces are registered too when <typeparamref name="TStore"/> implements
    /// them. Pre-register <typeparamref name="TStore"/> yourself to pick another lifetime.
    /// </summary>
    public AnalyticsBuilder<TPageView> WithStore<TStore>()
        where TStore : class, IPageViewStore<TPageView>
    {
        if (!enabled)
            return this;

        Services.TryAddScoped<TStore>();
        Services.AddScoped<IPageViewStore<TPageView>>(sp => sp.GetRequiredService<TStore>());

        if (typeof(IPageViewStatsStore).IsAssignableFrom(typeof(TStore)))
            Services.AddScoped(sp => (IPageViewStatsStore)sp.GetRequiredService<TStore>());
        if (typeof(IPageViewRetentionStore).IsAssignableFrom(typeof(TStore)))
            Services.AddScoped(sp => (IPageViewRetentionStore)sp.GetRequiredService<TStore>());

        return this;
    }

    /// <summary>Factory variant; stats/retention interfaces are not auto-wired here — register them yourself.</summary>
    public AnalyticsBuilder<TPageView> WithStore(Func<IServiceProvider, IPageViewStore<TPageView>> factory)
    {
        if (!enabled)
            return this;

        Services.AddScoped(factory);
        return this;
    }

    /// <summary>Replaces the default <see cref="HtmlPageVisitFilter"/>.</summary>
    public AnalyticsBuilder<TPageView> WithFilter<TFilter>()
        where TFilter : class, IVisitFilter
    {
        if (!enabled)
            return this;

        Services.Replace(ServiceDescriptor.Singleton<IVisitFilter, TFilter>());
        return this;
    }

    /// <summary>
    /// Adds an in-request hook; contributors run in registration order. Singleton — the middleware
    /// constructor-injects them from the root provider, so scoped dependencies don't fit here.
    /// </summary>
    public AnalyticsBuilder<TPageView> AddContributor<TContributor>()
        where TContributor : class, IVisitContributor<TPageView>
    {
        if (!enabled)
            return this;

        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IVisitContributor<TPageView>, TContributor>());
        return this;
    }

    /// <summary>Adds a background enricher; enrichers run in registration order. Scoped, like the store.</summary>
    public AnalyticsBuilder<TPageView> AddEnricher<TEnricher>()
        where TEnricher : class, IPageViewEnricher<TPageView>
    {
        if (!enabled)
            return this;

        Services.TryAddEnumerable(ServiceDescriptor.Scoped<IPageViewEnricher<TPageView>, TEnricher>());
        return this;
    }
}