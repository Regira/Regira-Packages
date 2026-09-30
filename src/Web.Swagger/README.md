# Regira.Web.Swagger

Swagger UI authentication inputs for APIs documented with [Swashbuckle.AspNetCore](https://www.nuget.org/packages/Swashbuckle.AspNetCore), part of [Regira Web.HTML](https://regira.github.io/Regira-Packages/src/Common.Web/). `AddJwtAuthentication()` and `AddApiKeyAuthentication()` extend `SwaggerGenOptions` with a JWT Bearer or API key security definition and a document-wide security requirement, so the Swagger UI prompts for a token or key. `DisplayEnumAsString()` on the MVC builder adds a `JsonStringEnumConverter` to the controllers' JSON options, so enums are serialized, and shown in Swagger, as strings.

## Installation

```xml
<PackageReference Include="Regira.Web.Swagger" Version="6.*" />
```

The package uses only [Microsoft.OpenApi](https://www.nuget.org/packages/Microsoft.OpenApi) 2.x APIs. On `net10.0` it references Microsoft.OpenApi 2.12.2 directly, the same version as `Regira.Security.Authentication.Web`, so an app referencing both resolves one version.

## Documentation

- [Web.Swagger](https://regira.github.io/Regira-Packages/src/Common.Web/#webswagger) — registering the JWT and API key inputs, and enums as strings
- [OpenAPI document transformers](https://regira.github.io/Regira-Packages/src/Common.Security/docs/controllers.html#openapi-document-transformers-securityauthenticationweb) — security schemes and per-operation requirements for the built-in `AddOpenApi` document (.NET 9+), in `Regira.Security.Authentication.Web`

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
