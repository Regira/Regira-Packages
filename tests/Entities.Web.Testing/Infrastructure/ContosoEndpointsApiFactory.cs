using Entities.TestApi.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace Entities.Web.Testing.Infrastructure;

/// <summary>
/// The test API with courses served by <c>MapEntityEndpoints()</c> instead of their controllers, so the course tests run
/// against the mapped endpoints as well.
/// </summary>
public class ContosoEndpointsApiFactory : ContosoApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting(ApiConfiguration.SurfaceKey, ApiConfiguration.EndpointsSurface);
    }
}
