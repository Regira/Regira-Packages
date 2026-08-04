using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Regira.Web.Swagger.Security;

public static class JwtAuthenticationExtensions
{
    public static void AddJwtAuthentication(this SwaggerGenOptions o, string authenticationScheme = "Bearer")
    {
        // ToDo: verify this implementation
        var jwtSecurityScheme = new OpenApiSecurityScheme
        {
            Scheme = authenticationScheme,
            BearerFormat = "JWT",
            Name = "JWT Authentication",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Description = "Put your JWT Bearer token on textbox below",
        };

        o.AddSecurityDefinition(authenticationScheme, jwtSecurityScheme);
        o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
        {
            {new OpenApiSecuritySchemeReference(authenticationScheme, doc), []}
        });
    }
}