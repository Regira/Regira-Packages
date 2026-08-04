using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Regira.Web.Swagger.Security;

public static class ApiKeyAuthenticationExtensions
{
    /// <summary>
    /// Adds the input form element for the ApiKey 
    /// </summary>
    public static void AddApiKeyAuthentication(this SwaggerGenOptions o, string authenticationScheme = "ApiKey", string parameterName = "X-Api-Key")
    {
        var apiKeySecurityScheme = new OpenApiSecurityScheme
        {
            Scheme = authenticationScheme,
            Name = parameterName,
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Description = "ApiKey"
        };

        o.AddSecurityDefinition(authenticationScheme, apiKeySecurityScheme);
        o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference(authenticationScheme, doc), [] }
        });
    }
}