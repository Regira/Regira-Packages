# Regira.Security.Authentication.Web — index card

> Pre-built account/password/user controllers over ASP.NET Core Identity, issuing JWTs and — when refresh tokens
> are registered — token pairs. The **full wiring recipe, option tables, and endpoint tables live in the
> `Regira.Security` `security.instructions` guide** — read it
> (`get_package id="Regira.Security" section="security.instructions"` → _Add JWT authentication_). This
> package ships only the controllers you subclass, plus the OpenAPI security transformers.

- **Three generic base controllers** (`Regira.Security.Authentication.Web.Controllers`):
  `AccountControllerBase<TUser>` (route `auth`), `PasswordControllerBase<TUser>` (`auth/password`),
  `UserControllerBase<TUser>` (`users`), with `TUser : IdentityUser<string>`. `[ApiController]` + the routes
  are on the bases — **do not repeat `[ApiController]`/`[Route]` on the subclass**.
- **Subclass with a forwarding ctor** (the bases' ctors are protected):
  `AccountControllerBase<AppUser>(ITokenHelper, UserManager<AppUser>, IUserClaimsPrincipalFactory<AppUser>, ILogger)`;
  `PasswordControllerBase<AppUser>(UserManager<AppUser>)`; `UserControllerBase<AppUser>(UserManager<AppUser>)`.
  **No `ISerializer`** — payloads use `System.Text.Json` internally.
- **Required services:** `AddIdentityCore<AppUser>().AddEntityFrameworkStores<TCtx>().AddSignInManager().AddDefaultTokenProviders()`,
  `AddJwtAuthentication(…)` (for `ITokenHelper`), and an `IEmailSender` (recover/confirm email).
  **`IEmailSender` is `Microsoft.AspNetCore.Identity.UI.Services.IEmailSender`** (a Microsoft type, not
  Regira — package `Microsoft.AspNetCore.Identity.UI`); implement it or use Regira's `IdentityMailer`.
- **Version floor:** this package references `Microsoft.AspNetCore.OpenApi` — pinning it (or
  `Microsoft.OpenApi`) below the floor fails restore with NU1605; resolve to the latest stable patch.
- **`clientApp` = JWT audience.** Login is `POST auth?clientApp=…` (required query); set the API's
  `Authentication:Jwt:Audience` to the SPA's `clientApp` or authenticated calls 401 (`audience invalid`).
- **Guarded only if you enforce it:** the bases carry no `[Authorize]` — `MapControllers().RequireAuthorization()`.
  `[AllowAnonymous]`: `POST auth`, **`POST auth/refresh-token`**, `auth/password/recover`, `auth/password/reset`,
  `users/confirm-email`.
- **⚠️ `auth/refresh` ≠ `auth/refresh-token`.** `auth/refresh` needs a **still-valid** bearer token, so it cannot
  renew an expired one — it is for picking up role changes mid-session. `auth/refresh-token` is anonymous (the
  refresh token **is** the credential, so **rate-limit it**) and answers `404` unless `.AddRefreshTokens()` was
  called. With refresh tokens registered, `POST auth` also returns `refreshToken` + `expiresAt`; without them the
  response body is unchanged.
