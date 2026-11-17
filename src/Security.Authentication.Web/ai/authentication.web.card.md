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
  `AccountControllerBase<AppUser>(ITokenHelper, UserManager<AppUser>, IUserClaimsPrincipalFactory<AppUser>, ILogger? logger = null)`;
  `PasswordControllerBase<AppUser>(UserManager<AppUser>)`; `UserControllerBase<AppUser>(UserManager<AppUser>)`.
  **No `ISerializer`** — payloads use `System.Text.Json` internally.
- **Minimal APIs:** no endpoint mapper. `AccountSignInService<TUser>` (`…Web.Services`, register it scoped) holds
  what `AccountControllerBase` decides: `CanSignIn`, `CreateSuccessResponse`, `TokenClaims`, `CreateFailedResponse`.
- **Required services:** `AddIdentityCore<AppUser>().AddEntityFrameworkStores<TCtx>().AddSignInManager().AddDefaultTokenProviders()`,
  `AddJwtAuthentication(…)` (for `ITokenHelper`), and an `IEmailSender` (recover/confirm email).
  **`IEmailSender` is `Microsoft.AspNetCore.Identity.UI.Services.IEmailSender`** (a Microsoft type, not
  Regira — it ships in the ASP.NET Core shared framework, so there is no package to add); implement it or use this package's `IdentityMailer`
  (`Regira.Security.Authentication.Web.Mail`), which sends through a registered Regira `IMailService` —
  `Regira.Office` → `office.mail.instructions` → *ASP.NET Identity Integration*.
- **Mailed links go only to allowed origins:** bind `AccountEmailOptions`
  (`Regira.Security.Authentication.Web.Models`) from `Authentication:AccountEmails`, validated on start, with
  `AllowedOrigins` = the SPA's origin and the app's `SiteName`. A `siteUrl` / `confirmEmailUrl` on another origin
  answers `400`; **with none listed, `recover` throws** (500). An override, or a minimal-API endpoint, that mails a link of
  its own checks the page with `AccountLinks.FindAllowedPage` — `security.instructions` → *Pre-built Auth
  Controllers* (*An endpoint of your own that mails a token link*).
- **Version floor:** floors `Microsoft.OpenApi` at **2.13.0** and `Microsoft.AspNetCore.OpenApi` at
  **10.0.13** on net10 — a direct pin of either below the resolved version fails restore with NU1605, and
  nothing restores or builds until it is cleared. Leave `Microsoft.OpenApi` **transitive**:
  `Microsoft.AspNetCore.OpenApi` brings the 2.x its source generator is compatible with (3.x breaks it).
  ⚠️ `dotnet new webapi` pins `Microsoft.AspNetCore.OpenApi` at its SDK's patch (`10.0.11` from SDK
  10.0.400) — raise it **before** referencing this package, or afterwards with
  `dotnet add package Microsoft.AspNetCore.OpenApi --version "10.*"` (the TFM's major), which lands even while the downgrade is live. Then pin the `10.*` it writes to the patch
  restore resolved (`dotnet list package`).
- **`clientApp` = JWT audience.** Login is `POST auth?clientApp=…` (required query); set the API's
  `Authentication:Jwt:Audience` to the SPA's `clientApp`, or sign-in answers `400`: a `clientApp` that is not a
  configured audience is refused before the user is looked up. A sign-in action of your own checks it with
  `IsAllowedAudience(clientApp)` (also on `AccountSignInService<TUser>`).
- **Guarded only if you enforce it:** the bases carry no `[Authorize]` — `MapControllers().RequireAuthorization()`.
  Without it `POST users` creates an account for any caller; the signed-in routes answer `401` themselves.
  `[AllowAnonymous]`: `POST auth`, **`POST auth/refresh-token`**, `auth/password/recover`, `auth/password/reset`,
  `users/confirm-email`.
- **`POST auth/password` guards the current password like a sign-in:** a wrong one counts toward lockout, and a
  locked-out account answers `400` with the error `UserLockedOut`. `PasswordControllerBase` has the same
  `IsBlocked` / `OnPasswordRejected` virtuals as `AccountControllerBase`. A change or reset revokes refresh tokens;
  issued access tokens stay valid until they expire.
- **Input limits:** user name 256, password 1024, URL 2048, token 4096 characters (`400`); a body over 16 KB
  answers `413`.
- **⚠️ `auth/refresh` ≠ `auth/refresh-token`.** `auth/refresh` needs a **still-valid** bearer token, so it cannot
  renew an expired one — it is for picking up role changes mid-session. `auth/refresh-token` is anonymous (the
  refresh token **is** the credential, so **rate-limit it**) and answers `404` unless `.AddRefreshTokens()` was
  called. With refresh tokens registered, `POST auth` also returns `refreshToken` + `expiresAt`; without them the
  response body is unchanged.
