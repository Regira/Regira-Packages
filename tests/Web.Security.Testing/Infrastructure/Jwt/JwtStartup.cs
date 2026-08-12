using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Regira.Security.Authentication.Jwt.Extensions;
using Regira.Security.Authentication.Web.OpenApi.Transformers;

namespace Web.Security.Testing.Infrastructure.Jwt;

public class JwtStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddJwtAuthentication(o =>
            {
                o.Secret = string.Join(":", Enumerable.Range(0, 3).Select(_ => Guid.NewGuid().ToString("N")));
                o.LifeSpan = 2;
            });

        services.AddControllersFor(typeof(AuthController), typeof(TestController));

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<SecurityRequirementOperationTransformer>();
        });
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
            endpoints.MapOpenApi().AllowAnonymous();

            // A minimal endpoint takes the other branch of the transformer's metadata lookup: nothing carries an
            // ActionDescriptor, so the index misses and it reads ActionDescriptor.EndpointMetadata — which for a
            // minimal API *is* the endpoint's metadata, unlike a controller's.
            endpoints.MapGet("minimal/protected", () => "minimal, guarded").RequireAuthorization();
            endpoints.MapGet("minimal/public", () => "minimal, anonymous").AllowAnonymous();
        });
    }
}