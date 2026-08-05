# Regira Security

Regira Security provides encryption, password hashing, JWT authentication, and API Key authentication for .NET applications.

## Projects

| Project | Package | Purpose |
|---------|---------|---------|
| `Common.Security` | `Regira.Security` | Symmetric encryption and PBKDF2 hashing |
| `Security.Hashing.BCryptNet` | `Regira.Security.Hashing.BCryptNet` | BCrypt password hashing |
| `Security.Authentication` | `Regira.Security.Authentication` | JWT tokens and API Key auth |
| `Security.Authentication.Web` | `Regira.Security.Authentication.Web` | Pre-built auth controllers |

## Installation

```xml
<!-- Core encryption + hashing -->
<PackageReference Include="Regira.Security" Version="6.*" />

<!-- BCrypt password hashing -->
<PackageReference Include="Regira.Security.Hashing.BCryptNet" Version="6.*" />

<!-- JWT + API Key auth -->
<PackageReference Include="Regira.Security.Authentication" Version="6.*" />

<!-- Pre-built Identity controllers -->
<PackageReference Include="Regira.Security.Authentication.Web" Version="6.*" />
```

---

## Encryption

Two `IEncrypter` implementations:

```csharp
public interface IEncrypter
{
    string Encrypt(string plainText, string? key = null);
    string Decrypt(string encryptedText, string? key = null);
}
```

### SymmetricEncrypter

AES-256 with a static key derived from `CryptoOptions.Secret`. Fast; same key always produces the same ciphertext.

```csharp
var enc = new SymmetricEncrypter(new CryptoOptions { Secret = "my-app-key" });
string cipher = enc.Encrypt("sensitive value");
string plain  = enc.Decrypt(cipher);
```

### AesEncrypter

AES with a random salt prepended per encryption. Slower but produces different ciphertext on each call — recommended for stored secrets.

```csharp
var enc = new AesEncrypter(new CryptoOptions { Secret = "my-app-key" });
string cipher = enc.Encrypt("sensitive value");
```

### CryptoOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Secret` | `string?` | built-in salt key | Signing / derivation secret |
| `AlgorithmType` | `string?` | `"SHA512"` | Hash algorithm — read only by `SymmetricEncrypter`, `SimpleHasher`, and the BCrypt hasher; `AesEncrypter` and the PBKDF2 `Hasher` hard-wire SHA-512 |
| `Iterations` | `int?` | `500000` | PBKDF2 iteration count used by the `Hasher` |
| `Encoding` | `Encoding?` | UTF-8 | Text encoding |

---

## Hashing

Two `IHasher` implementations:

```csharp
public interface IHasher
{
    string Hash(string plainText);
    bool   Verify(string plainText, string hashedValue);
}
```

### Hasher (PBKDF2)

Stores a per-hash random salt + PBKDF2 digest (500 000 iterations by default — configurable via `CryptoOptions.Iterations` — SHA-512, 64-byte output). Constant-time verification.

```csharp
var hasher = new Regira.Security.Hashing.Hasher();
string stored = hasher.Hash("myPassword123");
bool ok       = hasher.Verify("myPassword123", stored);   // true
```

### Security.Hashing.BCryptNet — BCrypt Hasher

Enhanced BCrypt (SHA-384 by default), using the BCrypt.Net default work factor. Recommended for passwords.

```csharp
var hasher = new Regira.Security.Hashing.BCryptNet.Hasher();
string stored = hasher.Hash("myPassword123");
bool ok       = hasher.Verify("myPassword123", stored);
```

### SimpleHasher

Double-SHA with salt — fast but weaker. Use for non-password data only.

---

## JWT Authentication

### JwtTokenOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Secret` | `string` | *(required)* | HMAC signing key — `HS256` needs ≥ 32 bytes, `HS384` ≥ 48, the `HS512` default ≥ 64. Enforced at registration |
| `Algorithm` | `string?` | `null` | Signing algorithm as a JWA id; HS512 when unset (applied as `SecurityAlgorithms.HmacSha512Signature`, the XML-dsig URI spelling) |
| `ValidateSecretLength` | `bool` | `true` | Whether registration rejects a `Secret` too short for `Algorithm` |
| `AuthenticationScheme` | `string` | `"Bearer"` | Name of the JwtBearer scheme |
| `Authority` | `string?` | `null` | Token issuer |
| `Audience` | `string?` | `null` | Single audience |
| `Audiences` | `ICollection<string>?` | `null` | Multiple audiences |
| `LifeSpan` | `int` | `7200` | Token lifetime in seconds |
| `IncludeIssuedDate` | `bool` | `true` | Include an `iat` claim in created tokens |
| `NameClaimType` | `string` | `"name"` | Claim used as user name |
| `RoleClaimType` | `string` | `"role"` | Claim used as role |
| `UseJwtClaimTypes` | `bool` | `true` | Configure the token handlers' claim-type maps to the short JWT spellings (`sub`, `name`, `email`) instead of the WS-2008 URIs |

### ITokenHelper

```csharp no-compile
string      Create(IEnumerable<Claim> claims, string? audience = null, int? lifeSpan = null);
Task<bool>  Validate(string token);
```

### DI registration

```csharp
var services = new ServiceCollection();
IConfiguration configuration = new ConfigurationManager();

services.AddJwtAuthentication(o => configuration.GetSection(AuthenticationSections.Jwt).Bind(o));
// Registers ITokenHelper as transient and configures the JwtBearer scheme.

// or explicitly
services.AddJwtAuthentication(options =>
{
    options.Secret    = configuration["Authentication:Jwt:Secret"]!;
    options.Authority = configuration["Authentication:Jwt:Authority"];
    options.Audience  = configuration["Authentication:Jwt:Audience"];
    options.LifeSpan  = 3600;
});
```

### ClaimsPrincipal extensions

Namespace `Regira.Security.Authentication.Jwt.Extensions` — historical; these apply to every scheme.

```csharp no-compile
string? userId = User.FindUserId();             // NameIdentifier / sub
string? name   = User.FindUserName();           // Identity.Name, then Name / name
string? email  = User.FindEmail();              // Email / email
IReadOnlyList<string> roles = User.FindRoles(); // every role, across all three spellings
bool canRead   = User.HasScope("api.read");     // splits the space-delimited scp / scope value
```

`FindRoles()` and `HasScope()` exist because the naive read is wrong. Roles reach a principal as `role`, `roles`
(Entra app roles) or the `ClaimTypes.Role` URI (API key, ASP.NET Identity), so reading one spelling answers empty
for the schemes using another. Scopes arrive as one space-delimited string rather than one claim each, so
`User.HasClaim("scp", "api.read")` is `false` against a token that plainly grants it.

### Claim normalization

`ClaimsNormalizer.Normalize(claims, authenticationType)` returns an identity carrying the canonical `sub` / `name`
/ `email` / `role` spellings (`RegiraClaimTypes`) alongside whatever the provider emitted. It is **additive** —
every source claim survives and a canonical copy is added only when missing, so Entra's `roles` keeps working for
anyone reading it directly while `[Authorize(Roles = …)]`, `User.IsInRole` and `RequireClaim("role", …)` all start
agreeing.

Source claim types per canonical claim are configurable via `ClaimNormalizationOptions`; the defaults cover `oid`,
`preferred_username`, `upn`, `unique_name`, `roles` and the `ClaimTypes` URIs. Note that **`oid`, not `sub`, is the
stable user id on an Entra token** — Entra's `sub` is pairwise per application.

### Configuration sections

`AuthenticationSections` holds the `Authentication:` root paths — `Jwt`, `Bearer`, `ApiKeys`, `Cookie`, `Oidc`,
`EntraId`:

```csharp
var services = new ServiceCollection();
IConfiguration configuration = new ConfigurationManager();

services.AddJwtAuthentication(o => configuration.GetSection(AuthenticationSections.Jwt).Bind(o));
```

---

## API Key Authentication

### ApiKeyAuthenticationOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ApiKeyHeaderName` | `string` | `"X-Api-Key"` | Request header name |
| `AuthenticationType` | `string` | `"ApiKey"` | Authentication type string |

### IApiKeyOwnerService

```csharp no-compile
Task<ApiKeyOwner?> FindByOwner(string id);
Task<ApiKeyOwner?> FindByKey(string apiKey);
Task<bool>         Validate(string id, string apiKey);
```

### ApiKeyOwner model

| Property | Type | Description |
|----------|------|-------------|
| `OwnerId` | `string` | Owner identifier |
| `Key` | `string` | API key value |
| `Roles` | `ICollection<string>` | Roles assigned to this key |
| `Claims` | `ICollection<ApiKeyOwner.Claim>` | Extra claims (`Type` / `Value` pairs) added to the principal |

### DI registration

```csharp
var services = new ServiceCollection();
IConfiguration configuration = new ConfigurationManager();

// In-memory keys from code
services.AddApiKeyAuthentication()
        .AddInMemoryApiKeyAuthentication(new[]
        {
            new ApiKeyOwner { OwnerId = "client-a", Key = "key-abc", Roles = ["read"] }
        });

// From appsettings.json
var keys = configuration.GetSection(AuthenticationSections.ApiKeys).ToApiKeyOwners();
services.AddApiKeyAuthentication()
        .AddInMemoryApiKeyAuthentication(keys);
```

`appsettings.json` shape — an **array**, each entry carrying its own `OwnerId`. `ToApiKeyOwners()` requires both
fields, so an object keyed by owner name throws `InvalidOperationException` at startup:
```json
"ApiKeys": [
  { "OwnerId": "client-a", "Key": "key-abc", "Roles": ["read", "write"] }
]
```

---

## Refresh Tokens (self-issued JWT)

Opt-in, chained off `AddJwtAuthentication`. Closes the gap a SPA on the JWT scheme hits: a 2-hour access token with no
way to renew it.

```csharp
var services = new ServiceCollection();
IConfiguration configuration = new ConfigurationManager();

services.AddJwtAuthentication(o => configuration.GetSection("Authentication:Jwt").Bind(o))
        .AddRefreshTokens();                      // in-memory store — development only
```

Nothing changes for a host that does not call it: `POST auth` returns the same body it always did (the `refreshToken`
and `expiresAt` fields are *absent*, not null) and `POST auth/refresh-token` answers `404`.

### auth/refresh vs auth/refresh-token

| Endpoint | Anon | Needs | Use |
|---|:---:|---|---|
| `POST auth/refresh` | | a **still-valid** bearer token | Pick up role changes mid-session |
| `POST auth/refresh-token` | ✅ | the refresh token | Renew **after** the access token expired |

`auth/refresh` cannot help at the one moment renewal matters — once the access token has expired it answers `401`.
`auth/refresh-token` is anonymous by necessity, because the refresh token *is* the credential; rate-limit it.

### RefreshTokenOptions

| Property | Type | Default | Description |
|---|---|---|---|
| `LifeSpan` | `int` (s) | 14 days | One token's validity |
| `AbsoluteLifeSpan` | `int` (s) | 90 days | Ceiling on the whole rotation chain |
| `Rotate` | `bool` | `true` | Issue a new token on every use, revoking the presented one |
| `RevokeFamilyOnReuse` | `bool` | `true` | End the chain when an already-rotated token is presented again |
| `TokenByteLength` | `int` | `32` | Entropy of the generated token |
| `HashStoredTokens` | `bool` | `true` | Persist a hash rather than the token |

### Security model

- **Opaque and random**, not a JWT — no claims to go stale, nothing readable by its holder.
- **Rotated** on every use, so a captured token is good for one call at most.
- **A replayed token ends the whole chain.** A rotated token should never appear twice, so a second presentation means
  two parties hold it and the server cannot tell which is asking. This also kills the attacker's freshly-minted token.
- **Claims are re-read on every refresh**, through a resolver you supply. It is a required parameter of
  `IRefreshTokenService.Refresh` so it cannot be skipped — replaying sign-in claims would keep a removed role in force
  and a disabled account working. Returning null refuses the refresh and revokes the chain.
- **`AbsoluteLifeSpan` caps the chain**; without it a frequently-used token never expires.
- **Stored hashed** (unsalted SHA-256) so a leaked store yields nothing usable. Unsalted is deliberate — the store is
  looked up by the hash, and a 256-bit random token has no guessable input for a slow KDF to protect. That reasoning
  does not transfer to passwords; use `IHasher` there.

### The in-memory store is development-only

`AddRefreshTokens()` falls back to `InMemoryRefreshTokenStore` when no store is registered. It loses every session on
restart and is per-process, so behind a load balancer a refresh lands on an instance that has never heard of the token
and users are signed out at random. Neither shows up on one developer machine.

Implement `IRefreshTokenStore` over your own `DbContext` — five methods, of which only `TryRevoke` needs care — and
register it first:

```csharp no-compile
services.AddJwtAuthentication(…)
        .AddRefreshTokenStore<MyEfRefreshTokenStore>()
        .AddRefreshTokens();
```

`RevokeFamily` must reach already-revoked tokens as well as active ones: it ends a chain, and a replayed token is by
definition one that was already revoked.

`TryRevoke` **must be atomic** — it is a test-and-set, revoking only if the token is not already revoked and
returning whether this caller did it. Two concurrent refreshes of one token would otherwise both succeed and split
the family into two live chains with no replay detected. Implement it as a conditional write: `ExecuteUpdate`
filtered on `RevokedAt == null` with a row-count check, or `UPDATE … WHERE TokenKey = @k AND RevokedAt IS NULL`.

Call `RevokeAllForUser` on a password change or when an account is disabled — nothing does it automatically.

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

## Cookie Authentication

For server-rendered apps, Blazor Server, and same-site SPAs. No extra package — it is in the ASP.NET Core shared
framework.

```csharp
var services = new ServiceCollection();
IConfiguration configuration = new ConfigurationManager();

services.AddCookieAuthentication(o =>
{
    o.IsApi = true;                          // 401/403 instead of a 302 to LoginPath
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// or bind Authentication:Cookie
services.AddCookieAuthentication(configuration);
```

### CookieAuthOptions

| Property | Type | Default | Description |
|---|---|---|---|
| `AuthenticationScheme` | `string` | `"Cookies"` | The framework's own name, so `SignInAsync` without a scheme resolves here |
| `CookieName` | `string` | `".Regira.Auth"` | |
| `IsApi` | `bool` | `false` | Answer `401`/`403` instead of redirecting |
| `ExpireTimeSpan` | `TimeSpan` | 8 h | |
| `SlidingExpiration` | `bool` | `true` | Measured from the last request |
| `LoginPath` / `LogoutPath` / `AccessDeniedPath` | `string` | `/login`, `/logout`, `/forbidden` | Ignored when `IsApi` |
| `ReturnUrlParameter` | `string` | `"returnUrl"` | |
| `SameSite` | `SameSiteMode` | `Lax` | |
| `SecurePolicy` | `CookieSecurePolicy` | `Always` | |
| `Domain` | `string?` | `null` | |
| `Claims` | `ClaimNormalizationOptions` | *(defaults)* | Source claim types folded into the canonical set |
| `Configure` | `Action<CookieAuthenticationOptions>?` | `null` | Applied last, for anything not exposed |

`HttpOnly` is always on and not configurable.

### Signing in and out

```csharp no-compile
await HttpContext.SignInWithClaimsAsync(claims, isPersistent: true);   // normalizes first
await HttpContext.SignOutCookieAsync();
```

Normalization runs at sign-in, so the canonical claim spellings go into the ticket rather than being recomputed
per request.

### Cookie notes

- **`SecurePolicy.Always` means the cookie is never sent over plain HTTP.** Over `http://`, sign-in appears to
  succeed and every later request is anonymous — the cookie is issued, never returned, and the endpoint answers
  `401` as though the credentials were wrong. Serve dev over HTTPS, or use `SameAsRequest` locally only.
- **The cookie is Data Protection-encrypted.** Multi-instance or containerised hosting needs a shared, persisted
  key ring plus `SetApplicationName`, or every restart invalidates every cookie. The symptom is random logouts,
  never an error, and it does not reproduce on one machine.
- **Cookies authenticate ambiently, so state-changing endpoints need antiforgery** — a bearer token does not.
- **Set `IsApi` for anything a script calls**, or the handler `302`s to an HTML login page that `fetch` follows,
  returning `200` and HTML where the caller expected JSON.
- **A cross-site SPA needs `SameSite = None`**, which requires `Secure` (so HTTPS), plus a CORS policy with
  `AllowCredentials`.
- **A cookie is a stale snapshot** — role changes take effect on expiry. For immediate revocation, validate a
  security stamp in `Configure`'s `Events.OnValidatePrincipal`.

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

## Composing multiple schemes

`AddSchemeSelector()` registers a policy scheme that forwards each request to the scheme matching the credential
it carries, and makes itself the default authenticate and challenge scheme.

```csharp
var services = new ServiceCollection();
IConfiguration configuration = new ConfigurationManager();

services.AddApiKeyAuthentication()
        .AddInMemoryApiKeyAuthentication(configuration.GetSection("Authentication:ApiKeys").ToApiKeyOwners());

services.AddJwtAuthentication(o => configuration.GetSection("Authentication:Jwt").Bind(o))
        .AddSchemeSelector();     // last
```

Call it **last**. Every `Add…Authentication` sets its own default scheme, so without it the registration order
decides which handler an unattributed `[Authorize]` uses — and the symptom is a 401 for a caller holding a
perfectly good credential of the other kind. Rules naming an unregistered scheme are skipped, so the order stops
mattering. No `AddAuthorization` default policy is needed to name the schemes.

Built-in rules, first match wins: `Authorization: Bearer …` → the bearer scheme; a non-empty API-key header →
the API-key scheme; the default-named authentication cookie → the cookie scheme (`SchemeForwardRules.Cookie()`,
skipped when that scheme is not registered). A blank API-key header deliberately does not match, so it cannot
capture a request that the bearer scheme could have served.

| `SchemeSelectorOptions` | Type | Default | Description |
|---|---|---|---|
| `AuthenticationScheme` | `string` | `"Smart"` | Name of the policy scheme |
| `DisplayName` | `string` | *(descriptive)* | Display name of the policy scheme |
| `FallbackScheme` | `string?` | `null` | Scheme for a request with no recognised credential; defaults to the lowest-ordered registered rule |
| `ChallengeScheme` | `string?` | `null` | Scheme that answers a challenge; when unset, a registered sign-in scheme (see below) or the forwarding rules decide |
| `ForwardChallengeToSignInScheme` | `bool` | `true` | Whether a registered interactive sign-in scheme answers challenges when `ChallengeScheme` is unset |
| `Rules` | `IList<SchemeForwardRule>` | `[]` | Extra rules, added before the built-in ones |
| `UseDefaultRules` | `bool` | `true` | Whether to include the bearer, API-key, and cookie rules |

Build rules with `SchemeForwardRules.Bearer(scheme)`, `.Basic(scheme)`, `.ApiKey(scheme)`,
`.Cookie(scheme, cookieName)`, or `new SchemeForwardRule(order, scheme, context => …)`.

Because the policy scheme authenticates nothing, no document transformer declares it — register the operation
transformer so guarded operations name the real schemes instead, plus a document transformer per scheme.

---

## Pre-built Auth Controllers (`Security.Authentication.Web`)

`Security.Authentication.Web` ships three abstract base controllers over ASP.NET Core Identity's `UserManager<TUser>`. Subclass each with a closed user type — `[ApiController]` and the route templates are declared on the base classes and inherited, so the concrete controllers need no attributes of their own:

```csharp no-compile
public class AccountController(
    ITokenHelper tokenHelper,
    UserManager<AppUser> userManager,
    IUserClaimsPrincipalFactory<AppUser> claimsFactory,
    ILogger<AccountController> logger)
    : AccountControllerBase<AppUser>(tokenHelper, userManager, claimsFactory, logger);

public class PasswordController(UserManager<AppUser> userManager)
    : PasswordControllerBase<AppUser>(userManager);

public class UsersController(UserManager<AppUser> userManager)
    : UserControllerBase<AppUser>(userManager);
```

`TUser` must derive from `IdentityUser<string>` (`UserControllerBase` additionally requires `new()`).

### Required services

| Service | Provided by | Used for |
|---------|-------------|----------|
| `UserManager<TUser>` + user store | `AddIdentityCore<TUser>().AddEntityFrameworkStores<…>().AddDefaultTokenProviders()` | user lookup, password & token operations |
| `ITokenHelper` | `AddJwtAuthentication(…)` | issuing JWTs (`AccountController`) |
| `IUserClaimsPrincipalFactory<TUser>` | `AddIdentityCore` | building token claims |
| `IEmailSender` | Regira's `IdentityMailer` (over `Regira.Office.Mail`) or your own | recover / confirm emails |

`AddDefaultTokenProviders()` is required — recover and confirm-email generate Identity tokens. The confirm-email/reset token payloads are (de)serialized with `System.Text.Json` internally, so no serializer needs to be registered.

### Authorization: guarded by default

The controllers carry no per-action `[Authorize]`. Instead, **the host applies a global authorization requirement when mapping them, so that every endpoint requires an authenticated user and only `[AllowAnonymous]` actions stay public:**

```csharp
var app = WebApplication.Create();

app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers()
        .RequireAuthorization(/* optional policy */);
});
```

Without this global requirement the non-anonymous endpoints (change-password, user creation, refresh, personal-data) would be exposed. The endpoints that opt out with `[AllowAnonymous]` are:

| Endpoint | Why anonymous |
|----------|---------------|
| `POST auth` (authenticate) | the caller has no token yet |
| `POST auth/password/recover` | forgot-password entry point |
| `POST auth/password/reset` | authorized by the emailed token |
| `POST users/confirm-email` | authorized by the emailed token |

### `AccountControllerBase<TUser>` — route `auth`

| Endpoint | Anon | Request body | Success | Failure |
|----------|:----:|--------------|---------|---------|
| `POST auth?clientApp=…` | ✅ | `{ username, password }` | `200` `{ isAuthenticated: true, token }`, plus `refreshToken` and `expiresAt` when refresh tokens are registered | `401` `{ isLockedOut, lockedOutEnd }` |
| `POST auth/validate` | | *(bearer)* | `204` | `401`, or `403` if the token is valid but the user is gone |
| `POST auth/refresh` | | *(bearer, **still valid**)* | `200` `{ isAuthenticated: true, token }` | `401` |
| `POST auth/refresh-token` | ✅ | `{ refreshToken }` | `200` `{ isAuthenticated: true, token, refreshToken, expiresAt }` | `401`; `404` when refresh tokens are not registered |
| `GET auth/personal-data` | | *(bearer)* | `200` `{ given_name, family_name }` | `401` |

`auth/refresh` needs a **still-valid** bearer token, so it cannot renew an expired one — use `auth/refresh-token` for
that. See *Refresh Tokens* above.

`clientApp` becomes the token audience. A successful authenticate resets the user's failed-access count; a failed one increments it and can trigger Identity lockout.

### `PasswordControllerBase<TUser>` — route `auth/password`

| Endpoint | Anon | Request body | Success | Failure |
|----------|:----:|--------------|---------|---------|
| `POST auth/password` | | `{ currentPassword, newPassword }` | `200` | `400` identity errors / `404` |
| `POST auth/password/recover` | ✅ | `{ username, siteUrl, siteName }` | `200` (always) | — |
| `POST auth/password/reset` | ✅ | `{ token, password }` | `200` | `400` malformed token or identity errors |

`recover` always returns `200` (it never reveals whether the user exists) and emails a `token` — a Base64 payload of the Identity reset token plus the username. `reset` decodes that payload and returns `400` when it is malformed.

### `UserControllerBase<TUser>` — route `users`

| Endpoint | Anon | Request body | Success | Failure |
|----------|:----:|--------------|---------|---------|
| `POST users` | | `{ username, password, confirmEmailUrl? }` | `200` | `400` identity errors |
| `POST users/confirm-email` | ✅ | `{ token, userName, password? }` | `200` | `400` malformed token or identity errors |

`username` is used as both the user name and the email address. When `confirmEmailUrl` is supplied, a confirmation email carrying a Base64 `token` is sent; `confirm-email` decodes it and returns `400` on a malformed token. Creating a user that already exists is a no-op `200`. The optional `password` on the confirm-email input is not used by the base implementation — it is available to overrides.

### OpenAPI document transformers (`Security.Authentication.Web`)

`Regira.Security.Authentication.Web.OpenApi.Transformers` describes the API's authentication in the generated
OpenAPI document (.NET 9+). Two transformers are enough whatever the scheme count — the first declares the schemes,
which is what makes the Swagger/Scalar authentication prompt appear; the second records **which** operations need
one, without which a generated client cannot tell a public endpoint from a guarded one.

| Transformer | Registration | Emits |
|---|---|---|
| `AuthenticationSchemeDocumentTransformer` | `AddDocumentTransformer<…>()` | `components.securitySchemes` for **every** registered scheme, from the descriptor each contributes at registration |
| `SecurityRequirementOperationTransformer` | `AddOperationTransformer<…>()` | a per-operation `security` requirement, resolving a forwarding policy scheme to the schemes behind it |
| `BearerSecuritySchemeTransformer` | `AddDocumentTransformer<…>()` | the `Bearer` scheme alone — superseded, and safe to register alongside |
| `ApiKeySecurityDocumentTransformer` | `AddDocumentTransformer<…>()` | the API-key scheme alone — superseded, and safe to register alongside |

Cookie has no dedicated OpenAPI security type; it is emitted as an API key with `in: cookie`, the accepted
convention. Adding a scheme needs no transformer change.

```csharp no-compile
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<SecurityRequirementOperationTransformer>();
});
```

An operation counts as guarded when its endpoint carries authorization metadata and no `[AllowAnonymous]` —
the same reading the authorization middleware performs, so a global
`MapControllers().RequireAuthorization()` with a few `[AllowAnonymous]` actions is described accurately. The
scheme named on `[Authorize(AuthenticationSchemes = …)]` wins; otherwise the default authenticate scheme is
used, and a guarded operation for which no scheme resolves is logged as a warning.

### Security notes

- **Apply the global auth requirement** when mapping the controllers (above) — they do not self-guard with per-action `[Authorize]`, so without it the non-anonymous endpoints are exposed.
- The authenticate failure response distinguishes a locked-out existing user from an unknown one (`isLockedOut` is `null` for unknown users) — a deliberate trade-off to weigh against username enumeration.
- Recover / confirm tokens travel in the email body and in the `siteUrl` / `confirmEmailUrl` query string; query strings can surface in server logs, browser history and referrers, so prefer short token lifetimes.

---

## Overview

1. **[Index](https://regira.github.io/Regira-Packages/src/Common.Security/)** — Overview, encryption, hashing, and every authentication scheme
1. [Examples](https://regira.github.io/Regira-Packages/src/Common.Security/docs/examples.html) — Hash passwords, JWT + refresh tokens, API keys, cookies, Entra ID, Identity controllers
