#if NET9_0_OR_GREATER
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
#if NET10_0_OR_GREATER
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif
using Regira.Security.Authentication.Core.Abstraction;
using Regira.Security.Authentication.Core.Models;

namespace Regira.Security.Authentication.Web.OpenApi.Transformers;

/// <summary>
/// Declares every registered Regira authentication scheme under <c>components.securitySchemes</c>, from the
/// descriptors the schemes contributed at registration.
/// <para>
/// One transformer instead of one per scheme. Registering it alongside the older
/// <see cref="BearerSecuritySchemeTransformer"/> / <see cref="ApiKeySecurityDocumentTransformer"/> is harmless —
/// all three insert with <c>TryAdd</c>, so whichever runs first wins and the result is identical.
/// </para>
/// </summary>
public class AuthenticationSchemeDocumentTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider, IEnumerable<ISecuritySchemeDescriptor> descriptors) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        // A descriptor names a scheme it *expects* to be registered; a host that configured the options but never
        // added the scheme must not have it advertised.
        var registered = (await authenticationSchemeProvider.GetAllSchemesAsync())
            .Select(scheme => scheme.Name)
            .ToHashSet(StringComparer.Ordinal);

        var applicable = descriptors
            .Where(descriptor => registered.Contains(descriptor.AuthenticationScheme))
            .ToArray();

        if (applicable.Length == 0)
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
#if NET10_0_OR_GREATER
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
#else
        document.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();
#endif

        foreach (var descriptor in applicable)
        {
            document.Components.SecuritySchemes.TryAdd(descriptor.AuthenticationScheme, ToOpenApi(descriptor));
        }
    }

    private static OpenApiSecurityScheme ToOpenApi(ISecuritySchemeDescriptor descriptor) => descriptor.Kind switch
    {
        SecuritySchemeKind.Http => new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = descriptor.HttpScheme,
            In = ParameterLocation.Header,
            BearerFormat = string.Equals(descriptor.HttpScheme, "bearer", StringComparison.OrdinalIgnoreCase) ? "Json Web Token" : null,
            Description = descriptor.Description
        },
        SecuritySchemeKind.ApiKey => new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = descriptor.ParameterName,
            Description = descriptor.Description
        },
        // OpenAPI has no cookie security type; an API key located in the cookie is the accepted convention.
        SecuritySchemeKind.Cookie => new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = descriptor.ParameterName,
            Description = descriptor.Description
        },
        SecuritySchemeKind.OpenIdConnect => new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OpenIdConnect,
            OpenIdConnectUrl = new Uri(descriptor.OpenIdConnectUrl!),
            Description = descriptor.Description
        },
        _ => throw new NotSupportedException($"Unsupported {nameof(SecuritySchemeKind)}: {descriptor.Kind}.")
    };
}
#endif
