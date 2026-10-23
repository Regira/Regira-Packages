using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Web.Security.Testing.Infrastructure;

public class TestingWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> 
    where TStartup : class
{
    protected override IHostBuilder CreateHostBuilder()
    {
        var builder = Host
            .CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseStartup<TStartup>()
                    .UseTestServer();
            });
        return builder;
    }

    /// <summary>
    /// Pinned, so the machine's <c>ASPNETCORE_ENVIRONMENT</c> cannot decide: the startups add the developer exception
    /// page in Development, which turns an exception a test expects into a 500 page. Here rather than in
    /// <see cref="CreateHostBuilder"/>, because this runs on the factory's own web host builder, the last to read the
    /// <c>ASPNETCORE_</c> variables.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment(Environments.Production);
}
