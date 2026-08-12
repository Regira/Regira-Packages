# Regira.Security — index card

> The security **hub**: this package ships symmetric encryption + PBKDF2 hashing, and its
> `security.instructions` guide is the full wiring reference for the whole area — JWT (+ refresh tokens),
> API keys, cookies, external bearer (Entra ID / OIDC), interactive sign-in, scheme selection, and the
> pre-built controllers (`Regira.Security.Authentication[.Web]` redirect here for their recipes).

- **In this package:** `Encrypter` (symmetric encryption) and `PasswordHasher` (PBKDF2); BCrypt lives in
  `Regira.Security.Hashing.BCryptNet`. Everything token/scheme-shaped lives in
  `Regira.Security.Authentication` — but is documented here.
- **JWT wiring one-liner:** `AddJwtAuthentication(o => configuration.GetSection(AuthenticationSections.Jwt).Bind(o))` —
  `Secret` **≥ 64 bytes** (HS512 default, counted as ASCII) or startup throws; `Algorithm` is a JWA id
  (`"HS512"`), leave unset for the default.
- **`Audience` = the SPA's `clientApp`.** Login is `POST auth?clientApp=…`; a mismatch 401s every
  authenticated call with `audience invalid`.
- **Roles end-to-end** (Identity → JWT → SPA) is a dedicated recipe: `security.instructions` →
  *Roles end-to-end* (`how_to` key `roles-end-to-end`). The two load-bearing lines: `.AddRoles<IdentityRole>()`
  and `o.ClaimsIdentity.RoleClaimType = RegiraClaimTypes.Role` — without them, Identity emits no role claims /
  `[Authorize(Roles=…)]` 403s every Identity-issued token.
- **Role claims have three spellings** — `role` (self-issued JWT), `roles` (Entra), `ClaimTypes.Role` URI
  (Identity default, API keys). Read with `principal.FindRoles()` (all three); `RequireClaim("role", …)` or a
  single-spelling check silently excludes the other issuers.
- **Claim normalization:** the external-bearer/OIDC/cookie paths run `ClaimsNormalizer` (adds canonical
  `sub`/`name`/`email`/`role` without removing the provider's); the **local `AddJwtAuthentication` scheme does
  not** — it validates the spellings the token carries.
- **Scheme choice:** self-issued tokens → `AddJwtAuthentication` (registers `ITokenHelper`); someone else's
  tokens → `AddBearerAuthentication`/`AddEntraIdBearer` (validate only, no `ITokenHelper`); 2+ schemes →
  `.AddSchemeSelector()` **last**.
- **Refresh tokens are opt-in** (`.AddRefreshTokens()`); ⚠️ `auth/refresh` (still-valid bearer, picks up role
  changes) ≠ `auth/refresh-token` (anonymous, the refresh token **is** the credential — rate-limit it). The
  in-memory store is dev-only.
- **Top gotchas:** Entra app roles arrive as `roles` and key on `oid` not `sub`; cookie `SecurePolicy`
  defaults to `Always` (plain HTTP → sign-in "works", every request 401s); pre-built controllers are guarded
  only via `MapControllers().RequireAuthorization()`.
