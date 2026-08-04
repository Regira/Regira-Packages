#if NET9_0_OR_GREATER
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regira.Security.Authentication.Core.Abstraction;
#if NET10_0_OR_GREATER
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif

namespace Regira.Security.Authentication.Web.OpenApi.Transformers;

/// <summary>
/// Marks every guarded operation as requiring its authentication scheme.
/// <para>
/// The document transformers declare the schemes under <c>components.securitySchemes</c>, which is what makes
/// the Scalar/Swagger auth prompt appear — but a declared scheme says nothing about <em>which</em> endpoints
/// need it. Without a per-operation requirement a generated client cannot tell a public endpoint from a
/// guarded one, and the document reads as if the whole API were anonymous. Register it alongside them:
/// <code>
/// options.AddDocumentTransformer&lt;BearerSecuritySchemeTransformer&gt;();
/// options.AddOperationTransformer&lt;SecurityRequirementOperationTransformer&gt;();
/// </code>
/// </para>
/// An operation is guarded when its endpoint carries <see cref="IAuthorizeData"/> and no
/// <see cref="IAllowAnonymous"/> — the same reading the authorization middleware performs, so a
/// <c>MapControllers().RequireAuthorization()</c> app with a few <c>[AllowAnonymous]</c> actions is described
/// accurately. The scheme named on <c>[Authorize(AuthenticationSchemes = …)]</c> wins; otherwise the default
/// authenticate scheme is used.
/// <para>
/// ⚠️ A resolved scheme is run through <see cref="IAuthenticationSchemeExpander"/> before it is written out,
/// because the default scheme is not necessarily one that authenticates. A policy scheme — what
/// <c>AddSchemeSelector</c> registers, and the shape any multi-scheme host ends up with — forwards instead, so
/// it is a registered scheme that no document transformer declares. Naming it directly emits a requirement
/// referencing a <c>securitySchemes</c> entry that does not exist, and the auth prompt disappears from the
/// generated document while every operation still claims to need a credential.
/// </para>
/// </summary>
public class SecurityRequirementOperationTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider,
    ILogger<SecurityRequirementOperationTransformer>? logger = null,
    IAuthenticationSchemeExpander? schemeExpander = null) : IOpenApiOperationTransformer
{
    private IReadOnlyDictionary<string, Endpoint>? _endpointsByActionId;

    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = GetEndpointMetadata(context);
        // AllowAnonymous anywhere in the metadata wins, exactly as AuthorizationMiddleware treats it.
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        var authorizeData = metadata.OfType<IAuthorizeData>().ToArray();
        if (authorizeData.Length == 0)
        {
            return;
        }

        var schemeNames = await GetSchemeNames(authorizeData);
        if (schemeNames.Count == 0)
        {
            // Silence here would reproduce the very gap this transformer closes: an operation that needs a
            // token, described as if it were public. It means no authentication scheme is registered (or none
            // is the default), which is an app-wiring problem, not something to paper over.
            logger?.LogWarning(
                "OpenAPI: operation {Operation} requires authorization but no authentication scheme resolved, so it is described as anonymous. " +
                "Register the scheme, or name it on [Authorize(AuthenticationSchemes = …)].",
                context.Description.ActionDescriptor.DisplayName ?? context.Description.RelativePath);
            return;
        }

        operation.Security ??= [];
        foreach (var schemeName in schemeNames)
        {
#if NET10_0_OR_GREATER
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                // The host document is what lets the reference serialize as
                // '#/components/securitySchemes/<name>'; without it the requirement writes out empty.
                [new OpenApiSecuritySchemeReference(schemeName, context.Document)] = []
            });
#else
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = schemeName }
                }] = []
            });
#endif
        }
    }

    /// <summary>
    /// The metadata of the endpoint behind the operation.
    /// <para>
    /// ⚠️ Read from the endpoint, not from <c>ActionDescriptor.EndpointMetadata</c>: for a controller the
    /// latter holds attribute-derived metadata only, so a global
    /// <c>MapControllers().RequireAuthorization()</c> — the way a Regira API is normally guarded — is
    /// invisible there and every operation would be described as anonymous. Minimal APIs fall back to the
    /// action descriptor, whose metadata is the endpoint's.
    /// </para>
    /// </summary>
    private IEnumerable<object> GetEndpointMetadata(OpenApiOperationTransformerContext context)
    {
        var actionDescriptor = context.Description.ActionDescriptor;
        _endpointsByActionId ??= BuildEndpointIndex(context.ApplicationServices);

        return _endpointsByActionId.TryGetValue(actionDescriptor.Id, out var endpoint)
            ? endpoint.Metadata
            : actionDescriptor.EndpointMetadata;
    }

    /// <summary>Endpoints indexed by the id of the action they were built from — an identity match, no route-string comparison.</summary>
    private static IReadOnlyDictionary<string, Endpoint> BuildEndpointIndex(IServiceProvider services)
    {
        var endpoints = services.GetService<EndpointDataSource>()?.Endpoints;
        if (endpoints == null)
        {
            return new Dictionary<string, Endpoint>();
        }

        return endpoints
            .Select(endpoint => (endpoint, actionId: endpoint.Metadata.GetMetadata<ActionDescriptor>()?.Id))
            .Where(x => x.actionId != null)
            .GroupBy(x => x.actionId!)
            .ToDictionary(group => group.Key, group => group.First().endpoint, StringComparer.Ordinal);
    }

    /// <summary>
    /// The schemes an operation accepts, restricted to schemes that are actually registered — a requirement
    /// naming a scheme the document never declares is a dangling reference.
    /// </summary>
    private async Task<IReadOnlyList<string>> GetSchemeNames(IEnumerable<IAuthorizeData> authorizeData)
    {
        var registered = (await authenticationSchemeProvider.GetAllSchemesAsync())
            .Select(scheme => scheme.Name)
            .ToHashSet(StringComparer.Ordinal);

        var named = authorizeData
            .Select(data => data.AuthenticationSchemes)
            .Where(schemes => !string.IsNullOrWhiteSpace(schemes))
            .SelectMany(schemes => schemes!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .Where(registered.Contains)
            .ToArray();

        if (named.Length > 0)
        {
            return Expand(named, registered);
        }

        var defaultScheme = await authenticationSchemeProvider.GetDefaultAuthenticateSchemeAsync();
        return defaultScheme != null ? Expand([defaultScheme.Name], registered) : [];
    }

    /// <summary>
    /// Replaces any forwarding scheme with the schemes behind it. One requirement is emitted per resolved scheme,
    /// which is OR semantics — the caller needs any one of them, not all.
    /// </summary>
    private IReadOnlyList<string> Expand(IReadOnlyList<string> schemeNames, HashSet<string> registered)
    {
        if (schemeExpander == null)
        {
            return schemeNames;
        }

        return schemeNames
            .SelectMany(schemeExpander.Expand)
            .Where(registered.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
#endif
