# Regira Security — JWT Authentication

Self-issued JWT bearer tokens and their refresh tokens, from `Regira.Security.Authentication`. For tokens issued by an external identity provider, see [External Identity Providers](external-auth.md).

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

<!-- no-compile -->
```csharp
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

<!-- no-compile -->
```csharp
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

<!-- no-compile -->
```csharp
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

## Overview

1. [Index](../README.md) — Overview, projects, and choosing a scheme
1. [Encryption & Hashing](cryptography.md) — Symmetric encryption, PBKDF2 and BCrypt password hashing
1. **[JWT Authentication](jwt.md)** — Self-issued bearer tokens, claims, and refresh tokens
1. [API Key Authentication](api-keys.md) — Key-based auth for machine callers
1. [External Identity Providers](external-auth.md) — Validating external bearer tokens; OpenID Connect sign-in
1. [Cookie Authentication](cookies.md) — Cookie-backed sessions
1. [Composing Multiple Schemes](schemes.md) — Running several schemes side by side
1. [Pre-built Auth Controllers](controllers.md) — Account, password and user endpoints
1. [Practical Examples](examples.md) — Complete implementation examples

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
