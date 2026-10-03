# Licensing.Testing

NUnit tests for [Common.Licensing](../../src/Common.Licensing/README.md): license key generation, parsing and
validation, expiry and grace-period reporting, license status, the free-tier defaults and `LicenseUtility`.

## Running

```bash
dotnet test tests/Licensing.Testing
```

No external requirements; the tests sign with RSA keys they create themselves. The assembly runs serially, because two
fixtures set the process-wide `LicenseValidator.TestPublicKey` and one captures console output.
