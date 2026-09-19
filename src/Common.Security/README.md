# Regira Security

Regira Security provides encryption, password hashing, JWT authentication, and API Key authentication for .NET applications.

## Projects

| Project | Package | Purpose |
|---------|---------|---------|
| `Common.Security` | `Regira.Security` | Symmetric encryption and PBKDF2 hashing |
| `Security.Hashing.BCryptNet` | `Regira.Security.Hashing.BCryptNet` | BCrypt password hashing |
| `Security.Authentication` | `Regira.Security.Authentication` | JWT tokens and API Key auth |
| `Security.Authentication.Web` | `Regira.Security.Authentication.Web` | Pre-built auth controllers |

## Installation

```xml
<!-- Core encryption + hashing -->
<PackageReference Include="Regira.Security" Version="6.*" />

<!-- BCrypt password hashing -->
<PackageReference Include="Regira.Security.Hashing.BCryptNet" Version="6.*" />

<!-- JWT + API Key auth -->
<PackageReference Include="Regira.Security.Authentication" Version="6.*" />

<!-- Pre-built Identity controllers -->
<PackageReference Include="Regira.Security.Authentication.Web" Version="6.*" />
```

---

## Choosing an authentication scheme

| You need | Use | Reference |
|---|---|---|
| Sign in users this application owns | Self-issued JWT + refresh tokens | [JWT Authentication](https://regira.github.io/Regira-Packages/src/Common.Security/docs/jwt.html) |
| Authenticate a machine caller | API key | [API Key Authentication](https://regira.github.io/Regira-Packages/src/Common.Security/docs/api-keys.html) |
| Accept tokens from Entra ID or another IdP | External bearer validation | [External Identity Providers](https://regira.github.io/Regira-Packages/src/Common.Security/docs/external-auth.html) |
| Redirect users to an IdP to sign in | OpenID Connect | [External Identity Providers](https://regira.github.io/Regira-Packages/src/Common.Security/docs/external-auth.html) |
| Server-rendered app or same-site SPA | Cookie sessions | [Cookie Authentication](https://regira.github.io/Regira-Packages/src/Common.Security/docs/cookies.html) |
| More than one of the above in one app | Scheme composition | [Composing Multiple Schemes](https://regira.github.io/Regira-Packages/src/Common.Security/docs/schemes.html) |
| Account / password / user endpoints without writing them | `Regira.Security.Authentication.Web` | [Pre-built Auth Controllers](https://regira.github.io/Regira-Packages/src/Common.Security/docs/controllers.html) |

Encryption and password hashing stand apart from the schemes above and are covered in
[Encryption & Hashing](https://regira.github.io/Regira-Packages/src/Common.Security/docs/cryptography.html). `Regira.Security.Hashing.BCryptNet` is the
recommended password hasher.

---

## Overview

1. **[Index](https://regira.github.io/Regira-Packages/src/Common.Security/)** — Overview, projects, and choosing a scheme
1. [Encryption & Hashing](https://regira.github.io/Regira-Packages/src/Common.Security/docs/cryptography.html) — Symmetric encryption, PBKDF2 and BCrypt password hashing
1. [JWT Authentication](https://regira.github.io/Regira-Packages/src/Common.Security/docs/jwt.html) — Self-issued bearer tokens, claims, and refresh tokens
1. [API Key Authentication](https://regira.github.io/Regira-Packages/src/Common.Security/docs/api-keys.html) — Key-based auth for machine callers
1. [External Identity Providers](https://regira.github.io/Regira-Packages/src/Common.Security/docs/external-auth.html) — Validating external bearer tokens; OpenID Connect sign-in
1. [Cookie Authentication](https://regira.github.io/Regira-Packages/src/Common.Security/docs/cookies.html) — Cookie-backed sessions
1. [Composing Multiple Schemes](https://regira.github.io/Regira-Packages/src/Common.Security/docs/schemes.html) — Running several schemes side by side
1. [Pre-built Auth Controllers](https://regira.github.io/Regira-Packages/src/Common.Security/docs/controllers.html) — Account, password and user endpoints
1. [Practical Examples](https://regira.github.io/Regira-Packages/src/Common.Security/docs/examples.html) — Complete implementation examples

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
