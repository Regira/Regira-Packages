using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Office.Word.Abstractions;

namespace Regira.Office.Word.Syncfusion.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="WordService"/> as <see cref="IWordService"/> and supplies it the
    /// Syncfusion license key.
    /// </summary>
    /// <remarks>
    /// Named <c>AddSyncfusionWord</c> rather than <c>AddSyncfusion</c> because Syncfusion also
    /// ships Excel and PDF libraries.
    /// </remarks>
    public static IServiceCollection AddSyncfusionWord(this IServiceCollection services, Action<SyncfusionWordConfig> configure)
    {
        services
            .Configure<SyncfusionWordConfig>(configure.Invoke)
            .AddTransient(p => p.GetRequiredService<IOptionsSnapshot<SyncfusionWordConfig>>().Value)
            .AddTransient<IWordService, WordService>();

        return services;
    }
}
