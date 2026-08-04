#if NET9_0_OR_GREATER
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
#if NET10_0_OR_GREATER
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif

namespace Regira.Security.Authentication.Web.OpenApi.Transformers;

// https://www.answeroverflow.com/m/1306435010865401907
public class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var authenticationSchemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (authenticationSchemes.Any(authScheme => authScheme.Name == "Bearer"))
        {
            var scheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer", // "bearer" refers to the header name here
                In = ParameterLocation.Header,
                BearerFormat = "Json Web Token"
            };
            document.Components ??= new OpenApiComponents();
#if NET10_0_OR_GREATER
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
#else
            document.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();
#endif
            document.Components.SecuritySchemes.TryAdd("Bearer", scheme);
        }
    }
}
#endif