using Regira.Entities.DependencyInjection.Normalizers;
using Regira.Entities.DependencyInjection.Preppers;
using Regira.Entities.DependencyInjection.Primers;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;
using Regira.Normalizing.Models;

namespace Regira.Entities.DependencyInjection.Extensions;

public static class EntityServiceCollectionExtensions
{
    /// <summary>
    /// <inheritdoc cref="ServiceCollectionPrimerExtensions.AddDefaultPrimers"/>
    /// <inheritdoc cref="ServiceCollectionPrepperExtensions.AddDefaultPreppers"/>
    /// <inheritdoc cref="ServiceCollectionNormalizerExtensions.AddDefaultEntityNormalizer(EntityServiceCollectionOptions,Action{NormalizeOptions})"/>
    /// <inheritdoc cref="ServiceCollectionQueryFilterExtensions.AddDefaultGlobalQueryFilters"/>
    /// Also calls <see cref="EntityServiceCollectionOptions.AddDefaultInterceptors"/>, so
    /// <c>UseEntities&lt;TContext&gt;()</c> wires the interceptors and the UTC date convention into the DbContext options.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EntityServiceCollectionOptions UseDefaults(this EntityServiceCollectionOptions options, Action<EntityDefaultNormalizingOptions>? configure = null)
    {
        var entityDefaultOptions = new EntityDefaultNormalizingOptions();

        // Paging: DefaultPageSize applies when the caller omits paging; MaxPageSize caps every request
        // (and is what a pageSize <= 0 opt-out falls back to).
        options.DefaultPageSize = 10;
        options.MaxPageSize = 100;
        // UTC date handling is on by default process-wide (DateTimeDefaults.UseUtc) — no need to set it here
        // DbContext plumbing (interceptors + UTC convention) is wired automatically by UseEntities<TContext>()
        options.AddDefaultInterceptors();

        // must run before the normalizer branch below — it consumes ConfigureNormalizingFunc
        configure?.Invoke(entityDefaultOptions);

        options.AddDefaultPrimers();
        options.AddDefaultPreppers();
        if (entityDefaultOptions.ConfigureNormalizingFunc != null)
        {
            options.AddDefaultEntityNormalizer(entityDefaultOptions.ConfigureNormalizingFunc);
        }
        else
        {
            options.AddDefaultEntityNormalizer();
            options.AddDefaultGlobalFilter<FilterHasNormalizedContentQueryBuilder>();
        }
        options.AddDefaultGlobalQueryFilters();

        return options;
    }
}