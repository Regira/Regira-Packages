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

Obsolete. AES-256 with a key and vector derived from `CryptoOptions.Secret` alone, so a value always encrypts to the
same ciphertext. Keep it only to decrypt what it encrypted; encrypt with `AesEncrypter`. Without a secret it uses the
built-in key, which is published with the source.

<!-- no-compile -->
```csharp
var legacy = new SymmetricEncrypter(new CryptoOptions { Secret = "my-app-key" });
string plain = legacy.Decrypt(storedCipher);
```

### AesEncrypter

AES-256 with a random salt per value, so it produces different ciphertext on each call — recommended for stored secrets.
The costly step, PBKDF2 over the secret (about 0.2 s), runs once per secret in a process and is cached; each value then
gets a key of its own from its salt with HKDF, in microseconds, so decrypting in an EF value converter stays cheap. The
text is `aes1:` and Base64, 64 bytes longer than the cipher: 32 of salt and a 32-byte HMAC-SHA256 tag, so a value that
was changed, or encrypted with another secret, throws `CryptographicException`. A value without that prefix, written before 6.6.0,
still decrypts, paying the derivation on its first read in a process; decrypt and encrypt it again to move it. A version
before 6.6.0 cannot read the `aes1:` format.
Encrypting needs a secret, `CryptoOptions.Secret` or a key passed to `Encrypt`, and throws `InvalidOperationException`
without one. `Decrypt` without one uses the built-in key, so what an earlier version encrypted with it stays readable.

```csharp
var enc = new AesEncrypter(new CryptoOptions { Secret = "my-app-key" });
string cipher = enc.Encrypt("sensitive value");
```

A value converter still decrypts the column whenever the entity loads, even into a DTO that drops it, and a read DTO
that carries it sends the plain value to every client. For a value few screens show (a bank account), store the
ciphertext in a plain property encrypted by a prepper from a write-only input, keep it out of the read DTOs (a masked
copy can serve the lists), and decrypt it in an endpoint of its own that returns one value.

### CryptoOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Secret` | `string?` | `null` | The key `AesEncrypter` derives from; it refuses to encrypt without one. Without it, decrypting and the obsolete `SymmetricEncrypter` and `SimpleHasher` use a built-in key, published with the source |
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

Obsolete: a double SHA with a fixed salt, compared in variable time. Hash with `Hasher` (PBKDF2), or the BCrypt
`Hasher` for passwords.

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
