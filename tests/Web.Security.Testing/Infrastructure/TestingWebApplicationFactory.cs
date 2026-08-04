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
}