using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Regira.Security.Authentication.ApiKey.Extensions;
using Regira.Security.Authentication.Core.Extensions;
using Regira.Security.Authentication.Jwt.Extensions;
#if NET10_0_OR_GREATER
using Regira.Security.Authentication.Web.OpenApi.Transformers;
#endif
using Web.Security.Testing.Infrastructure.ApiKey;
using Web.Security.Testing.Infrastructure.Jwt;

namespace Web.Security.Testing.Infrastructure.Composition;

/// <summary>
/// One host serving both schemes behind the selector — the arrangement the guides describe and the only one that
/// can show a credential reaching the wrong handler.
/// </summary>
public class SchemeSelectorStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // API key is registered FIRST and JWT SECOND, deliberately. Each Add…Authentication sets its own default
        // scheme, so without the selector the later call would own the default and decide what an unattributed
        // [Authorize] authenticates against. Registering in the order that is *wrong* for the assertions below is
        // what makes them prove the selector took that decision over.
        services
            .AddApiKeyAuthentication()
            .AddInMemoryApiKeyAuthentication(ApiKeyOwners.Value);

        services
            .AddJwtAuthentication(o =>
                o.Secret = string.Join(":", Enumerable.Range(0, 3).Select(_ => Guid.NewGuid().ToString("N"))))
            .AddSchemeSelector();

        services.AddControllersFor(typeof(AuthController), typeof(TestController));

#if NET10_0_OR_GREATER
        // One document transformer for every scheme, from the descriptors they contributed at registration —
        // in place of BearerSecuritySchemeTransformer + ApiKeySecurityDocumentTransformer. .NET 10 only, like
        // AddOpenApi; the selector itself is tested on .NET 8 too.
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
        });
    }
}
