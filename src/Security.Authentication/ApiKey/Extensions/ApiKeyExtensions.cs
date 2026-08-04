using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.ApiKey.Abstraction;
using Regira.Security.Authentication.ApiKey.Models;
using Regira.Security.Authentication.ApiKey.Services;
using Regira.Security.Authentication.Core.Abstraction;
using Regira.Security.Authentication.Core.Models;

namespace Regira.Security.Authentication.ApiKey.Extensions;

public static class ApiKeyExtensions
{
    public static AuthenticationBuilder AddApiKeyAuthentication(this IServiceCollection services, Action<ApiKeyAuthenticationOptions>? configure = null)
    {
        // Describes itself once, so a security document does not need a transformer class per scheme. The header
        // name is read from the configured options rather than the mutable static default.
        var described = new ApiKeyAuthenticationOptions();
        configure?.Invoke(described);
        services.AddSingleton<ISecuritySchemeDescriptor>(
            SecuritySchemeDescriptor.ApiKey(ApiKeyDefaults.AuthenticationScheme, described.ApiKeyHeaderName));

        return services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = ApiKeyDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = ApiKeyDefaults.AuthenticationScheme;
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyDefaults.AuthenticationScheme, configure ?? (c => { }));
    }

    public static AuthenticationBuilder AddInMemoryApiKeyAuthentication(this AuthenticationBuilder builder, IEnumerable<ApiKeyOwner> apiKeyOwners)
    {
        builder.Services
            .AddSingleton<IApiKeyOwnerService>(_ => new InMemoryApiKeyOwnerService(apiKeyOwners));

        return builder;
    }
}