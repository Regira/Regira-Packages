# Normalizing.Testing

NUnit tests for the normalizing utilities in [Common](../../src/Common/README.md) — `DefaultNormalizer`,
`ObjectNormalizer` with `[Normalized]` properties, `NormalizingUtility`, and custom normalizers over the Contoso
`Person` from [Testing.Library](../Testing.Library/README.md) — and for phone-number formatting in
[Globalization.LibPhoneNumber](../../src/Globalization.LibPhoneNumber/README.md).

## Running

```bash
dotnet test tests/Normalizing.Testing
```

No external requirements.
