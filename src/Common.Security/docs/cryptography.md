# Regira Security — Encryption & Hashing

Symmetric encryption and password hashing from `Regira.Security`, plus the BCrypt hasher in `Regira.Security.Hashing.BCryptNet`. Both families are interface-first, so a consumer depends on `IEncrypter` / `IHasher` and picks the implementation at registration.

---

## Encryption

Two `IEncrypter` implementations:

```csharp
public interface IEncrypter
{
    string Encrypt(string plainText, string? key = null);
    string Decrypt(string encryptedText, string? key = null);
}
```

### SymmetricEncrypter

AES-256 with a static key derived from `CryptoOptions.Secret`. Fast; same key always produces the same ciphertext.

```csharp
var enc = new SymmetricEncrypter(new CryptoOptions { Secret = "my-app-key" });
string cipher = enc.Encrypt("sensitive value");
string plain  = enc.Decrypt(cipher);
```

### AesEncrypter

AES with a random salt prepended per encryption. Slower but produces different ciphertext on each call — recommended for stored secrets.

```csharp
var enc = new AesEncrypter(new CryptoOptions { Secret = "my-app-key" });
string cipher = enc.Encrypt("sensitive value");
```

### CryptoOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Secret` | `string?` | built-in salt key | Signing / derivation secret |
| `AlgorithmType` | `string?` | `"SHA512"` | Hash algorithm — read only by `SymmetricEncrypter`, `SimpleHasher`, and the BCrypt hasher; `AesEncrypter` and the PBKDF2 `Hasher` hard-wire SHA-512 |
| `Iterations` | `int?` | `500000` | PBKDF2 iteration count used by the `Hasher` |
| `Encoding` | `Encoding?` | UTF-8 | Text encoding |

---

## Hashing

Two `IHasher` implementations:

```csharp
public interface IHasher
{
    string Hash(string plainText);
    bool   Verify(string plainText, string hashedValue);
}
```

### Hasher (PBKDF2)

Stores a per-hash random salt + PBKDF2 digest (500 000 iterations by default — configurable via `CryptoOptions.Iterations` — SHA-512, 64-byte output). Constant-time verification.

```csharp
var hasher = new Regira.Security.Hashing.Hasher();
string stored = hasher.Hash("myPassword123");
bool ok       = hasher.Verify("myPassword123", stored);   // true
```

### Security.Hashing.BCryptNet — BCrypt Hasher

Enhanced BCrypt (SHA-384 by default), using the BCrypt.Net default work factor. Recommended for passwords.

```csharp
var hasher = new Regira.Security.Hashing.BCryptNet.Hasher();
string stored = hasher.Hash("myPassword123");
bool ok       = hasher.Verify("myPassword123", stored);
```

### SimpleHasher

Double-SHA with salt — fast but weaker. Use for non-password data only.

---

## Overview

1. [Index](../README.md) — Overview, projects, and choosing a scheme
1. **[Encryption & Hashing](cryptography.md)** — Symmetric encryption, PBKDF2 and BCrypt password hashing
1. [JWT Authentication](jwt.md) — Self-issued bearer tokens, claims, and refresh tokens
1. [API Key Authentication](api-keys.md) — Key-based auth for machine callers
1. [External Identity Providers](external-auth.md) — Validating external bearer tokens; OpenID Connect sign-in
1. [Cookie Authentication](cookies.md) — Cookie-backed sessions
1. [Composing Multiple Schemes](schemes.md) — Running several schemes side by side
1. [Pre-built Auth Controllers](controllers.md) — Account, password and user endpoints
1. [Practical Examples](examples.md) — Complete implementation examples

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
