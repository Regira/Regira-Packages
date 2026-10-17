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
| `IUserClaimsPrincipalFactory<TUser>` | `AddIdentityCore` | building token claims (all but Identity's security stamp, which no token carries) |
| `IEmailSender` | Regira's `IdentityMailer` (over `Regira.Office.Mail`) or your own | recover / confirm emails |
| `AccountEmailOptions` | `services.Configure<AccountEmailOptions>(…)` | the origins the recover / confirm links may point at, and the reset mail's subject — see [Mailed links](#mailed-links-allowed-origins) |

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
| `POST auth/refresh-token` | the refresh token is the credential; `404` unless refresh tokens are registered |
| `POST auth/password/recover` | forgot-password entry point |
| `POST auth/password/reset` | authorized by the emailed token |
| `POST users/confirm-email` | authorized by the emailed token |

### `AccountControllerBase<TUser>` — route `auth`

| Endpoint | Anon | Request body | Success | Failure |
|----------|:----:|--------------|---------|---------|
| `POST auth?clientApp=…` | ✅ | `{ username, password }` | `200` `{ isAuthenticated: true, token }`, plus `refreshToken` and `expiresAt` when refresh tokens are registered | `401` `{ isLockedOut, lockedOutEnd }`; `401` `{ isNotAllowed: true }` for an unconfirmed address or account (below) |
| `POST auth/validate` | | *(bearer)* | `204` | `401`, or `403` if the token is valid but the user is gone |
| `POST auth/refresh` | | *(bearer, **still valid**)* | `200` `{ isAuthenticated: true, token }` | `401` |
| `POST auth/refresh-token` | ✅ | `{ refreshToken }` | `200` `{ isAuthenticated: true, token, refreshToken, expiresAt }` | `401`; `404` when refresh tokens are not registered |
| `GET auth/personal-data` | | *(bearer)* | `200` `{ given_name, family_name }` | `401` |

`auth/refresh` needs a **still-valid** bearer token, so it cannot renew an expired one — use `auth/refresh-token` for
that. See *Refresh Tokens* above.

`clientApp` becomes the token audience. A successful authenticate resets the user's failed-access count; a failed one increments it and can trigger Identity lockout.

An account the sign-in options refuse answers `401` `{ isNotAllowed: true }` once its password checks: an address not confirmed under `SignIn.RequireConfirmedEmail`, a phone number under `RequireConfirmedPhoneNumber`, or an account the registered `IUserConfirmation<TUser>` does not confirm under `RequireConfirmedAccount`, which the Identity templates set (Identity's default confirmation checks the address). A user you seed, or create through `POST users` without `confirmEmailUrl`, then needs `EmailConfirmed = true`, and `PhoneNumberConfirmed = true` under `RequireConfirmedPhoneNumber`, or it can never sign in.

### `PasswordControllerBase<TUser>` — route `auth/password`

| Endpoint | Anon | Request body | Success | Failure |
|----------|:----:|--------------|---------|---------|
| `POST auth/password` | | `{ currentPassword, newPassword }` | `200` | `400` identity errors / `404` |
| `POST auth/password/recover` | ✅ | `{ username, siteUrl }` | `200`, whether or not the user exists | `400` when `siteUrl` is not on an allowed origin |
| `POST auth/password/reset` | ✅ | `{ token, password }` | `200` | `400` malformed token or identity errors |

`recover` answers the same for an unknown user (it never reveals whether the user exists). A known user is emailed a link: `siteUrl`, the reset page, with its query string set to `?token=…`, percent-encoded so any query parser (`URLSearchParams`, vue-router's `route.query`) reads it back intact; a query `siteUrl` carried is dropped. The token is a Base64 payload of the Identity reset token plus the username. `reset` decodes that payload and returns `400` when it is malformed.

### `UserControllerBase<TUser>` — route `users`

| Endpoint | Anon | Request body | Success | Failure |
|----------|:----:|--------------|---------|---------|
| `POST users` | | `{ username, password, confirmEmailUrl? }` | `200` | `400` identity errors, or `confirmEmailUrl` not on an allowed origin |
| `POST users/confirm-email` | ✅ | `{ token }` | `200` | `400` malformed token or identity errors |

`username` is used as both the user name and the email address. When `confirmEmailUrl` is supplied, the new user is emailed that page with its query string set to `?token=…`, percent-encoded like the reset link's: a Base64 payload of the Identity confirmation token plus the username. The confirm page posts that token back as `{ token }`; `confirm-email` reads the user from it and returns `400` on a malformed token. `userName` is accepted and not read, and `password` is accepted for overrides; the base implementation reads neither. A name that is taken answers `400` with Identity's `DuplicateUserName` error and leaves the existing account alone. An override that assigns a role or tenant does so only for an account this call created. A failed confirmation mail throws after the account exists, so it assigns in a `finally`, guarded by a check that the name was free before the call.

If the confirmation mail fails, `POST users` answers `500` with the user already created, and a repeat answers `400` (`DuplicateUserName`) without mailing. To resend, the app adds an endpoint of its own that mails a fresh link for the existing user (`UserManager.GenerateEmailConfirmationTokenAsync`), checking the page against `AllowedOrigins` with `AccountLinks.FindAllowedPage`, as the base action does.

A `400` comes in one of three bodies: an input that fails model validation gets ASP.NET Core's `ValidationProblemDetails` (errors under `errors`); identity errors and a page on another origin get the field errors as the body itself, an identity error keyed by its code (`{ "PasswordTooShort": ["..."] }`); a malformed token gets a `ProblemDetails` without field errors. A client reads `errors` when present, else the body.

### Mailed links: allowed origins

`recover`, and `POST users` with a `confirmEmailUrl`, mail a link to the page the request names, with `?token=…` as
its query string. Whoever controls that page receives the token when the link is followed, so the controllers mail a
link only to a page on an origin the application lists in `AccountEmailOptions`:

```csharp
using Regira.Security.Authentication.Web.Models;

services.AddOptions<AccountEmailOptions>()
    .Bind(configuration.GetSection("Authentication:AccountEmails"))
    .Validate(o => o.AllowedOrigins.Count > 0, "Authentication:AccountEmails:AllowedOrigins lists no origin.")
    .ValidateOnStart();
```

```json
{
  "Authentication": {
    "AccountEmails": {
      "SiteName": "Example",
      "AllowedOrigins": [ "https://app.example.com" ]
    }
  }
}
```

| Option | Default | Purpose |
|--------|---------|---------|
| `AllowedOrigins` | empty | the origins (scheme, host and port) the reset and confirmation pages may be on |
| `SiteName` | `null` | starts the reset mail's subject: "Example Password Reset" |

- A `siteUrl` or `confirmEmailUrl` on any other origin — another host, scheme or port, or a URL carrying credentials — answers `400` and sends nothing, and `POST users` then creates no user. `recover` checks the page before it looks the user up, so its answer still reveals nothing about accounts.
- With no origin listed, `recover` and a `POST users` with `confirmEmailUrl` throw `InvalidOperationException`, a 500. The `ValidateOnStart()` above moves that failure to startup; an entry that is not an http(s) URL is reported, by name, when a link is first needed. A `POST users` without `confirmEmailUrl` needs no configuration.
- The request's `siteName` is ignored, so a caller cannot choose what the subject says.
- The check runs in the base actions. An override of `RecoverPassword` or `Create` that mails a link of its own instead of calling the base must check the page itself, with `AccountLinks.FindAllowedPage`. So must a minimal-API host, which gets no endpoint mapper: it writes the account endpoints itself, reusing the request and token models in `Regira.Security.Authentication.Web.Models`. `FindAllowedPage(url)` returns the page only when it is on an allowed origin, and `WithToken(token)` adds the percent-encoded token, the Base64 of the UTF-8 JSON of a `UserTokenModel` (`System.Text.Json` defaults, so PascalCase keys). To change only a mail's wording or layout, wrap the `IEmailSender`: it receives the subject and the body, link included.
- A development front-end's origin, such as `http://localhost:5173`, belongs in `appsettings.Development.json` only.

### OpenAPI document transformers (`Security.Authentication.Web`)

`Regira.Security.Authentication.Web.OpenApi.Transformers` describes the API's authentication in the generated
OpenAPI document (.NET 10). Two transformers are enough whatever the scheme count — the first declares the schemes,
which is what makes the Swagger/Scalar authentication prompt appear; the second records **which** operations need
one, without which a generated client cannot tell a public endpoint from a guarded one.

| Transformer | Registration | Emits |
|---|---|---|
| `AuthenticationSchemeDocumentTransformer` | `AddDocumentTransformer<…>()` | `components.securitySchemes` for **every** registered scheme, from the descriptor each contributes at registration |
| `SecurityRequirementOperationTransformer` | `AddOperationTransformer<…>()` | a per-operation `security` requirement, resolving a forwarding policy scheme to the schemes behind it |

Cookie has no dedicated OpenAPI security type; it is emitted as an API key with `in: cookie`, the accepted
convention. Adding a scheme needs no transformer change.

<!-- no-compile -->
```csharp
using Regira.Security.Authentication.Web.OpenApi.Transformers;   // package Regira.Security.Authentication.Web

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<AuthenticationSchemeDocumentTransformer>();
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
- The authenticate failure response reads the same for an unknown user name as for a wrong password (`isLockedOut: false`). Two things still tell a name exists, trade-offs to weigh against username enumeration: a locked-out account answers `isLockedOut: true`, and an unknown name skips the password hash, so it answers faster. Rate-limit `auth` by client.
- **List only your own front-end origins** in `AccountEmailOptions.AllowedOrigins`: a mailed link hands its token to the page it opens.
- **Rate-limit the anonymous endpoints that cost something:** `auth/refresh-token` (its token is the credential) and `auth/password/recover` (it mails the named account on every call). Limit by client, not by account, so a `429` reveals no more than the `200` does.
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
