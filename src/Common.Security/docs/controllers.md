# Regira Security — Pre-built Auth Controllers

The ready-made account, password and user controllers in `Regira.Security.Authentication.Web`, and what they require from the host application.

---

## Pre-built Auth Controllers (`Security.Authentication.Web`)

`Security.Authentication.Web` ships three abstract base controllers over ASP.NET Core Identity's `UserManager<TUser>`. Subclass each with a closed user type — `[ApiController]` and the route templates are declared on the base classes and inherited, so the concrete controllers need no attributes of their own:

<!-- no-compile -->
```csharp
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

<!-- no-compile -->
```csharp
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

1. [Index](../README.md) — Overview, projects, and choosing a scheme
1. [Encryption & Hashing](cryptography.md) — Symmetric encryption, PBKDF2 and BCrypt password hashing
1. [JWT Authentication](jwt.md) — Self-issued bearer tokens, claims, and refresh tokens
1. [API Key Authentication](api-keys.md) — Key-based auth for machine callers
1. [External Identity Providers](external-auth.md) — Validating external bearer tokens; OpenID Connect sign-in
1. [Cookie Authentication](cookies.md) — Cookie-backed sessions
1. [Composing Multiple Schemes](schemes.md) — Running several schemes side by side
1. **[Pre-built Auth Controllers](controllers.md)** — Account, password and user endpoints
1. [Practical Examples](examples.md) — Complete implementation examples

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
