# Security — Example: Staff Portal Authentication

> Context: A back-office web API. Staff sign in with a password and get a JWT; a CI/CD integration authenticates
> with an API key. Later sections show the same portal moved onto cookie sessions or Entra ID.

## DI Registration

```csharp
// Program.cs
services.AddSingleton<IHasher, Regira.Security.Hashing.BCryptNet.Hasher>();

services.AddApiKeyAuthentication()
        .AddInMemoryApiKeyAuthentication(
            configuration.GetSection(AuthenticationSections.ApiKeys).ToApiKeyOwners());

services.AddJwtAuthentication(o => configuration.GetSection(AuthenticationSections.Jwt).Bind(o))
        .AddRefreshTokens()      // in-memory store — development only
        .AddSchemeSelector();    // ⚠️ last: it forwards per credential and owns the default scheme
```

`AddSchemeSelector()` goes **last**. Every `Add…Authentication` sets its own default scheme, so without it the
registration order decides which handler an unattributed `[Authorize]` uses — and the symptom is a 401 for a caller
holding a perfectly good credential of the other kind.

`appsettings.json` — `ApiKeys` is an **array**, each entry carrying its own `OwnerId`:

```json
{
  "Authentication": {
    "ApiKeys": [
      { "OwnerId": "ci-pipeline", "Key": "ci-secret-key", "Roles": ["read"] }
    ],
    "Jwt": {
      "Secret": "at-least-64-characters-for-the-HS512-default-or-startup-throws-0123",
      "Authority": "https://portal.example",
      "Audience": "staff-spa",
      "LifeSpan": 3600
    }
  }
}
```

`Authentication:Jwt:Secret` must be ≥ 64 bytes for the `HS512` default (≥ 48 for `HS384`, ≥ 32 for `HS256`) — a
shorter one throws from `AddJwtAuthentication` at startup. Length is counted as ASCII, the encoding the signing key is
derived from, so a non-ASCII character contributes one byte rather than the two or three UTF-8 would give it.
`Authentication:Jwt:Audience` must equal the SPA's `clientApp`.

## Register a new staff member

```csharp
public async Task Register(string email, string plainPassword)
{
    var stored = _hasher.Hash(plainPassword);
    await _userRepository.Add(new StaffUser { Email = email, PasswordHash = stored });
}
```

## Login and issue a token pair

```csharp
public async Task<TokenPair?> Login(string email, string plainPassword)
{
    var user = await _userRepository.FindByEmail(email);
    if (user == null || !_hasher.Verify(plainPassword, user.PasswordHash!))
        return null;

    var claims = new[]
    {
        new Claim(RegiraClaimTypes.Subject, user.Id.ToString()),
        new Claim(RegiraClaimTypes.Name,    user.DisplayName ?? email),
        new Claim(RegiraClaimTypes.Email,   email),
        // ⚠️ the plain "role" claim, not ClaimTypes.Role — JwtTokenOptions.RoleClaimType is "role",
        // so the long URI would reach the principal under a type nothing resolves against.
        new Claim(RegiraClaimTypes.Role,    user.Role)
    };

    return await _refreshTokenService.Issue(user.Id.ToString(), claims);
}
```

Without `AddRefreshTokens()` this is `_tokenHelper.Create(claims)` returning the access token alone.

## Renew an expired access token

```csharp
// POST auth/refresh-token  { "refreshToken": "…" }
var pair = await _refreshTokenService.Refresh(refreshToken, async userId =>
{
    var user = await _userRepository.Find(userId);
    // Returning null refuses the refresh and revokes the whole chain.
    return user is { IsActive: true } ? BuildClaims(user) : null;
});
```

The resolver is required, and re-reads the user on purpose: replaying the claims captured at sign-in would keep a role
removed an hour ago in force until the refresh token expired.

## The same portal on cookie sessions

```csharp
services.AddCookieAuthentication(o =>
{
    o.IsApi = true;                             // 401/403 rather than a 302 to an HTML login page
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// in the login endpoint
await HttpContext.SignInWithClaimsAsync(claims);
await HttpContext.SignOutCookieAsync();
```

Two things a library cannot do for you: serve over **HTTPS** (the cookie is `Secure` by default, so over plain HTTP
it is issued and never sent back — sign-in looks fine and every later request 401s), and persist a **shared Data
Protection key ring** with `SetApplicationName` (without it, every restart and scale-out event logs everyone out).

## Protect the same API with Entra ID

```csharp
services.AddEntraIdBearer(o =>
{
    o.TenantId = configuration["Authentication:EntraId:TenantId"]!;
    o.ClientId = configuration["Authentication:EntraId:ClientId"]!;
});
```

Entra app roles arrive as **`roles`** (plural), and both `api://{clientId}` and `{clientId}` are accepted as the
audience. Key your rows on **`oid`**, not `sub` — Entra's `sub` is pairwise per application.

## Sign staff in with Entra ID

```csharp
services.AddEntraIdSignIn(o =>
{
    o.TenantId     = configuration["Authentication:EntraId:TenantId"]!;
    o.ClientId     = configuration["Authentication:EntraId:ClientId"]!;
    o.ClientSecret = configuration["Authentication:EntraId:ClientSecret"]!;
});
```

Registers the cookie + OIDC pair. Sign-out needs **both** schemes, or the next challenge silently signs the user
straight back in.

## Read the caller, whatever signed them in

```csharp
string? userId = User.FindUserId();
IReadOnlyList<string> roles = User.FindRoles();   // covers "role", "roles" and ClaimTypes.Role
bool canRead = User.HasScope("api.read");         // splits the space-delimited scp / scope value
```

`FindRoles()` and `HasScope()` exist because the naive read is wrong: roles reach a principal under three spellings,
and scopes arrive as one space-delimited string rather than one claim each.

## Encrypt a third-party API key at rest

```csharp
// AesEncrypter produces a different ciphertext each call — safe for stored secrets
var enc = new AesEncrypter(new CryptoOptions { Secret = configuration["Crypto:Secret"] });

string stored = enc.Encrypt(apiKey);   // store in DB
string plain  = enc.Decrypt(stored);   // retrieve when needed
```
