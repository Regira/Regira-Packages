# Regira Security — Composing Multiple Schemes

Running more than one authentication scheme in one application, and choosing which one a given endpoint accepts.

---

## Composing multiple schemes

`AddSchemeSelector()` registers a policy scheme that forwards each request to the scheme matching the credential
it carries, and makes itself the default authenticate and challenge scheme.

```csharp
var services = new ServiceCollection();
IConfiguration configuration = new ConfigurationManager();

services.AddApiKeyAuthentication()
        .AddInMemoryApiKeyAuthentication(configuration.GetSection("Authentication:ApiKeys").ToApiKeyOwners());

services.AddJwtAuthentication(o => configuration.GetSection("Authentication:Jwt").Bind(o))
        .AddSchemeSelector();     // last
```

Call it **last**. Every `Add…Authentication` sets its own default scheme, so without it the registration order
decides which handler an unattributed `[Authorize]` uses — and the symptom is a 401 for a caller holding a
perfectly good credential of the other kind. Rules naming an unregistered scheme are skipped, so the order stops
mattering. No `AddAuthorization` default policy is needed to name the schemes.

Built-in rules, first match wins: `Authorization: Bearer …` → the bearer scheme; a non-empty API-key header →
the API-key scheme; the default-named authentication cookie → the cookie scheme (`SchemeForwardRules.Cookie()`,
skipped when that scheme is not registered). A blank API-key header deliberately does not match, so it cannot
capture a request that the bearer scheme could have served.

| `SchemeSelectorOptions` | Type | Default | Description |
|---|---|---|---|
| `AuthenticationScheme` | `string` | `"Smart"` | Name of the policy scheme |
| `DisplayName` | `string` | *(descriptive)* | Display name of the policy scheme |
| `FallbackScheme` | `string?` | `null` | Scheme for a request with no recognised credential; defaults to the lowest-ordered registered rule |
| `ChallengeScheme` | `string?` | `null` | Scheme that answers a challenge; when unset, a registered sign-in scheme (see below) or the forwarding rules decide |
| `ForwardChallengeToSignInScheme` | `bool` | `true` | Whether a registered interactive sign-in scheme answers challenges when `ChallengeScheme` is unset |
| `Rules` | `IList<SchemeForwardRule>` | `[]` | Extra rules, added before the built-in ones |
| `UseDefaultRules` | `bool` | `true` | Whether to include the bearer, API-key, and cookie rules |

Build rules with `SchemeForwardRules.Bearer(scheme)`, `.Basic(scheme)`, `.ApiKey(scheme)`,
`.Cookie(scheme, cookieName)`, or `new SchemeForwardRule(order, scheme, context => …)`.

Because the policy scheme authenticates nothing, no document transformer declares it — register the operation
transformer so guarded operations name the real schemes instead, plus a document transformer per scheme.

---

## Overview

1. [Index](../README.md) — Overview, projects, and choosing a scheme
1. [Encryption & Hashing](cryptography.md) — Symmetric encryption, PBKDF2 and BCrypt password hashing
1. [JWT Authentication](jwt.md) — Self-issued bearer tokens, claims, and refresh tokens
1. [API Key Authentication](api-keys.md) — Key-based auth for machine callers
1. [External Identity Providers](external-auth.md) — Validating external bearer tokens; OpenID Connect sign-in
1. [Cookie Authentication](cookies.md) — Cookie-backed sessions
1. **[Composing Multiple Schemes](schemes.md)** — Running several schemes side by side
1. [Pre-built Auth Controllers](controllers.md) — Account, password and user endpoints
1. [Practical Examples](examples.md) — Complete implementation examples

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
