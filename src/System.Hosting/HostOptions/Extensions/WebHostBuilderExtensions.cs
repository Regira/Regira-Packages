using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Regira.System.Hosting.HostOptions.Abstractions;
using Regira.System.Hosting.HostOptions.Constants;
using Regira.System.Hosting.HostOptions.Models;

namespace Regira.System.Hosting.HostOptions.Extensions;

public static class WebHostBuilderExtensions
{
    /// <summary>
    /// Configure <see cref="WebHostOptions" />.
    /// Also overrides the url port by <see cref="WebHostOptions.LocalPort" /> when present by adding the value for 'UseUrls' in a <see cref="MemoryConfigurationSource" />
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IHostBuilder UseWebHostOptions(this IHostBuilder builder)
    {
        return builder
            .ConfigureAppConfiguration((_, configBuilder) =>
            {
                var config = configBuilder.Build();
                var localPort = config.GetValue<int?>($"{HostingDefaults.HostingSectionName}:LocalPort");
                if (localPort.HasValue)
                {
                    var memoryConfig = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string> { ["urls"] = $"http://*:{localPort}" }!)
                        .Build(); // this config replaces .UseUrls from the webBuilder in ConfigureWebHostDefaults
                    configBuilder.AddConfiguration(memoryConfig);
                }
            })
            .UseHostOptions()
            .ConfigureServices((context, services) =>
            {
                var hostingSection = context.Configuration.GetSection(HostingDefaults.HostingSectionName);
                var hostOptions = hostingSection.Get<WebHostOptions>() ?? new WebHostOptions();
                services
                    .Configure<WebHostOptions>(hostingSection)
                    .AddTransient(p => p.GetRequiredService<IOptionsSnapshot<WebHostOptions>>().Value) // only accessible as scoped service
                    .AddTransient<IWebHostOptions>(_ => hostOptions); // can be retrieved without scope
            });
    }
}