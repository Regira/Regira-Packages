using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Regira.System.Hosting.Extensions;

public static class HostBuilderExtensions
{
    /// <summary>
    /// Forces the UserSecrets to be inserted just before the Environment variables
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IHostBuilder InsertUserSecrets(this IHostBuilder builder)
    {
        return builder.ConfigureAppConfiguration((context, config) =>
        {
            var envSource = config.Sources.LastOrDefault(x => x is EnvironmentVariablesConfigurationSource);
            var envIndex = envSource != null
                ? config.Sources.IndexOf(envSource)
                : config.Sources.Count - 1;

            var tempConfig = new ConfigurationBuilder();
            IHostEnvironment env = context.HostingEnvironment;
            if (!string.IsNullOrEmpty(env.ApplicationName))
            {
                var appAssembly = Assembly.Load(new AssemblyName(env.ApplicationName));
                tempConfig.AddUserSecrets(appAssembly, optional: true);
            }

            if (tempConfig.Sources.Any())
            {
                envIndex = Math.Max(envIndex, 0); // no negative value
                config.Sources.Insert(envIndex, tempConfig.Sources.First());
            }
            else
            {
                // Create custom logger since we don't have access to DI container yet
                var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
                var logger = loggerFactory.CreateLogger("InsertUserSecrets");
                logger.LogWarning("Could not add UserSecrets");
            }
        });
    }

    /// <summary>
    /// Inserts custom parameters at the given index in <see cref="IConfigurationSource">Configuration</see> <see cref="IList{T}">Sources</see> as a <see cref="MemoryConfigurationSource" />
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="buildParameters"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public static IHostBuilder AddCustomParameters(this IHostBuilder builder, Func<HostBuilderContext, IConfigurationBuilder, IDictionary<string, string>> buildParameters, int index = 0)
    {
        return builder
            .ConfigureAppConfiguration((context, config) =>
            {
                var parameters = buildParameters(context, config);

                if (parameters.Any())
                {
                    var memoryConfig = new ConfigurationBuilder()
                        .AddInMemoryCollection(parameters!);

                    config.Sources.Insert(index, memoryConfig.Sources.First());
                }
            });
    }
}