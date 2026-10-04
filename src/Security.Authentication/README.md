# Regira.Security.Authentication

Authentication schemes for ASP.NET Core in [Regira Security](https://regira.github.io/Regira-Packages/src/Common.Security/), each registered with one extension method: self-issued JWTs with refresh tokens (`AddJwtAuthentication`, `AddRefreshTokens`, `ITokenHelper`), bearer tokens issued by Entra ID or another identity provider (`AddBearerAuthentication`, `AddEntraIdBearer`), API keys (`AddApiKeyAuthentication`), cookie sessions (`AddCookieAuthentication`) and OpenID Connect sign-in (`AddOidcAuthentication`, `AddEntraIdSignIn`). `AddSchemeSelector` runs several of them side by side. Built on [Microsoft.AspNetCore.Authentication.JwtBearer](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer), [Microsoft.AspNetCore.Authentication.OpenIdConnect](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.OpenIdConnect) and [Duende.IdentityModel](https://www.nuget.org/packages/Duende.IdentityModel).

## Installation

```xml
<PackageReference Include="Regira.Security.Authentication" Version="6.*" />
```

## Documentation

- [Choosing an authentication scheme](https://regira.github.io/Regira-Packages/src/Common.Security/#choosing-an-authentication-scheme) — which scheme fits which need, and the page that documents each one
- [JWT Authentication](https://regira.github.io/Regira-Packages/src/Common.Security/docs/jwt.html) — token options, `ITokenHelper`, claims and refresh tokens
- [Practical Examples](https://regira.github.io/Regira-Packages/src/Common.Security/docs/examples.html) — JWT, API key, cookie, Entra ID and multi-scheme setups

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
