using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Regira.System.Hosting.HostOptions.Abstractions;
using Regira.System.Hosting.HostOptions.Constants;

namespace Regira.System.Hosting.HostOptions.Extensions;

public static class HostBuilderExtensions
{
    /// <summary>
    /// Configure <see cref="HostOptions" />
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IHostBuilder UseHostOptions(this IHostBuilder builder)
    {
        return builder.ConfigureServices((context, services) =>
        {
            var hostingSection = context.Configuration.GetSection(HostingDefaults.HostingSectionName);
            var hostOptions = hostingSection.Get<Models.HostOptions>() ?? new Models.HostOptions();
            services
                .Configure<Models.HostOptions>(hostingSection)
                .AddTransient(p => p.GetRequiredService<IOptionsSnapshot<Models.HostOptions>>().Value) // only accessible as scoped service
                .AddTransient<IHostOptions>(_ => hostOptions); // can be retrieved without scope
        });
    }
}