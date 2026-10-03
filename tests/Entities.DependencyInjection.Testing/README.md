# Entities.DependencyInjection.Testing

NUnit tests for [Entities.DependencyInjection](../../src/Entities.DependencyInjection/README.md), the `UseEntities()` /
`For<>()` registration API of [Regira Entities](../../src/Common.Entities/README.md). They assert on the services each
`For<>()` overload (one to five type parameters) registers, on attachment, query and paging options, and on license
enforcement: the free tier's 5 simple and 2 complex entity registrations, and how a valid, invalid or expired key
changes that.

## Running

```bash
dotnet test tests/Entities.DependencyInjection.Testing
```

No external requirements. `LicenseEnforcementTests` sets the process-wide `LicenseValidator.TestPublicKey` and is
marked `[NonParallelizable]`.
