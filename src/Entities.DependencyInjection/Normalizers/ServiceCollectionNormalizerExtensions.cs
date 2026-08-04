using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.Normalizing;
using Regira.Entities.Normalizing.Abstractions;
using Regira.Entities.Keywords;
using Regira.Entities.Keywords.Abstractions;
using Regira.Normalizing;
using Regira.Normalizing.Abstractions;
using Regira.Normalizing.Models;

namespace Regira.Entities.DependencyInjection.Normalizers;

public static class ServiceCollectionNormalizerExtensions
{
    // Normalizers
    public static IServiceCollection AddNormalizer<TNormalizer>(this IServiceCollection services)
        where TNormalizer : class, IEntityNormalizer
    {
        services.AddTransient<IEntityNormalizer, TNormalizer>();
        return services;
    }
    public static IServiceCollection AddNormalizer<TEntity, TNormalizer>(this IServiceCollection services)
        where TNormalizer : class, IEntityNormalizer<TEntity>
    {
        AddNormalizer<TNormalizer>(services);
        services.AddTransient<IEntityNormalizer<TEntity>, TNormalizer>();
        return services;
    }
    public static IServiceCollection AddNormalizer(this IServiceCollection services, Func<IServiceProvider, IEntityNormalizer> factory)
    {
        services.AddTransient(factory);
        return services;
    }
    public static IServiceCollection AddNormalizer<TEntity>(this IServiceCollection services, Func<IServiceProvider, IEntityNormalizer<TEntity>> factory)
    {
        services.AddTransient<IEntityNormalizer>(factory);
        services.AddTransient(factory);
        return services;
    }


    // EntityServiceCollectionOptions

    /// <summary> 
    /// Adds default normalizing services
    /// <list type="bullet">
    ///     <item><see cref="DefaultNormalizer"/></item>
    ///     <item><see cref="QKeywordHelper"/></item>
    ///     <item><see cref="ObjectNormalizer"/></item>
    ///     <item><see cref="DefaultEntityNormalizer"/></item>
    /// </list>
    /// </summary>
    /// <param name="options"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EntityServiceCollectionOptions AddDefaultEntityNormalizer(this EntityServiceCollectionOptions options, Action<NormalizeOptions> configure)
        => options.AddDefaultEntityNormalizer((_, o) => configure.Invoke(o));
    /// <summary> 
    /// Adds default normalizing services
    /// <list type="bullet">
    ///     <item><see cref="DefaultNormalizer"/></item>
    ///     <item><see cref="QKeywordHelper"/></item>
    ///     <item><see cref="ObjectNormalizer"/></item>
    ///     <item><see cref="DefaultEntityNormalizer"/></item>
    /// </list>
    /// </summary>
    /// <param name="options"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EntityServiceCollectionOptions AddDefaultEntityNormalizer(this EntityServiceCollectionOptions options, Action<IServiceProvider, NormalizeOptions>? configure = null)
    {
        if (configure == null)
        {
            options.Services.AddTransient<INormalizer, DefaultNormalizer>();
            options.Services.AddTransient<IObjectNormalizer, ObjectNormalizer>();
            options.AddNormalizer<DefaultEntityNormalizer>();
            options.AddDefaultQKeywordHelper();
            return options;
        }

        options.Services.AddTransient<INormalizer>(p =>
        {
            var nOptions = new NormalizingOptions();
            configure.Invoke(p, nOptions);
            return nOptions.DefaultNormalizer ?? new DefaultNormalizer(nOptions);
        });
        options.Services.AddTransient<IObjectNormalizer>(p =>
        {
            var nOptions = new NormalizingOptions();
            configure.Invoke(p, nOptions);
            return nOptions.DefaultObjectNormalizer ?? new ObjectNormalizer(nOptions);
        });

        options.Services.AddTransient<IEntityNormalizer, DefaultEntityNormalizer>();
        options.AddDefaultQKeywordHelper();

        return options;
    }

    public static EntityServiceCollectionOptions AddNormalizer<TNormalizer>(this EntityServiceCollectionOptions options)
        where TNormalizer : class, IEntityNormalizer
    {
        options.Services.AddNormalizer<TNormalizer>();
        return options;
    }
    public static EntityServiceCollectionOptions AddNormalizer<TEntity, TNormalizer>(this EntityServiceCollectionOptions options)
        where TNormalizer : class, IEntityNormalizer<TEntity>
    {
        options.Services.AddNormalizer<TEntity, TNormalizer>();
        return options;
    }

    public static EntityServiceCollectionOptions AddDefaultQKeywordHelper(this EntityServiceCollectionOptions options, QKeywordHelperOptions? qOptions = null)
    {
        options.Services.AddTransient<IQKeywordHelper>(p =>
        {
            var normalizer = qOptions?.ApplyNormalize ?? true ? p.GetService<INormalizer>() ?? new DefaultNormalizer() : null;
            return new QKeywordHelper(qOptions, normalizer);
        });
        return options;
    }
}