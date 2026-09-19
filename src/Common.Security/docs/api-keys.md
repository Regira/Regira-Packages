# Regira Security — API Key Authentication

API key authentication from `Regira.Security.Authentication` — for machine callers that cannot perform an interactive sign-in.

---

## API Key Authentication

### ApiKeyAuthenticationOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ApiKeyHeaderName` | `string` | `"X-Api-Key"` | Request header name |
| `AuthenticationType` | `string` | `"ApiKey"` | Authentication type string |

### IApiKeyOwnerService

<!-- no-compile -->
```csharp
Task<ApiKeyOwner?> FindByOwner(string id);
Task<ApiKeyOwner?> FindByKey(string apiKey);
Task<bool>         Validate(string id, string apiKey);
```

### ApiKeyOwner model

| Property | Type | Description |
|----------|------|-------------|
| `OwnerId` | `string` | Owner identifier |
| `Key` | `string` | API key value |
| `Roles` | `ICollection<string>` | Roles assigned to this key |
| `Claims` | `ICollection<ApiKeyOwner.Claim>` | Extra claims (`Type` / `Value` pairs) added to the principal |

### DI registration

```csharp
var services = new ServiceCollection();
IConfiguration configuration = new ConfigurationManager();

// In-memory keys from code
services.AddApiKeyAuthentication()
        .AddInMemoryApiKeyAuthentication(new[]
        {
            new ApiKeyOwner { OwnerId = "client-a", Key = "key-abc", Roles = ["read"] }
        });

// From appsettings.json
var keys = configuration.GetSection(AuthenticationSections.ApiKeys).ToApiKeyOwners();
services.AddApiKeyAuthentication()
        .AddInMemoryApiKeyAuthentication(keys);
```

`appsettings.json` shape — an **array**, each entry carrying its own `OwnerId`. `ToApiKeyOwners()` requires both
fields, so an object keyed by owner name throws `InvalidOperationException` at startup:
```json
"ApiKeys": [
  { "OwnerId": "client-a", "Key": "key-abc", "Roles": ["read", "write"] }
]
```

---

## Overview

1. [Index](../README.md) — Overview, projects, and choosing a scheme
1. [Encryption & Hashing](cryptography.md) — Symmetric encryption, PBKDF2 and BCrypt password hashing
1. [JWT Authentication](jwt.md) — Self-issued bearer tokens, claims, and refresh tokens
1. **[API Key Authentication](api-keys.md)** — Key-based auth for machine callers
1. [External Identity Providers](external-auth.md) — Validating external bearer tokens; OpenID Connect sign-in
1. [Cookie Authentication](cookies.md) — Cookie-backed sessions
1. [Composing Multiple Schemes](schemes.md) — Running several schemes side by side
1. [Pre-built Auth Controllers](controllers.md) — Account, password and user endpoints
1. [Practical Examples](examples.md) — Complete implementation examples

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
