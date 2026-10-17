using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Regira.Security.Authentication.Jwt.Extensions;
#if NET10_0_OR_GREATER
using Regira.Security.Authentication.Web.OpenApi.Transformers;
#endif

namespace Web.Security.Testing.Infrastructure.Jwt;

public class JwtStartup(IConfiguration configuration)
{
    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddJwtAuthentication(o =>
            {
                o.Secret = string.Join(":", Enumerable.Range(0, 3).Select(_ => Guid.NewGuid().ToString("N")));
                o.LifeSpan = configuration.GetValue(TokenLifeSpan.Setting, TokenLifeSpan.Default);
            });

        services.AddControllersFor(typeof(AuthController), typeof(TestController));

#if NET10_0_OR_GREATER
        // AddOpenApi and the package's transformers exist on .NET 10 only; the rest of the host runs on .NET 8 too.
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<AuthenticationSchemeDocumentTransformer>();
            options.AddOperationTransformer<SecurityRequirementOperationTransformer>();
        });
#endif
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
#if NET10_0_OR_GREATER
            endpoints.MapOpenApi().AllowAnonymous();
#endif

            // A minimal endpoint takes the other branch of the transformer's metadata lookup: nothing carries an
            // ActionDescriptor, so the index misses and it reads ActionDescriptor.EndpointMetadata — which for a
            // minimal API *is* the endpoint's metadata, unlike a controller's.
            endpoints.MapGet("minimal/protected", () => "minimal, guarded").RequireAuthorization();
            endpoints.MapGet("minimal/public", () => "minimal, anonymous").AllowAnonymous();
        });
    }
}