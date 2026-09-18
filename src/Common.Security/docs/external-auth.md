# Regira Security — External Identity Providers

Authentication against an external identity provider: validating bearer tokens it issued, and driving an interactive sign-in through OpenID Connect. For tokens this application issues itself, see [JWT Authentication](jwt.md).

---

## External Bearer Authentication (OIDC / Entra ID)

Validating tokens **something else issued** — Entra ID, Auth0, Keycloak, Duende, Okta.

`AddJwtAuthentication` cannot do this: it requires a `Secret` and always derives a symmetric key, while an external
authority signs with rotating asymmetric keys published at a JWKS endpoint. `AddBearerAuthentication` registers no
`ITokenHelper` — it reads tokens, it does not mint them.

```csharp
var services = new ServiceCollection();
IConfiguration configuration = new ConfigurationManager();

// Any OpenID Connect provider
services.AddBearerAuthentication(o =>
{
    o.Authority = "https://your-tenant.eu.auth0.com/";
    o.Audience  = "https://api.example.com";
});

// Entra ID, from the app registration
services.AddEntraIdBearer(o =>
{
    o.TenantId = configuration["Authentication:EntraId:TenantId"]!;
    o.ClientId = configuration["Authentication:EntraId:ClientId"]!;
});
```

### BearerValidationOptions

| Property | Type | Default | Description |
|---|---|---|---|
| `AuthenticationScheme` | `string` | `"Bearer"` | |
| `Authority` | `string?` | `null` | Issuer base URL; signing keys discovered and refreshed from it |
| `MetadataAddress` | `string?` | `null` | Overrides the derived metadata URL |
| `Secret` | `string?` | `null` | Shared symmetric key, for an HMAC-signing issuer |
| `Audience` / `Audiences` | | `null` | `Audiences` wins when both are set |
| `ValidIssuers` | `ICollection<string>?` | `null` | Null ⇒ the discovery document's issuer |
| `RequireHttpsMetadata` | `bool` | `true` | |
| `SaveToken` | `bool` | `false` | Keep the raw token for a downstream call |
| `ValidateLifetime` | `bool` | `true` | |
| `ClockSkew` | `TimeSpan` | `Zero` | |
| `NameClaimType` / `RoleClaimType` | `string` | `"name"` / `"role"` | |
| `Claims` | `ClaimNormalizationOptions` | *(defaults)* | |
| `Configure` | `Action<JwtBearerOptions>?` | `null` | Applied before the normalization hook is chained on |

Exactly one source of signing keys is required — `Authority`/`MetadataAddress` or `Secret`. Both, or neither,
throws at registration.

### EntraIdOptions

| Property | Type | Default | Description |
|---|---|---|---|
| `TenantId` | `string` | *(required)* | Directory id, or `organizations` / `common` |
| `ClientId` | `string` | *(required)* | This API's application id |
| `Instance` | `string` | `https://login.microsoftonline.com` | Sovereign clouds differ |
| `UseV2Endpoint` | `bool` | `true` | |
| `Audiences` | `ICollection<string>?` | `null` | Defaults to both `api://{ClientId}` and `{ClientId}` |
| `AuthenticationScheme` | `string` | `"Bearer"` | |
| `SaveToken` | `bool` | `false` | |
| `Claims` | `ClaimNormalizationOptions` | *(defaults)* | |
| `Configure` | `Action<JwtBearerOptions>?` | `null` | |

### Entra notes

- **App roles arrive as `roles`, plural.** `role` singular matches nothing, so `[Authorize(Roles = "Admin")]`
  answers 403 against a token that visibly contains the role. The preset handles it and normalization adds a `role`
  copy, so both spellings work.
- **`oid` is the stable user id, `sub` is not** — Entra's `sub` is pairwise per application, so two apps see
  different values for the same person.
- **v1 vs v2 issuer.** A registration on `accessTokenAcceptedVersion: null` issues v1 tokens from
  `https://sts.windows.net/{tid}/`; the mismatch surfaces as `IDX10205`. Both spellings are accepted for a single
  tenant.
- **`organizations` / `common` is multi-tenant**, so there is no fixed issuer — it is validated against the token's
  own `tid`. Any tenant can then sign in; deciding whether that tenant is entitled to anything is the
  application's job.
- **`groups` is object GUIDs**, and past the token-size limit it is dropped in favour of `_claim_names` — so a
  groups-based model breaks for the users in the most groups. Resolving it needs a Graph call.

### What these presets do not do

No `Microsoft.Identity.Web`, no MSAL. They protect an API and sign users in, and stop there — no on-behalf-of flow,
no downstream calls as the user, no MSAL token cache, no incremental consent, no B2C user flows. Take
`Microsoft.Identity.Web` directly if you need those.

---

## Interactive Sign-in (OpenID Connect)

Signing users in through a browser: authorization code + PKCE, landing in a cookie session. Always a **pair** of
schemes — the OIDC handler runs the challenge and code exchange, a cookie holds the session — and
`AddOidcAuthentication` registers both.

```csharp
var services = new ServiceCollection();
IConfiguration configuration = new ConfigurationManager();

// Entra ID
services.AddEntraIdSignIn(o =>
{
    o.TenantId     = configuration["Authentication:EntraId:TenantId"]!;
    o.ClientId     = configuration["Authentication:EntraId:ClientId"]!;
    o.ClientSecret = configuration["Authentication:EntraId:ClientSecret"]!;
});

// Any OpenID Connect provider
services.AddOidcAuthentication(o =>
{
    o.Authority    = "https://your-tenant.eu.auth0.com/";
    o.ClientId     = "…";
    o.ClientSecret = "…";
});
```

### Scheme pairing

| Default | Scheme |
|---|---|
| `DefaultScheme` / `DefaultAuthenticateScheme` / `DefaultSignInScheme` | the cookie |
| `DefaultChallengeScheme` / `DefaultSignOutScheme` | the OIDC scheme |

Backwards, an `[Authorize]` endpoint either tries to validate an id_token it does not have, or redirects to the
provider on every request.

### OidcAuthOptions

| Property | Type | Default | Description |
|---|---|---|---|
| `AuthenticationScheme` | `string` | `"OpenIdConnect"` | |
| `SignInScheme` | `string?` | `null` | Defaults to `Cookie`'s scheme |
| `Authority` / `ClientId` | `string` | *(required)* | |
| `ClientSecret` | `string?` | `null` | Required for the confidential-client code exchange |
| `ResponseType` | `string` | `"code"` | |
| `Scopes` | `ICollection<string>` | `openid profile email` | Replaces the handler's defaults |
| `CallbackPath` | `string` | `/signin-oidc` | Must match a registered redirect URI exactly |
| `SignedOutCallbackPath` | `string` | `/signout-callback-oidc` | |
| `SignedOutRedirectUri` | `string?` | `null` | |
| `UsePkce` | `bool` | `true` | |
| `SaveTokens` | `bool` | `false` | Keeps the tokens in the cookie — and enlarges it |
| `GetClaimsFromUserInfoEndpoint` | `bool` | `true` | A lean id_token usually omits `email` |
| `RequireHttpsMetadata` | `bool` | `true` | |
| `NameClaimType` / `RoleClaimType` | `string` | `"name"` / `"role"` | |
| `ValidIssuers` | `ICollection<string>?` | `null` | Null ⇒ the discovery document's issuer |
| `Cookie` | `CookieAuthOptions` | *(defaults)* | The session half |
| `Claims` | `ClaimNormalizationOptions` | *(defaults)* | |
| `Configure` | `Action<OpenIdConnectOptions>?` | `null` | Applied before the normalization hook is chained on |

`EntraIdSignInOptions` is the preset — `TenantId`, `ClientId`, `ClientSecret`, `Instance`, `UseV2Endpoint`,
`Scopes`, the callback paths, `SaveTokens`, and a `Configure` reaching the full `OidcAuthOptions`.

### OIDC notes

- **"Correlation failed" behind a reverse proxy.** A proxy terminating TLS makes the handler build `redirect_uri`
  from the internal plain-HTTP request; the provider rejects it, or returns the browser to the wrong origin where
  the correlation cookie is not sent back. Configure `UseForwardedHeaders` ahead of the authentication middleware.
- **Sign-out needs both halves** — the cookie scheme and the OIDC scheme. Clearing only the cookie leaves the
  provider session intact, so the next challenge signs the user straight back in and "log out" appears to do nothing.
- **`CallbackPath` must match a registered redirect URI exactly**, scheme, host and port included.
- **`SaveTokens = true`** is required for a later downstream call and makes the cookie considerably larger.
- A **multi-tenant** sign-in has the same issuer hole as a multi-tenant API; it is closed automatically, and the
  check is applied after your `Configure` delegate so customization cannot drop it.

The code exchange itself needs a live provider — verify the full round trip against a real tenant.

---

## Overview

1. [Index](../README.md) — Overview, projects, and choosing a scheme
1. [Encryption & Hashing](cryptography.md) — Symmetric encryption, PBKDF2 and BCrypt password hashing
1. [JWT Authentication](jwt.md) — Self-issued bearer tokens, claims, and refresh tokens
1. [API Key Authentication](api-keys.md) — Key-based auth for machine callers
1. **[External Identity Providers](external-auth.md)** — Validating external bearer tokens; OpenID Connect sign-in
1. [Cookie Authentication](cookies.md) — Cookie-backed sessions
1. [Composing Multiple Schemes](schemes.md) — Running several schemes side by side
1. [Pre-built Auth Controllers](controllers.md) — Account, password and user endpoints
1. [Practical Examples](examples.md) — Complete implementation examples

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
