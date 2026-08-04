# Regira Security AI Agent Instructions

> Encryption, password hashing, and authentication for .NET applications — self-issued JWT with refresh tokens,
> API keys, cookie sessions, external-authority bearer (Entra ID / OIDC), and interactive OpenID Connect sign-in.

## Projects

| Project | Package | Purpose |
|---|---|---|
| `Common.Security` | `Regira.Security` | Symmetric encryption and PBKDF2 hashing |
| `Security.Hashing.BCryptNet` | `Regira.Security.Hashing.BCryptNet` | BCrypt password hashing |
| `Security.Authentication` | `Regira.Security.Authentication` | JWT (+ refresh tokens), API keys, cookies, external bearer / Entra ID, OpenID Connect sign-in, scheme selection |
| `Security.Authentication.Web` | `Regira.Security.Authentication.Web` | Pre-built auth controllers |

---

## Installation

```xml
<!-- Core encryption + PBKDF2 hashing -->
<PackageReference Include="Regira.Security" Version="6.*" />

<!-- BCrypt password hashing (recommended for passwords) -->
<PackageReference Include="Regira.Security.Hashing.BCryptNet" Version="6.*" />

<!-- JWT + API Key authentication -->
<PackageReference Include="Regira.Security.Authentication" Version="6.*" />

<!-- Pre-built auth controllers (AccountController, UserController, PasswordController) -->
<PackageReference Include="Regira.Security.Authentication.Web" Version="6.*" />
```

---

## Encryption

### `IEncrypter`

```csharp
string Encrypt(string plainText, string? key = null);
string Decrypt(string encryptedText, string? key = null);
```

### `SymmetricEncrypter` — AES-256, static key

Fast. Same key always produces the same ciphertext. Use for non-sensitive reversible encoding.

```csharp
using Regira.Security.Encryption; // SymmetricEncrypter
using Regira.Security.Core;       // CryptoOptions

var enc = new SymmetricEncrypter(new CryptoOptions { Secret = "my-app-key" });
string cipher = enc.Encrypt("sensitive value");
string plain  = enc.Decrypt(cipher);
```

### `AesEncrypter` — AES with random salt

Slower but produces different ciphertext on each call. **Recommended for stored secrets.**

```csharp
using Regira.Security.Encryption; // AesEncrypter
using Regira.Security.Core;       // CryptoOptions

var enc = new AesEncrypter(new CryptoOptions { Secret = "my-app-key" });
string cipher = enc.Encrypt("sensitive value");
string plain  = enc.Decrypt(cipher);
```

### `CryptoOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `Secret` | `string?` | built-in salt key | Signing / derivation secret |
| `AlgorithmType` | `string?` | `"SHA512"` | Hash algorithm for key derivation |
| `Encoding` | `Encoding?` | UTF-8 | Text encoding |

---

## Hashing

### `IHasher` — `Regira.Security.Abstractions`

```csharp
using Regira.Security.Abstractions; // IHasher

string Hash(string? plainText);
bool   Verify(string? plainText, string hashedValue);
```

### `Hasher` — PBKDF2 (in `Regira.Security`)

Per-hash random salt + PBKDF2 digest (10 000 iterations, SHA-512, 64-byte output). Constant-time verification.

```csharp
var hasher = new Regira.Security.Hashing.Hasher();
string stored = hasher.Hash("myPassword123");
bool ok       = hasher.Verify("myPassword123", stored);  // true
```

### `BCryptNet.Hasher` — BCrypt (in `Regira.Security.Hashing.BCryptNet`)

Enhanced BCrypt (SHA-384 by default). **Recommended for passwords.**

```csharp
var hasher = new Regira.Security.Hashing.BCryptNet.Hasher();
string stored = hasher.Hash("myPassword123");
bool ok       = hasher.Verify("myPassword123", stored);
```

### `SimpleHasher` — double-SHA

Fast but weaker. Use for non-password data only (e.g. cache keys, checksums).

---

## Hashing Decision Guide

| Use case | Recommended |
|---|---|
| User passwords | `BCryptNet.Hasher` |
| General data hashing with security | `Hasher` (PBKDF2) |
| Cache keys / non-security checksums | `SimpleHasher` |

---

## Choosing a scheme

`SelfHostingApiWithAuth` scaffolds **any** authenticated app — the scheme is a registration call on top of it,
not a different template. Pick by what the caller presents:

| The caller is | Register | Read |
|---|---|---|
| A machine or service holding a shared secret | `AddApiKeyAuthentication` | *API Key Authentication* |
| A SPA or mobile client against your own user table | `AddJwtAuthentication` (+ `AddRefreshTokens`) | *JWT Authentication*, *Refresh Tokens (self-issued JWT)* |
| A browser session — server-rendered, Blazor Server, same-site SPA | `AddCookieAuthentication` | *Cookie Authentication* |
| Presenting tokens an external authority issued (Entra ID, Auth0, Keycloak, Okta) | `AddBearerAuthentication` / `AddEntraIdBearer` | *External Bearer Authentication (OIDC / Entra ID)* |
| Signing in interactively through a provider | `AddOidcAuthentication` / `AddEntraIdSignIn` | *Interactive Sign-in (OpenID Connect)* |
| More than one of the above | each scheme above, then `AddSchemeSelector` | *Composing multiple schemes — `AddSchemeSelector`* |

Each `Add*` registration is independent, so more than one is normal; `AddSchemeSelector` only decides which
registered scheme authenticates a given request. Claims differ per scheme — read
*⚠️ Claims emitted per scheme — the two spellings* before reading a claim by name.

---

## JWT Authentication

### `JwtTokenOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `Secret` | `string` | *(required)* | HMAC signing key — **length must fit the algorithm**: `HS256` ≥ 32 bytes, `HS384` ≥ 48, the `HS512` default ≥ 64. Enforced at registration |
| `Algorithm` | `string?` | `null` | Signing algorithm as a JWA id; `HS512` when unset |
| `ValidateSecretLength` | `bool` | `true` | Whether registration rejects a `Secret` too short for `Algorithm` |
| `Authority` | `string?` | `null` | Token issuer |
| `Audience` | `string?` | `null` | Single audience |
| `Audiences` | `ICollection<string>?` | `null` | Multiple audiences |
| `LifeSpan` | `int` | `7200` | Token lifetime in seconds |
| `NameClaimType` | `string` | `"name"` | Claim used as user name |
| `RoleClaimType` | `string` | `"role"` | Claim used as role |

A `Secret` shorter than its algorithm allows throws `InvalidOperationException` from
`AddJwtAuthentication`, naming the byte count it got and the one it needs. Set `ValidateSecretLength = false`
only for a scheme that never issues tokens — when validating tokens signed elsewhere, the required key length
is whatever the issuer used, which `Algorithm` does not describe.

### `ITokenHelper` — `Regira.Security.Authentication.Jwt.Abstraction`

```csharp
using Regira.Security.Authentication.Jwt.Abstraction; // ITokenHelper
using Regira.Security.Authentication.Jwt.Models;      // JwtTokenOptions

string      Create(IEnumerable<Claim> claims, string? audience = null, int? lifeSpan = null);
Task<bool>  Validate(string token);
```

### DI Registration

```csharp
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

> ⚠️ **Emit a plain `"role"` claim — not `ClaimTypes.Role`.** Regira points
> `TokenValidationParameters.RoleClaimType` at `RoleClaimType` (default `"role"`), and `UseJwtClaimTypes`
> (on by default) rebuilds the inbound maps of **both** token handlers so that **only `sub` and `email` are
> renamed** (to `ClaimTypes.NameIdentifier` / `ClaimTypes.Email`). Every other claim — `role`, `name`,
> `given_name`, … — reaches your code under the name the token carries, so read those by their JWT names
> rather than through `ClaimTypes.*`; the `User.Find*()` helpers below accept either spelling. Emit `ClaimTypes.Role` instead — or reset an inbound
> claim-type map yourself — and align `RoleClaimType` to match, because a mismatch never fails a build and has
> **two** symptoms: `[Authorize(Roles=…)]` answers 403, and anything reading the claim by hand (row-security
> filters, `ctx.User.FindFirst("role")`) sees nothing and quietly returns the rows a role-less user may see.
> A 403 is loud; fewer rows with a 200 is not. For tiers, prefer claim **policies** over `[Authorize(Roles=…)]`
> strings — but a policy that pins the type (`RequireClaim("role", …)`) holds only for JWT callers. If API keys
> also reach those endpoints, see *Claims emitted per scheme* below: the two schemes spell the role claim
> differently, and `RequireRole`/`IsInRole` is the one form both resolve.

### `ClaimsPrincipal` Extension Methods — `Regira.Security.Authentication.Jwt.Extensions`

```csharp
using Regira.Security.Authentication.Jwt.Extensions;

string? userId = User.FindUserId();            // NameIdentifier / sub
string? name   = User.FindUserName();          // Identity.Name, then Name / name
string? email  = User.FindEmail();             // Email / email
IReadOnlyList<string> roles = User.FindRoles(); // every role, across all three spellings
bool canRead   = User.HasScope("api.read");    // splits the space-delimited scp / scope value
```

The namespace is `…Jwt.Extensions` for historical reasons — **these apply to every scheme**, not just JWT.

Two of them exist because the naive read is wrong:

- **`FindRoles()`** — roles reach a principal as `role`, `roles` (Entra app roles) or the `ClaimTypes.Role` URI
  (API key, ASP.NET Identity). Reading one spelling answers empty for the schemes using another.
- **`HasScope(scope)`** — scopes arrive as **one space-delimited string**, not one claim each
  (`"openid profile api.read"`). So `User.HasClaim("scp", "api.read")` is `false` against a token that plainly
  grants it.

---

## API Key Authentication

### `ApiKeyAuthenticationOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `ApiKeyHeaderName` | `string` | `"X-Api-Key"` | Request header name |
| `AuthenticationType` | `string` | `"ApiKey"` | Authentication type string |

### `IApiKeyOwnerService`

```csharp
Task<ApiKeyOwner?> FindByOwner(string id);
Task<ApiKeyOwner?> FindByKey(string apiKey);
Task<bool>         Validate(string id, string apiKey);
```

### `ApiKeyOwner` Model

| Property | Type | Description |
|---|---|---|
| `OwnerId` | `string` | Owner identifier |
| `Key` | `string` | API key value |
| `Roles` | `ICollection<string>` | Roles assigned to this key |
| `Claims` | `ICollection<ApiKeyOwner.Claim>` | Extra `(Type, Value)` pairs, emitted verbatim |

### DI Registration

```csharp
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

`appsettings.json` shape — an **array**, each entry carrying its own `OwnerId`. `ToApiKeyOwners()` maps one
child section per owner and requires both fields, so a dictionary keyed by owner name (`"client-a": { … }`)
throws `InvalidOperationException: OwnerId is missing` at startup:

```json
"ApiKeys": [
  { "OwnerId": "client-a", "Key": "key-abc", "Roles": ["read", "write"] }
]
```

### ⚠️ Claims emitted per scheme — the two spellings

The schemes do **not** agree on the role claim type, which decides whether a `RequireClaim` policy holds for
one caller and 403s for the other. A policy that must accept both cannot pin a single type:

| Claim | JWT bearer | API key |
|---|---|---|
| Identity | `ClaimTypes.NameIdentifier` (`sub` is renamed by `UseJwtClaimTypes`) | `ClaimTypes.NameIdentifier` ← `OwnerId` |
| Role | **`"role"`** — the plain JWT name, per `RoleClaimType` above | **`ClaimTypes.Role`** — the long `schemas.microsoft.com/…/role` URI |
| Anything else | the name the token carries | `Claims` entries, verbatim |

`[Authorize(Roles = …)]` works on both (each scheme's `ClaimsIdentity` resolves roles through its own
`RoleClaimType`). A hand-written check does not: `RequireClaim("role", "Administrator")` silently excludes
every API-key caller. For a mixed API, assert over both spellings —
`p.RequireAssertion(c => c.User.IsInRole(role) || c.User.HasClaim("role", role))` — or use `User.IsInRole`,
which each scheme already resolves correctly.

### Claim normalization — `RegiraClaimTypes` / `ClaimsNormalizer`

`Regira.Security.Authentication.Core`. The mechanism that stops the table above growing a row per scheme: a
handler hands its claims to `ClaimsNormalizer.Normalize(...)` and gets an identity carrying the canonical
spellings as well.

```csharp
var identity = ClaimsNormalizer.Normalize(claims, authenticationType);   // ClaimNormalizationOptions optional
```

Canonical set — the JWT spellings, so a normalized principal is indistinguishable from a JWT one:
`sub`, `name`, `email`, `role` (`RegiraClaimTypes`).

**Additive, never substitutive.** Every source claim survives and a canonical copy is added only when missing.
Folding Entra's `roles` into `role` and dropping the original would break a consumer reading it directly; adding
a `role` copy cannot break anything except code that counts claims. On a normalized principal all three checks
agree, whichever spelling the provider used:

| Check | Un-normalized | Normalized |
|---|---|---|
| `[Authorize(Roles = …)]` | resolves via the identity's own `RoleClaimType` | ✅ |
| `User.IsInRole(role)` | same | ✅ |
| `RequireClaim("role", …)` | pins one spelling — 403s the other scheme | ✅ |

`ClaimNormalizationOptions` holds the ordered source list per canonical claim (`SubjectClaimTypes`,
`NameClaimTypes`, `EmailClaimTypes`, `RoleClaimTypes`); first non-empty match wins, roles take every match, and
values are de-duplicated. Defaults cover `oid`, `preferred_username`, `upn`, `unique_name`, `roles` and the
`ClaimTypes` URIs.

⚠️ **`oid`, not `sub`, is the stable user id on an Entra token** — Entra's `sub` is pairwise per application, so
two apps see different values for the same person. `SubjectClaimTypes` lists both; key a row on `oid` when the
issuer is Entra.

⚠️ Normalization is **idempotent** and belongs at ticket construction. Running it per-request instead (an
`IClaimsTransformation`) re-reads a cached cookie principal on every request, so it would have to clone and
short-circuit or accumulate duplicate role claims.

### Configuration sections — `AuthenticationSections`

`Regira.Security.Authentication.Core.Models`. Constants for the `Authentication:` root convention: `Root`,
`Jwt`, `RefreshTokens` (nested — `Authentication:Jwt:RefreshTokens`), `Bearer`, `ApiKeys`, `Cookie`, `Oidc`,
`EntraId`.

```csharp
services.AddJwtAuthentication(o => configuration.GetSection(AuthenticationSections.Jwt).Bind(o));
```

---

## Refresh Tokens (self-issued JWT)

`Regira.Security.Authentication.Jwt.*`. Opt-in, chained off `AddJwtAuthentication`. Closes the gap every SPA on the
JWT scheme hits: a 2-hour access token with no way to renew it.

```csharp
services.AddJwtAuthentication(o => configuration.GetSection(AuthenticationSections.Jwt).Bind(o))
        .AddRefreshTokens();                      // in-memory store — development only
```

**Nothing changes for a host that does not call it.** Without an `IRefreshTokenService` registered, `POST auth`
returns the same body it always did (the `refreshToken` and `expiresAt` fields are *absent*, not null) and
`POST auth/refresh-token` answers `404`.

### ⚠️ `auth/refresh` and `auth/refresh-token` are different endpoints

| Endpoint | Anon | Needs | Use |
|---|:---:|---|---|
| `POST auth/refresh` | | a **still-valid** bearer token | Pick up role changes mid-session |
| `POST auth/refresh-token` | ✅ | the refresh token | Renew **after** the access token expired |

`auth/refresh` cannot help at the one moment renewal matters: once the access token has expired it answers `401`. A
SPA could only use it by polling ahead of expiry, and a tab left closed past the lifespan loses the session.
`auth/refresh-token` is anonymous by necessity — the refresh token *is* the credential — so **rate-limit it**.

### `RefreshTokenOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `LifeSpan` | `int` (s) | 14 days | One token's validity |
| `AbsoluteLifeSpan` | `int` (s) | 90 days | Ceiling on the whole rotation chain |
| `Rotate` | `bool` | `true` | Issue a new token on every use, revoking the presented one |
| `RevokeFamilyOnReuse` | `bool` | `true` | End the chain when an already-rotated token is presented again |
| `TokenByteLength` | `int` | `32` | Entropy of the generated token |
| `HashStoredTokens` | `bool` | `true` | Persist a hash rather than the token |

### The security model, and why each part is there

- **Opaque and random**, not a JWT — so it carries no claims that can go stale, and it cannot be read by its holder.
- **Rotated** on every use: a captured token is good for one call at most.
- **A replayed token ends the whole chain.** A rotated token should never appear twice, so a second presentation means
  two parties hold it and the server cannot tell which one is asking. Revoking the family also kills the *attacker's*
  freshly-minted token, not just the victim's.
- **Claims are re-read on every refresh**, via a resolver you supply. `IRefreshTokenService.Refresh` takes it as a
  required parameter precisely so it cannot be skipped: replaying the claims captured at sign-in keeps a role removed
  an hour ago in force, and a disabled account working, until the refresh token happens to expire. Return null from
  the resolver to refuse — which also revokes the chain.
- **`AbsoluteLifeSpan` caps the chain.** Without it a token rotated often enough never expires and a session lives
  forever.
- **Stored hashed** (unsalted SHA-256), so a leaked store yields nothing usable. Unsalted is deliberate: the store is
  looked up *by* the hash, which a per-token salt would make impossible, and a 256-bit random token has no guessable
  input for a slow KDF to protect. **That reasoning does not transfer to passwords** — use `IHasher` there.

### ⚠️ The in-memory store is development-only

`AddRefreshTokens()` falls back to `InMemoryRefreshTokenStore` when no store is registered. Two properties make it
unusable in production, and neither shows up on one developer machine: it **loses every session on restart**, and it
is **per-process** — so behind a load balancer a refresh lands on an instance that has never heard of the token and
users are signed out at random.

Implement `IRefreshTokenStore` over your own `DbContext` — five methods, of which only `TryRevoke` needs care — and
register it first:

```csharp
services.AddJwtAuthentication(…)
        .AddRefreshTokenStore<MyEfRefreshTokenStore>()
        .AddRefreshTokens();
```

`RevokeFamily` must reach tokens that are **already revoked** as well as active ones: it ends a chain, and a replayed
token is by definition one that was already revoked.

### Abstractions

```csharp
// IRefreshTokenService
Task<TokenPair>  Issue(string userId, IEnumerable<Claim> claims, string? audience = null, CancellationToken ct = default);
Task<TokenPair?> Refresh(string refreshToken, Func<string, Task<IEnumerable<Claim>?>> claimsResolver, CancellationToken ct = default);
Task             Revoke(string refreshToken, CancellationToken ct = default);
Task             RevokeAllForUser(string userId, CancellationToken ct = default);

// IRefreshTokenStore
Task<RefreshTokenRecord?> Find(string tokenKey, CancellationToken ct = default);
Task Store(RefreshTokenRecord record, CancellationToken ct = default);
Task<bool> TryRevoke(string tokenKey, string? replacedByTokenKey, DateTimeOffset revokedAt, CancellationToken ct = default);
Task RevokeFamily(string familyId, DateTimeOffset revokedAt, CancellationToken ct = default);
Task RevokeAllForUser(string userId, DateTimeOffset revokedAt, CancellationToken ct = default);
```

⚠️ **`TryRevoke` must be atomic, and it is a test-and-set for that reason.** It revokes only if the token is not
already revoked, and returns whether *this* caller did it. Two concurrent refreshes of one token — a SPA firing two
calls after a single `401` as much as an attacker racing the legitimate client — would otherwise both read
`RevokedAt == null`, both succeed, and split the family into two live chains with no replay detected. Returning
`false` is what lets the service treat the loser as a replay.

Implement it as a **conditional write**, never read-then-write: in EF, `ExecuteUpdate` filtered on
`RevokedAt == null` with a row-count check; in SQL, `UPDATE … WHERE TokenKey = @k AND RevokedAt IS NULL` returning
whether one row changed.

`TokenPair` is `(AccessToken, AccessTokenExpiresAt, RefreshToken?, RefreshTokenExpiresAt?)`. `RefreshTokenRecord`
holds `TokenKey`, `FamilyId`, `UserId`, `Audience`, `CreatedAt`, `ExpiresAt`, `FamilyExpiresAt`, `RevokedAt`,
`ReplacedByTokenKey` — note it does **not** hold the token itself.

Call `RevokeAllForUser` on a password change or when an account is disabled; nothing does it automatically.

---

## External Bearer Authentication (OIDC / Entra ID)

`Regira.Security.Authentication.Jwt.*`. Validating tokens **something else issued**. Use this — not
`AddJwtAuthentication` — whenever the issuer is Entra ID, Auth0, Keycloak, Duende or Okta.

`AddJwtAuthentication` cannot do this at all: it requires a `Secret` and always derives a symmetric key, while an
external authority signs with rotating asymmetric keys published at a JWKS endpoint. `AddBearerAuthentication`
registers **no `ITokenHelper`** — it reads tokens, it does not mint them.

### DI Registration

```csharp
// Any OpenID Connect provider: an authority and an audience is the whole configuration.
services.AddBearerAuthentication(o =>
{
    o.Authority = "https://your-tenant.eu.auth0.com/";
    o.Audience  = "https://api.example.com";
});

// Entra ID, as the values an app registration gives you
services.AddEntraIdBearer(o =>
{
    o.TenantId = configuration["Authentication:EntraId:TenantId"]!;
    o.ClientId = configuration["Authentication:EntraId:ClientId"]!;
});

// or bind Authentication:Bearer / Authentication:EntraId
services.AddBearerAuthentication(configuration);
services.AddEntraIdBearer(configuration);
```

### `BearerValidationOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `AuthenticationScheme` | `string` | `"Bearer"` | |
| `Authority` | `string?` | `null` | Issuer base URL; signing keys discovered and refreshed from it |
| `MetadataAddress` | `string?` | `null` | Overrides the derived metadata URL |
| `Secret` | `string?` | `null` | Shared symmetric key, for an issuer that signs with HMAC |
| `Audience` / `Audiences` | | `null` | `Audiences` wins when both are set |
| `ValidIssuers` | `ICollection<string>?` | `null` | Null ⇒ the discovery document's issuer |
| `RequireHttpsMetadata` | `bool` | `true` | |
| `SaveToken` | `bool` | `false` | Keep the raw token for calling a downstream API |
| `ValidateLifetime` | `bool` | `true` | |
| `ClockSkew` | `TimeSpan` | `Zero` | |
| `NameClaimType` / `RoleClaimType` | `string` | `"name"` / `"role"` | |
| `Claims` | `ClaimNormalizationOptions` | *(defaults)* | |
| `Configure` | `Action<JwtBearerOptions>?` | `null` | Applied before the normalization hook is chained on, so replacing `Events` cannot drop it |

**Exactly one source of signing keys** — either `Authority`/`MetadataAddress` or `Secret`. Both, or neither,
throws at registration naming which.

### `EntraIdOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `TenantId` | `string` | *(required)* | Directory id, or `organizations` / `common` for multi-tenant |
| `ClientId` | `string` | *(required)* | This API's application id |
| `Instance` | `string` | `https://login.microsoftonline.com` | Sovereign clouds differ |
| `UseV2Endpoint` | `bool` | `true` | |
| `Audiences` | `ICollection<string>?` | `null` | Defaults to **both** `api://{ClientId}` and `{ClientId}` |
| `AuthenticationScheme` | `string` | `"Bearer"` | |
| `SaveToken` | `bool` | `false` | |
| `Claims` | `ClaimNormalizationOptions` | *(defaults)* | |
| `Configure` | `Action<JwtBearerOptions>?` | `null` | |

Derived: `Authority` = `{Instance}/{TenantId}/v2.0` (no `/v2.0` when `UseV2Endpoint` is false);
`RoleClaimType` = `"roles"`; `ValidIssuers` = both the v2 and v1 spellings for a single tenant.

### ⚠️ Entra gotchas

1. **App roles arrive as `roles`, plural.** `role` singular matches nothing on an Entra token, so
   `[Authorize(Roles = "Admin")]` answers 403 against a token that visibly contains the role. `AddEntraIdBearer`
   sets `RoleClaimType = "roles"` and normalization adds a `role` copy, so both spellings work.
2. **`oid` is the stable user id; `sub` is not.** Entra's `sub` is *pairwise per application* — two apps see
   different values for the same person — so keying rows on `sub` silently fragments a user across apps.
3. **v1 vs v2 issuer.** A registration left at `accessTokenAcceptedVersion: null` issues **v1** tokens whose `iss`
   is `https://sts.windows.net/{tid}/`, not `{Instance}/{tid}/v2.0`. The mismatch surfaces as `IDX10205`, which
   names neither the version nor the setting. Both spellings are accepted for a single tenant, so this only bites
   if the audience form is also wrong.
4. **Multi-tenant has no fixed issuer.** With `organizations` or `common`, every tenant issues under its own id, so
   `ValidIssuers` is left unset and the issuer is validated against the token's own `tid` claim instead. Without
   that check the API accepts tokens from *any* directory — a valid token, just not from a tenant you agreed to
   trust. Signing in is also not authorization: a multi-tenant app must still decide whether a given tenant is
   entitled to anything.
5. **`groups` is object GUIDs, not names**, and above the token-size limit it is replaced entirely by
   `_claim_names`/`_claim_sources` — so a `groups`-based authorization model silently breaks for exactly the users
   who are in the most groups. Resolving it needs a Graph call, which is the case for taking
   `Microsoft.Identity.Web` directly.

### What these presets deliberately do not do

No `Microsoft.Identity.Web`, no MSAL. That covers protecting an API and signing users in, and stops there. It does
**not** cover: the on-behalf-of flow or any downstream call *as the user* (`ITokenAcquisition`), the MSAL token
cache, incremental consent and `MsalUiRequiredException`, Continuous Access Evaluation claims challenges,
managed-identity or certificate client credentials, or B2C / External ID user flows. If the app must call Graph or
another API on the user's behalf, take `Microsoft.Identity.Web` directly.

---

## Cookie Authentication

`Regira.Security.Authentication.Cookie.*`. For server-rendered apps, Blazor Server, and same-site SPAs served by
the API. No extra package — it is in the ASP.NET Core shared framework.

### DI Registration

```csharp
services.AddCookieAuthentication(o =>
{
    o.IsApi = true;                              // 401/403 instead of a 302 to LoginPath
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// or bind Authentication:Cookie
services.AddCookieAuthentication(configuration);
```

### `CookieAuthOptions`

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

```csharp
using Regira.Security.Authentication.Cookie.Extensions;

await HttpContext.SignInWithClaimsAsync(claims, isPersistent: true);   // normalizes first
await HttpContext.SignOutCookieAsync();
```

`SignInWithClaimsAsync` runs `ClaimsNormalizer` before signing in, so the canonical `sub`/`name`/`email`/`role`
spellings go **into the ticket** — normalization happens once, not per request. `expiresIn` overrides
`ExpireTimeSpan` for one session and only matters when `isPersistent` is set.

Called without a scheme, both helpers use the host's **default sign-in scheme**, which
`AddCookieAuthentication` sets (`DefaultSignInScheme` and `DefaultSignOutScheme`, `??=`, so an explicit choice
still wins). They deliberately never fall back to `DefaultScheme`: with `AddSchemeSelector` registered that is the
policy scheme, which forwards by credential and so cannot sign anyone in.

### ⚠️ Cookie gotchas

1. **`SecurePolicy.Always` means the cookie is never sent over plain HTTP.** The default, and correct — but over
   `http://` the effect is that sign-in *appears* to succeed and every subsequent request is anonymous: the cookie
   is issued, silently never returned, and the endpoint answers `401` as though the credentials were wrong. Serve
   dev over HTTPS, or set `SecurePolicy = SameAsRequest` for local development only.
2. **The cookie is Data Protection-encrypted.** A multi-instance or containerised host needs a **shared, persisted
   key ring** plus `SetApplicationName`, or every restart and every scale-out event invalidates every cookie. The
   symptom is "users get logged out at random" — never an error, and not reproducible on one machine.
3. **A cookie authenticates ambiently, so state-changing endpoints need antiforgery.** A bearer token does not,
   because the caller has to attach it. Moving from JWT to cookies inherits a vulnerability class the JWT scheme
   did not have.
4. **Set `IsApi` for anything a script calls.** The framework default `302`s to an HTML login page, which `fetch`
   follows transparently — the caller gets `200` and a page of HTML where it expected JSON, and cross-origin it
   surfaces as an opaque CORS error.
5. **A cross-site SPA needs `SameSite = None`**, which browsers accept only together with `Secure` — so it cannot
   work over plain HTTP at all — plus a CORS policy with `AllowCredentials`.
6. **A cookie is a stale snapshot.** Role changes and account disablement do not take effect until it expires; for
   immediate revocation, validate against a security stamp in `Configure`'s `Events.OnValidatePrincipal`.

---

## Interactive Sign-in (OpenID Connect)

`Regira.Security.Authentication.OpenIdConnect.*`. Signing users in through a browser: authorization code + PKCE,
landing in a cookie session. Needs `Microsoft.AspNetCore.Authentication.OpenIdConnect` (already referenced).

This is always a **pair** of schemes — the OIDC handler runs the challenge and the code exchange, a cookie holds
the session — and `AddOidcAuthentication` registers both and wires their defaults.

### DI Registration

```csharp
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

### Scheme pairing — what `AddOidcAuthentication` sets

| Default | Scheme | Why |
|---|---|---|
| `DefaultScheme` / `DefaultAuthenticateScheme` / `DefaultSignInScheme` | the **cookie** | the session, read on every request |
| `DefaultChallengeScheme` / `DefaultSignOutScheme` | the **OIDC** scheme | starts and ends the flow at the provider |

Getting this backwards is the usual reason a hand-rolled setup fails: an `[Authorize]` endpoint either tries to
validate an id_token it does not have, or redirects to the provider on every single request.

### `OidcAuthOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `AuthenticationScheme` | `string` | `"OpenIdConnect"` | |
| `SignInScheme` | `string?` | `null` | Defaults to `Cookie`'s scheme |
| `Authority` / `ClientId` | `string` | *(required)* | |
| `ClientSecret` | `string?` | `null` | Required for the confidential-client code exchange |
| `ResponseType` | `string` | `"code"` | |
| `Scopes` | `ICollection<string>` | `openid profile email` | **Replaces** the handler's defaults |
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

`EntraIdSignInOptions` is the preset: `TenantId`, `ClientId`, `ClientSecret`, `Instance`, `UseV2Endpoint`,
`Scopes`, `CallbackPath`, `SignedOutCallbackPath`, `SignedOutRedirectUri`, `SaveTokens`, and a
`Configure` reaching the full `OidcAuthOptions`. It derives the authority, sets `RoleClaimType = "roles"`, and
handles single- vs multi-tenant issuer validation.

Claims are normalized in `OnTokenValidated`, **before** the principal is handed to the cookie — so the canonical
spellings are written into the ticket rather than recomputed per request.

### ⚠️ OIDC gotchas

1. **"Correlation failed" behind a reverse proxy.** A proxy that terminates TLS makes the handler build
   `redirect_uri` from the internal, plain-HTTP request. The provider rejects it as unregistered, or accepts it and
   returns the browser to the wrong origin, where the correlation cookie set on the original origin is not sent
   back. Configure `UseForwardedHeaders` **ahead of** the authentication middleware.
2. **Sign-out needs both halves.** `SignOutAsync` on the cookie scheme *and* on the OIDC scheme. Clearing only the
   cookie leaves the provider session intact, so the next challenge signs the user straight back in with no prompt
   and "log out" appears to do nothing.
3. **`CallbackPath` must match a registered redirect URI exactly** — scheme, host and port included.
4. **`SaveTokens = true` is required** for anything that later needs the access or refresh token, and it makes the
   cookie considerably larger. Leave it off unless a downstream call needs it.
5. **A multi-tenant sign-in has the same issuer hole as a multi-tenant API** — see the Entra notes above. It is
   closed automatically, and the check is chained *after* your `Configure` delegate so customization cannot drop it.

**Not covered in-process:** the code exchange itself needs a live provider. The tests cover the challenge boundary
(authorize URL, PKCE, state, correlation cookie) and the option shaping; verify the full round trip against a real
tenant.

---

## Composing multiple schemes — `AddSchemeSelector`

`Regira.Security.Authentication.Core.Extensions`. Registers a policy scheme that forwards each request to the
scheme matching the credential it carries, and makes itself the default authenticate and challenge scheme.

```csharp
services.AddApiKeyAuthentication()
        .AddInMemoryApiKeyAuthentication(configuration.GetSection("Authentication:ApiKeys").ToApiKeyOwners());

services.AddJwtAuthentication(o => configuration.GetSection("Authentication:Jwt").Bind(o))
        .AddSchemeSelector();     // ⚠️ last
```

⚠️ **Call it last.** Every `Add…Authentication` sets its own default scheme, so without the selector the
*registration order* silently decides which handler an unattributed `[Authorize]` uses — and the symptom is a
401 for a caller holding a perfectly good credential of the other kind. `AddSchemeSelector` takes that decision
over, and a rule naming a scheme that is not registered is skipped, so the order stops mattering.

With the selector in place, **no `AddAuthorization` default policy is needed** to name the schemes:
`MapControllers().RequireAuthorization()` authenticates against the selector, which forwards.

### Built-in rules

Evaluated in ascending order, first match wins:

| Order | Forwards to | When |
|---|---|---|
| 20 | the bearer scheme | `Authorization: Bearer …` (scheme token matched case-insensitively) |
| 50 | `ApiKeyDefaults.AuthenticationScheme` | the scheme's `ApiKeyHeaderName` present **and non-empty** |
| 60 | the cookie scheme | the scheme's cookie is present (name read from its registered options) |
| — | `FallbackScheme` | no rule matched |

A **blank** API-key header deliberately does not match: `ApiKeyAuthenticationHandler` answers `NoResult` for a
blank key, so forwarding there would spend the request's one choice of handler and 401 without the bearer scheme
ever being offered.

**A scheme under a non-default name contributes its own rule.** The built-in rules can only name the defaults, so
`AddCookieAuthentication` registers a rule for whatever `AuthenticationScheme` it was given — without which a cookie
issued under a custom name authenticates as nobody, silently. Contributed rules are evaluated after any you added
explicitly and before the built-in ones.

`FallbackScheme` left unset resolves to the lowest-ordered registered rule — the bearer scheme for an API, which
gives an unauthenticated caller a `401` with `WWW-Authenticate: Bearer` rather than a bare `401`.

### `SchemeSelectorOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `AuthenticationScheme` | `string` | `"Smart"` | Name of the policy scheme |
| `DisplayName` | `string` | *(descriptive)* | Display name of the policy scheme |
| `FallbackScheme` | `string?` | `null` | Scheme for a request carrying no recognised credential |
| `ChallengeScheme` | `string?` | `null` | Scheme that answers a challenge; see below |
| `ForwardChallengeToSignInScheme` | `bool` | `true` | Use a registered OpenID Connect scheme for challenges |
| `Rules` | `IList<SchemeForwardRule>` | `[]` | Extra rules; added before the built-in ones |
| `UseDefaultRules` | `bool` | `true` | Whether to include the bearer, API-key and cookie rules |

Calling `AddSchemeSelector` **twice throws** — a second options instance would leave the expander describing schemes
the selector does not forward to.

### ⚠️ Challenges are not decided by the rules

The forwarding rules key on the credential a request **carries**, and a browser arriving at a guarded page carries
none. Left to the rules, a challenge therefore resolves to the lowest-ordered registered rule — a bearer `401` — which
in an app built around interactive sign-in makes login unreachable while nothing errors.

So the challenge is routed separately:

1. `ChallengeScheme`, if set.
2. Otherwise a registered **OpenID Connect** scheme, when `ForwardChallengeToSignInScheme` is on (the default).
   `AddOidcAuthentication` / `AddEntraIdSignIn` register themselves as that scheme.
3. Otherwise the rules — correct for an API, which wants `401` with `WWW-Authenticate`.

Only the *challenge* is delegated. Authenticate, sign-in and sign-out still route by credential, and **forbid**
deliberately stays with the rules: a forbidden caller is authenticated, so their credential identifies the scheme
that should render the refusal (an access-denied redirect for a cookie session, `403` for a bearer call).

A host serving **both** browsers and API clients should set `ChallengeScheme` deliberately — whichever it names, the
other kind of caller gets the wrong answer to a missing credential. Where the two must differ, name the scheme per
endpoint with `[Authorize(AuthenticationSchemes = …)]`.

Add a rule with `SchemeForwardRules.Bearer(scheme)` / `.Basic(scheme)` / `.ApiKey(scheme)` /
`.Cookie(scheme, cookieName)`, or construct `new SchemeForwardRule(order, scheme, context => …)`. A rule's
predicate decides *which handler gets the chance* — it must not authenticate anything, and a false positive
costs the request its other options.

### ⚠️ OpenAPI: two transformers, whatever the scheme count

```csharp
services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<AuthenticationSchemeDocumentTransformer>();
    options.AddOperationTransformer<SecurityRequirementOperationTransformer>();
});
```

`AuthenticationSchemeDocumentTransformer` declares **every** registered scheme under
`components.securitySchemes`, from a descriptor each `Add…Authentication` contributes at registration — so adding
a scheme needs no transformer change. A descriptor naming a scheme that was never registered is skipped.

`SecurityRequirementOperationTransformer` marks each guarded operation. It is what stops the policy scheme
breaking the document: the selector's scheme authenticates nothing and is declared by nobody, so the transformer
resolves it to the schemes behind it through `IAuthenticationSchemeExpander` (registered by `AddSchemeSelector`)
and emits one requirement per real scheme — OR semantics, the caller needs any one of them.

`BearerSecuritySchemeTransformer` and `ApiKeySecurityDocumentTransformer` still work and may be registered
alongside; all three insert with `TryAdd`, so the result is the same.

Cookie has no dedicated OpenAPI security type — it is emitted as an API key with `in: cookie`, which is the
accepted convention.

---

## Add JWT authentication — wiring recipe

Users in the same DB, no roles — the happy path (details in the sections below):

1. `AppUser : IdentityUser`; make the app `DbContext : IdentityDbContext<AppUser>` (users in the same DB).
2. `services.AddIdentityCore<AppUser>().AddEntityFrameworkStores<AppDbContext>().AddSignInManager().AddDefaultTokenProviders();`
3. `services.AddJwtAuthentication(o => configuration.GetSection(AuthenticationSections.Jwt).Bind(o));` — set `Authentication:Jwt:Secret` (**≥ 64 bytes for the HS512 default**, or startup throws — counted as ASCII, so a non-ASCII character is one byte, not the two or three UTF-8 would give it), `Authentication:Jwt:Authority`, and **`Authentication:Jwt:Audience` = the SPA's `clientApp`**; leave `Algorithm` unset (or `"HS512"`). Add `.AddRefreshTokens()` if the SPA needs to survive access-token expiry, and `.AddSchemeSelector()` last if more than one scheme is registered.
4. Register an `IEmailSender` — the interface is **`Microsoft.AspNetCore.Identity.UI.Services.IEmailSender`** (package `Microsoft.AspNetCore.Identity.UI`, not a Regira type). Use Regira's `IdentityMailer`, or a dev logger that prints the link. No `ISerializer` needed.
5. Subclass the three base controllers with forwarding ctors (below).
6. `app.UseAuthentication()` **before** `app.UseAuthorization()`; `MapControllers().RequireAuthorization()` — the bases carry no `[Authorize]`.
7. Seed the first user via `UserManager<AppUser>`.
8. **Front-end:** install `authPlugin` after router + axios and pass `clientApp: "<your audience>"` — the SPA's `login()` appends it as `?clientApp=`, so it must equal the API's `Jwt:Audience`. Leave `loginUrl` unset unless the login endpoint isn't `auth`.
9. **Dev cross-origin:** either proxy the SPA's `/api` through Vite (no CORS needed) or point the SPA straight at the API origin and add a CORS policy allowing that origin. The full recipe (proxy config, URL alignment, HTTPS-redirect trap) is in the front-end guide: `regira_modules.vue.entities` → `entities.setup` → *Calling the API in dev* / *The URL contract*.

## Pre-built Auth Controllers (`Security.Authentication.Web`)

> ⚠️ **Custom JWT over a plain (non-`IdentityUser`) entity forgoes this whole account surface.** The recipe above
> and the controllers below sit on ASP.NET Identity (`IdentityUser` / `UserManager`). Minting tokens for a plain
> entity (an `Employee`-as-user) with `ITokenHelper.Create(claims, audience)` gets you **login only** — not
> `PasswordController` (change/recover/reset), `AccountController`, or `UserController`. Treat auth as a package:
> decide Identity-vs-custom **before** modelling the user, and if custom, implement change/forgot/reset yourself
> (or adopt the Identity-backed controllers wholesale). "Login works" is not "auth is done."

Three abstract base controllers over ASP.NET Core Identity's `UserManager<TUser>` — `Regira.Security.Authentication.Web.Controllers`. Subclass each with a closed `TUser : IdentityUser<string>` (`UserControllerBase` also needs `new()`). `[ApiController]` and the route templates live on the bases and are inherited — **do not add `[ApiController]`/`[Route]` to the subclass**:

```csharp
using Regira.Security.Authentication.Web.Controllers;

public class AccountController(ITokenHelper tokenHelper, UserManager<AppUser> userManager,
    IUserClaimsPrincipalFactory<AppUser> claimsFactory, ILogger<AccountController> logger)
    : AccountControllerBase<AppUser>(tokenHelper, userManager, claimsFactory, logger);

public class PasswordController(UserManager<AppUser> userManager)
    : PasswordControllerBase<AppUser>(userManager);

public class UsersController(UserManager<AppUser> userManager)
    : UserControllerBase<AppUser>(userManager);
```

**Required services:** `AddIdentityCore<TUser>().AddEntityFrameworkStores<…>().AddDefaultTokenProviders()` (`AddDefaultTokenProviders` is mandatory — recover/confirm generate Identity tokens); `AddJwtAuthentication(…)` for `ITokenHelper`; an `IEmailSender` (Regira's `IdentityMailer`). Token payloads use `System.Text.Json` internally — no serializer registration needed.

### Authorization: guarded by default

**Rule: every endpoint requires an authenticated user unless it declares `[AllowAnonymous]`.** The controllers carry no per-action `[Authorize]` — the host MUST apply a global requirement when mapping them, or the non-anonymous endpoints are silently public:

```csharp
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers().RequireAuthorization(/* optional policy */);
});
```

`[AllowAnonymous]` endpoints (the only public ones): `POST auth`, `POST auth/refresh-token`,
`POST auth/password/recover`, `POST auth/password/reset`, `POST users/confirm-email`.

`auth/refresh-token` is public only when refresh tokens are registered — otherwise it answers `404` — and it is the
one that most needs rate-limiting, since the refresh token it accepts is itself the credential.

⚠️ **`POST users` (create user) is guarded** — the default suits an invite-only back office. For **self-registration**, override it:

```csharp
[AllowAnonymous]
public override Task<ActionResult<TUserDto>> Create(UserInput input) => base.Create(input);
```

Open sign-up means anyone can create an account: rate-limit it, and keep any role/tenant assignment server-side rather than reading it from the payload.

### `AccountControllerBase<TUser>` — route `auth`

| Endpoint | Anon | Request | Success | Failure |
|---|:---:|---|---|---|
| `POST auth?clientApp=…` | ✅ | `{ username, password }` | `200 { isAuthenticated, token }` — plus `refreshToken`, `expiresAt` when refresh tokens are registered | `401 { isLockedOut, lockedOutEnd }` |
| `POST auth/validate` |  | *(bearer)* | `204` | `401` / `403` (user gone) |
| `POST auth/refresh` |  | *(bearer, **still valid**)* | `200 { isAuthenticated, token }` | `401` |
| `POST auth/refresh-token` | ✅ | `{ refreshToken }` | `200 { isAuthenticated, token, refreshToken, expiresAt }` | `401`; `404` when refresh tokens are not registered |
| `GET auth/personal-data` |  | *(bearer)* | `200 { given_name, family_name }` | `401` |

`clientApp` = token audience — **set the API's `Jwt:Audience` (or `Audiences`) equal to the SPA's `clientApp`**,
or every authenticated call 401s with `audience invalid`. Success resets the failed-access count; failure
increments it (Identity lockout).

### `PasswordControllerBase<TUser>` — route `auth/password`

| Endpoint | Anon | Request | Success | Failure |
|---|:---:|---|---|---|
| `POST auth/password` |  | `{ currentPassword, newPassword }` | `200` | `400` / `404` |
| `POST auth/password/recover` | ✅ | `{ username, siteUrl, siteName }` | `200` (always) | — |
| `POST auth/password/reset` | ✅ | `{ token, password }` | `200` | `400` malformed token / identity errors |

`recover` always returns `200` (no user enumeration) and emails a Base64 `token` (reset token + username). `reset` returns `400` on a malformed token.

### `UserControllerBase<TUser>` — route `users`

| Endpoint | Anon | Request | Success | Failure |
|---|:---:|---|---|---|
| `POST users` |  | `{ username, password, confirmEmailUrl? }` | `200` | `400` |
| `POST users/confirm-email` | ✅ | `{ token, userName }` | `200` | `400` malformed token / identity errors |

`username` is used as both user name and email. With `confirmEmailUrl`, a confirmation email with a Base64 `token` is sent. Creating an existing user is a no-op `200`.

---

## Namespace Quick Reference

> **AI Agent Rule**: Always use exact namespaces. Do NOT guess or invent namespaces.

### `Regira.Security` package

| Type | Namespace |
|---|---|
| `IHasher` | `Regira.Security.Abstractions` |
| `Hasher` (PBKDF2) | `Regira.Security.Hashing` |
| `SimpleHasher` (double-SHA) | `Regira.Security.Hashing` |
| `IEncrypter` | `Regira.Security.Encryption` |
| `SymmetricEncrypter` | `Regira.Security.Encryption` |
| `AesEncrypter` | `Regira.Security.Encryption` |
| `CryptoOptions` | `Regira.Security.Core` |

### `Regira.Security.Hashing.BCryptNet` package

| Type | Namespace |
|---|---|
| `Hasher` (BCrypt) | `Regira.Security.Hashing.BCryptNet` |

### `Regira.Security.Authentication` package

| Type | Namespace |
|---|---|
| `ITokenHelper` | `Regira.Security.Authentication.Jwt.Abstraction` |
| `JwtTokenOptions` | `Regira.Security.Authentication.Jwt.Models` |
| `AddJwtAuthentication()` (extension) | `Regira.Security.Authentication.Jwt.Extensions` |
| `FindUserId()` / `FindUserName()` / `FindEmail()` / `FindRoles()` / `HasScope()` (extensions on `ClaimsPrincipal`) | `Regira.Security.Authentication.Jwt.Extensions` |
| `IApiKeyOwnerService` | `Regira.Security.Authentication.ApiKey.Abstraction` |
| `ApiKeyOwner` | `Regira.Security.Authentication.ApiKey.Models` |
| `ApiKeyAuthenticationOptions` | `Regira.Security.Authentication.ApiKey.Models` |
| `AddApiKeyAuthentication()` (extension) | `Regira.Security.Authentication.ApiKey.Extensions` |
| `RegiraClaimTypes` | `Regira.Security.Authentication.Core.Models` |
| `ClaimNormalizationOptions` | `Regira.Security.Authentication.Core.Models` |
| `AuthenticationSections` | `Regira.Security.Authentication.Core.Models` |
| `SchemeSelectorOptions` / `SchemeSelectorDefaults` | `Regira.Security.Authentication.Core.Models` |
| `SchemeForwardRule` / `SchemeForwardRules` | `Regira.Security.Authentication.Core.Models` |
| `ClaimsNormalizer` | `Regira.Security.Authentication.Core.Services` |
| `IAuthenticationSchemeExpander` / `ISecuritySchemeDescriptor` | `Regira.Security.Authentication.Core.Abstraction` |
| `SecuritySchemeDescriptor` / `SecuritySchemeKind` | `Regira.Security.Authentication.Core.Models` |
| `AddSchemeSelector()` (extension) | `Regira.Security.Authentication.Core.Extensions` |
| `IRefreshTokenService` / `IRefreshTokenStore` | `Regira.Security.Authentication.Jwt.Abstraction` |
| `RefreshTokenOptions` / `RefreshTokenRecord` / `TokenPair` | `Regira.Security.Authentication.Jwt.Models` |
| `RefreshTokenService` / `InMemoryRefreshTokenStore` | `Regira.Security.Authentication.Jwt.Services` |
| `AddRefreshTokens()` / `AddRefreshTokenStore<T>()` (extensions) | `Regira.Security.Authentication.Jwt.Extensions` |
| `BearerValidationOptions` | `Regira.Security.Authentication.Jwt.Models` |
| `EntraIdOptions` / `EntraIdDefaults` | `Regira.Security.Authentication.Jwt.Models` |
| `AddBearerAuthentication()` / `AddEntraIdBearer()` (extensions) | `Regira.Security.Authentication.Jwt.Extensions` |
| `OidcAuthOptions` / `EntraIdSignInOptions` | `Regira.Security.Authentication.OpenIdConnect.Models` |
| `AddOidcAuthentication()` / `AddEntraIdSignIn()` (extensions) | `Regira.Security.Authentication.OpenIdConnect.Extensions` |
| `CookieAuthOptions` / `CookieAuthDefaults` | `Regira.Security.Authentication.Cookie.Models` |
| `AddCookieAuthentication()` (extension) | `Regira.Security.Authentication.Cookie.Extensions` |
| `SignInWithClaimsAsync()` / `SignOutCookieAsync()` (extensions on `HttpContext`) | `Regira.Security.Authentication.Cookie.Extensions` |
