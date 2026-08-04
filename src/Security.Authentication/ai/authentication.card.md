# Regira.Security.Authentication — index card

> JWT + API-Key primitives. The **full guide is `Regira.Security` `security.instructions`** — read it for the
> option tables and DI (`get_package id="Regira.Security" section="security.instructions"`). The pre-built
> controllers that consume these primitives are in `Regira.Security.Authentication.Web`.

- **`AddJwtAuthentication(o => …)`** registers `ITokenHelper` + the JwtBearer scheme. Set `Secret`,
  `Authority`, `Audience`. **`Algorithm` is a JWA id (`HS512`)** and defaults to `HS512` when unset.
- **`Secret` must be ≥ 64 bytes for the HS512 default** (≥ 48 for HS384, ≥ 32 for HS256). A shorter key
  throws `InvalidOperationException` from `AddJwtAuthentication`, naming the byte count it got and the one it
  needs. `ValidateSecretLength = false` opts out — only correct for a scheme that never issues tokens.
- **`ITokenHelper.Create(claims, audience?, lifeSpan?)`** / `Validate(token)` —
  `Regira.Security.Authentication.Jwt.Abstraction`.
- **`ClaimsPrincipal` extensions** `FindUserId()` / `FindUserName()` / `FindEmail()` / `FindRoles()` /
  `HasScope()` — `Regira.Security.Authentication.Jwt.Extensions` (namespace is historical; they apply to **every**
  scheme). `FindRoles()` covers all three role spellings; `HasScope()` splits the space-delimited `scp`/`scope`
  value, which a plain `HasClaim` does not.
- **`ClaimsNormalizer.Normalize(claims, authType)`** (`…Core.Services`) adds the canonical `sub`/`name`/`email`/
  `role` spellings (`RegiraClaimTypes`) **without removing** the provider's — so `[Authorize(Roles=…)]`,
  `IsInRole` and `RequireClaim("role", …)` all agree. On an Entra token key rows on **`oid`, not `sub`** (`sub` is
  pairwise per application).
- **API keys:** `AddApiKeyAuthentication().AddInMemoryApiKeyAuthentication(keys)`; `IApiKeyOwnerService`,
  `ApiKeyOwner`, `ApiKeyAuthenticationOptions` under `Regira.Security.Authentication.ApiKey.*`.
- **Refresh tokens:** `.AddRefreshTokens()` chains off `AddJwtAuthentication` — **opt-in**, and without it nothing
  changes (`auth/refresh-token` 404s and `POST auth` keeps its exact body). Rotating, replay-detecting, stored hashed.
  ⚠️ `auth/refresh` (pre-existing) needs a **still-valid** bearer so it cannot renew an expired token — that is what
  `auth/refresh-token` is for. ⚠️ The default `InMemoryRefreshTokenStore` is **dev only**: it loses sessions on restart
  and is per-process, so behind a load balancer users are signed out at random. Implement `IRefreshTokenStore` and
  register it with `.AddRefreshTokenStore<T>()` first. `Refresh` requires a claims resolver so claims are re-read —
  never replayed. Call `RevokeAllForUser` on password change / disable; nothing does it automatically.
- **Someone else's tokens (Entra ID, Auth0, Keycloak, Okta):** `AddBearerAuthentication(o => { o.Authority = …;
  o.Audience = …; })`, or `AddEntraIdBearer(o => { o.TenantId = …; o.ClientId = …; })`.
  **`AddJwtAuthentication` cannot do this** — it requires a `Secret` and derives a symmetric key, while an external
  authority signs asymmetrically. These register **no `ITokenHelper`**; they validate, they do not issue.
- **Entra traps:** app roles arrive as **`roles`** (plural — `role` matches nothing, so `[Authorize(Roles=…)]`
  403s a token that visibly has the role); **`oid`, not `sub`**, is the stable user id (`sub` is pairwise per
  application); a registration on `accessTokenAcceptedVersion: null` issues **v1** tokens from
  `sts.windows.net` → `IDX10205`; `groups` is GUIDs and is dropped entirely past the token-size limit.
  `organizations`/`common` is multi-tenant — the issuer is checked against the token's own `tid`, and *any*
  tenant can then sign in, so authorize beyond that yourself.
- **Interactive sign-in:** `AddEntraIdSignIn(o => { o.TenantId = …; o.ClientId = …; o.ClientSecret = …; })` or
  `AddOidcAuthentication(…)` (`…OpenIdConnect.Extensions`). Registers the **cookie + OIDC pair** and wires the
  defaults: cookie authenticates, OIDC challenges. Traps — **sign-out needs both** schemes or the next challenge
  silently re-signs the user in; behind a reverse proxy you need `UseForwardedHeaders` before the auth middleware or
  the callback fails with "Correlation failed"; `Scopes` **replaces** the handler's defaults; `SaveTokens` enlarges
  the cookie.
- **No `Microsoft.Identity.Web`:** the presets protect an API and sign users in. They do **not** do on-behalf-of,
  Graph calls as the user, the MSAL token cache, incremental consent, or B2C user flows — take that package
  directly for those.
- **Cookies:** `AddCookieAuthentication(o => o.IsApi = true)` (`…Cookie.Extensions`); sign in with
  `HttpContext.SignInWithClaimsAsync(claims)` / `SignOutCookieAsync()`, which normalize into the ticket.
  **Set `IsApi` for anything a script calls** or the handler 302s to an HTML login page that `fetch` follows.
- **Cookie traps:** `SecurePolicy` defaults to `Always`, so over plain HTTP the cookie is issued and never sent
  back — sign-in looks fine and every later request 401s. And a multi-instance host needs a shared, persisted
  Data Protection key ring plus `SetApplicationName`, or restarts log everyone out at random.
- **`IRefreshTokenStore.TryRevoke` must be atomic** — a conditional write (`WHERE RevokedAt IS NULL` + rowcount),
  never read-then-write. Two concurrent refreshes of one token would otherwise both succeed and split the family
  into two live chains with no replay detected.
- **⚠️ `AddSchemeSelector` + interactive sign-in:** the forwarding rules key on the credential a request *carries*,
  and a browser hitting a guarded page carries none — so a challenge would resolve to a bearer `401` and login would
  be unreachable. A registered OIDC scheme therefore answers challenges automatically; set `ChallengeScheme`
  explicitly in a host serving both browsers and API clients. Calling `AddSchemeSelector` twice throws.
- **More than one scheme:** `.AddSchemeSelector()` (`…Core.Extensions`) — **call it last**. It forwards each
  request to the scheme matching its credential and takes over the default scheme, so registration order stops
  deciding what an unattributed `[Authorize]` authenticates against. No `AddAuthorization` default policy needed.
- **OpenAPI is two transformers, whatever the scheme count:** `AuthenticationSchemeDocumentTransformer` declares
  every registered scheme (each `Add…Authentication` contributes an `ISecuritySchemeDescriptor`), and
  `SecurityRequirementOperationTransformer` marks guarded operations — the latter also resolves the selector's
  policy scheme to the real schemes, without which every requirement would name a scheme the document never
  declares. Cookie is emitted as an API key with `in: cookie` (OpenAPI has no cookie type).
