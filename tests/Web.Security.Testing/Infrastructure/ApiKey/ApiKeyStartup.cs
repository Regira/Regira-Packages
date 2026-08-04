using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Regira.Security.Authentication.ApiKey.Extensions;

namespace Web.Security.Testing.Infrastructure.ApiKey;

public class ApiKeyStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddApiKeyAuthentication()
            .AddInMemoryApiKeyAuthentication(ApiKeyOwners.Value);

        services.AddControllersFor(typeof(TestController));
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints
                .MapControllers()
                .RequireAuthorization();
        });
    }
}