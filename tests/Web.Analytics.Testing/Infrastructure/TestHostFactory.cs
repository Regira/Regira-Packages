using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Web.Analytics.Testing.Infrastructure;

/// <summary>One TestServer-backed host shape for the middleware/endpoint integration tests.</summary>
public static class TestHostFactory
{
    public static Task<IHost> StartAsync(Dictionary<string, string?> settings,
        Action<WebHostBuilderContext, IServiceCollection> configureServices,
        Action<IApplicationBuilder> configureApp)
        => new HostBuilder()
            .ConfigureAppConfiguration(cfg => cfg.AddInMemoryCollection(settings))
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(configureServices)
                .Configure(configureApp))
            .StartAsync();

    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 10_000)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - start > timeoutMs)
                Assert.Fail($"Condition not met within {timeoutMs} ms");
            await Task.Delay(50);
        }
    }
}