# Regira.Security.Hashing.BCryptNet

BCrypt password hashing for [Regira Security](https://regira.github.io/Regira-Packages/src/Common.Security/), built on [BCrypt.Net-Next](https://www.nuget.org/packages/BCrypt.Net-Next). `Hasher` implements `IHasher` with enhanced BCrypt at the library's default work factor, and is the recommended password hasher over the PBKDF2 `Hasher` in Regira.Security.

## Installation

```xml
<PackageReference Include="Regira.Security.Hashing.BCryptNet" Version="6.*" />
```

## Documentation

- [Encryption & Hashing](https://regira.github.io/Regira-Packages/src/Common.Security/docs/cryptography.html#hashing) — the `IHasher` contract and how the BCrypt, PBKDF2 and simple hashers compare
- [Practical Examples](https://regira.github.io/Regira-Packages/src/Common.Security/docs/examples.html) — hashing and verifying a password

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
