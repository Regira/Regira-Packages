using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Office.Word.Abstractions;

namespace Regira.Office.Word.Aspose.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="WordService"/> as <see cref="IWordService"/> and supplies it the
    /// Aspose.Words licence.
    /// </summary>
    /// <remarks>
    /// Named <c>AddAsposeWord</c> rather than <c>AddAspose</c> because Aspose also ships Excel, PDF and
    /// presentation libraries. The configuration is read through <see cref="IOptions{TOptions}"/>, so the
    /// service resolves from a singleton or a hosted service as well as from a request scope.
    /// </remarks>
    public static IServiceCollection AddAsposeWord(this IServiceCollection services, Action<AsposeWordConfig> configure)
    {
        services
            .Configure<AsposeWordConfig>(configure.Invoke)
            .AddTransient(p => p.GetRequiredService<IOptions<AsposeWordConfig>>().Value)
            .AddTransient<IWordService, WordService>();

        return services;
    }
}
