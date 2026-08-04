# Regira Security — Examples

## Example 1: Hash and verify a password

```csharp
// BCrypt is the recommended hasher for passwords
IHasher hasher = new Regira.Security.Hashing.BCryptNet.Hasher();

// On registration
string stored = hasher.Hash(userInput.Password);
// Persist 'stored' to the database

// On login
bool valid = hasher.Verify(loginInput.Password, stored);
if (!valid)
    return Unauthorized();
```

---

## Example 2: Encrypt and decrypt a stored value

Use `AesEncrypter` for values that must be recoverable (API keys, tokens, PII).

```csharp
var enc = new AesEncrypter(new CryptoOptions
{
    Secret = configuration["Crypto:Secret"]
});

// Encrypt before storing
string cipher = enc.Encrypt(creditCardNumber);
await db.SaveAsync(new StoredSecret { Value = cipher });

// Decrypt on retrieval
var record    = await db.LoadAsync(id);
string plain  = enc.Decrypt(record.Value);
```

---

## Example 3: JWT authentication setup

```csharp
// Program.cs
services.AddJwtAuthentication(options => configuration.GetSection("Authentication:Jwt").Bind(options));

app.UseAuthentication();
app.UseAuthorization();
```

`Authentication:Jwt:Secret` must be at least 64 bytes for the `HS512` default — a shorter one throws at startup.

Issue a token after verifying credentials:

```csharp
public class AuthController(ITokenHelper tokens, UserManager<AppUser> users) : ControllerBase
{
    [HttpPost("auth")]
    public async Task<IActionResult> Authenticate([FromBody] AuthenticateInput input)
    {
        var user = await users.FindByNameAsync(input.Username);
        if (user == null || !await users.CheckPasswordAsync(user, input.Password))
            return Unauthorized();

        var claims = new[]
        {
            new Claim(RegiraClaimTypes.Subject, user.Id),
            new Claim(RegiraClaimTypes.Name,    user.UserName!),
            new Claim(RegiraClaimTypes.Email,   user.Email!),
            // The plain "role" claim — JwtTokenOptions.RoleClaimType is "role", so ClaimTypes.Role
            // would reach the principal under a type nothing resolves against.
            new Claim(RegiraClaimTypes.Role,    "staff")
        };

        return Ok(new { Token = tokens.Create(claims) });
    }
}
```

---

## Example 4: API Key authentication

```csharp
// Program.cs — in-memory keys
services.AddApiKeyAuthentication()
        .AddInMemoryApiKeyAuthentication(new[]
        {
            new ApiKeyOwner { OwnerId = "partner-a", Key = "sk-abc123", Roles = ["read"] },
            new ApiKeyOwner { OwnerId = "admin",     Key = "sk-xyz789", Roles = ["read", "write"] }
        });

app.UseAuthentication();
app.UseAuthorization();
```

Protect an endpoint:

```csharp
[Authorize(AuthenticationSchemes = ApiKeyDefaults.AuthenticationScheme)]
[HttpGet("data")]
public IActionResult GetData() => Ok(data);
```

---

## Example 7: One API serving several schemes

Add `AddSchemeSelector()` **last**. It forwards each request to the scheme matching the credential it carries and
takes over the default-scheme decision, so the order the schemes were registered in stops mattering:

```csharp
services.AddApiKeyAuthentication()
        .AddInMemoryApiKeyAuthentication(configuration.GetSection("Authentication:ApiKeys").ToApiKeyOwners());

services.AddJwtAuthentication(o => configuration.GetSection("Authentication:Jwt").Bind(o))
        .AddSchemeSelector();
```

No `AddAuthorization` default policy is needed to name the schemes — `MapControllers().RequireAuthorization()`
authenticates against the selector, which forwards.

---

## Example 8: Cookie sessions

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

Serve over HTTPS — the cookie is `Secure` by default, so over plain HTTP it is issued and never sent back, and every
request after sign-in is silently anonymous. Multi-instance hosting also needs a shared, persisted Data Protection key
ring plus `SetApplicationName`.

---

## Example 9: Microsoft Entra ID

Protect an API with Entra-issued tokens:

```csharp
services.AddEntraIdBearer(o =>
{
    o.TenantId = configuration["Authentication:EntraId:TenantId"]!;
    o.ClientId = configuration["Authentication:EntraId:ClientId"]!;
});
```

Sign users in interactively (registers the cookie + OIDC pair):

```csharp
services.AddEntraIdSignIn(o =>
{
    o.TenantId     = configuration["Authentication:EntraId:TenantId"]!;
    o.ClientId     = configuration["Authentication:EntraId:ClientId"]!;
    o.ClientSecret = configuration["Authentication:EntraId:ClientSecret"]!;
});
```

Entra app roles arrive as `roles` (plural), both `api://{clientId}` and `{clientId}` are accepted as the audience,
and `oid` — not `sub` — is the stable user id.

---

## Example 10: Refresh tokens

```csharp
services.AddJwtAuthentication(o => configuration.GetSection("Authentication:Jwt").Bind(o))
        .AddRefreshTokenStore<MyEfRefreshTokenStore>()   // the in-memory default is development-only
        .AddRefreshTokens();
```

`POST auth/refresh-token` then exchanges a refresh token for a new pair. It is anonymous — the refresh token *is* the
credential — so rate-limit it. Note `auth/refresh` is a different endpoint that needs a **still-valid** bearer token
and so cannot renew an expired one.

---

## Example 5: Pre-built AccountController

Inherit the base controller to get `auth`, `auth/validate`, `auth/refresh`, `auth/refresh-token` and
`auth/personal-data` for free. `[ApiController]` and `[Route("auth")]` are declared on the base and inherited — do
**not** repeat them on the subclass, or the route template is overridden:

```csharp
public class AuthController(
    ITokenHelper tokens,
    UserManager<AppUser> users,
    IUserClaimsPrincipalFactory<AppUser> factory)
    : AccountControllerBase<AppUser>(tokens, users, factory);
```

---

## Example 6: Pre-built UserController with email confirmation

`UserControllerBase<TUser>` takes the `UserManager` alone — no serializer; token payloads use `System.Text.Json`
internally. As above, the base declares `[ApiController]` and `[Route("users")]`:

```csharp
public class UsersController(UserManager<AppUser> users)
    : UserControllerBase<AppUser>(users);

// POST /users — creates user, sends confirmation email
// POST /users/confirm-email — confirms with token
```

Register the mailer so Identity confirmation emails are sent:

```csharp
services.AddSingleton<IEmailSender>(provider =>
    new IdentityMailer(
        provider.GetRequiredService<IMailService>(),
        new IdentityMailerOptions { Sender = "no-reply@example.com" }
    ));
```

---

## Overview

1. [Index](../README.md) — Overview, encryption, hashing, and every authentication scheme
1. **[Examples](examples.md)** — Hash passwords, JWT + refresh tokens, API keys, cookies, Entra ID, Identity controllers
