# Regira Security — Cookie Authentication

Cookie-based sessions from `Regira.Security.Authentication`, for a server-rendered app or a same-site SPA that prefers a cookie over a bearer token.

---

## Cookie Authentication

For server-rendered apps, Blazor Server, and same-site SPAs. No extra package — it is in the ASP.NET Core shared
framework.

```csharp
var services = new ServiceCollection();
IConfiguration configuration = new ConfigurationManager();

services.AddCookieAuthentication(o =>
{
    o.IsApi = true;                          // 401/403 instead of a 302 to LoginPath
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// or bind Authentication:Cookie
services.AddCookieAuthentication(configuration);
```

### CookieAuthOptions

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

<!-- no-compile -->
```csharp
await HttpContext.SignInWithClaimsAsync(claims, isPersistent: true);   // normalizes first
await HttpContext.SignOutCookieAsync();
```

Normalization runs at sign-in, so the canonical claim spellings go into the ticket rather than being recomputed
per request.

### Cookie notes

- **`SecurePolicy.Always` means the cookie is never sent over plain HTTP.** Over `http://`, sign-in appears to
  succeed and every later request is anonymous — the cookie is issued, never returned, and the endpoint answers
  `401` as though the credentials were wrong. Serve dev over HTTPS, or use `SameAsRequest` locally only.
- **The cookie is Data Protection-encrypted.** Multi-instance or containerised hosting needs a shared, persisted
  key ring plus `SetApplicationName`, or every restart invalidates every cookie. The symptom is random logouts,
  never an error, and it does not reproduce on one machine.
- **Cookies authenticate ambiently, so state-changing endpoints need antiforgery** — a bearer token does not.
- **Set `IsApi` for anything a script calls**, or the handler `302`s to an HTML login page that `fetch` follows,
  returning `200` and HTML where the caller expected JSON.
- **A cross-site SPA needs `SameSite = None`**, which requires `Secure` (so HTTPS), plus a CORS policy with
  `AllowCredentials`.
- **A cookie is a stale snapshot** — role changes take effect on expiry. For immediate revocation, validate a
  security stamp in `Configure`'s `Events.OnValidatePrincipal`.

---

## Overview

1. [Index](../README.md) — Overview, projects, and choosing a scheme
1. [Encryption & Hashing](cryptography.md) — Symmetric encryption, PBKDF2 and BCrypt password hashing
1. [JWT Authentication](jwt.md) — Self-issued bearer tokens, claims, and refresh tokens
1. [API Key Authentication](api-keys.md) — Key-based auth for machine callers
1. [External Identity Providers](external-auth.md) — Validating external bearer tokens; OpenID Connect sign-in
1. **[Cookie Authentication](cookies.md)** — Cookie-backed sessions
1. [Composing Multiple Schemes](schemes.md) — Running several schemes side by side
1. [Pre-built Auth Controllers](controllers.md) — Account, password and user endpoints
1. [Practical Examples](examples.md) — Complete implementation examples

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
