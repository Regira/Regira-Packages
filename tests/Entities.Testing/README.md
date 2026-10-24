# Entities.Testing

NUnit tests for [Regira Entities](../../src/Common.Entities/README.md), wired through
[Entities.DependencyInjection](../../src/Entities.DependencyInjection/README.md) and
[Entities.Validation.FluentValidation](../../src/Entities.Validation.FluentValidation/README.md): entity services and
query building, global filters, sorting and keyword search, archiving and soft delete, attachments, primers, preppers,
processors, normalizers, validators, reactors, concurrency tokens, startup validation and UTC date handling — over the
Contoso model from [Testing.Library](../Testing.Library/README.md). One fixture reads the JSON options of
[Entities.Web](../../src/Entities.Web/README.md) beside `AddProblemDetails()`, here because this project runs on net8.0 too.
With it referenced, `UseEntities()` here also registers Entities.Web's startup checks; they find nothing without
controllers or mapped endpoints.

## Running

```bash
dotnet test tests/Entities.Testing
```

The project targets `net8.0` (EF Core 9) and `net10.0` (EF Core 10), so a full run needs both runtimes; add
`--framework net10.0` to run one. Nothing else is needed: each fixture opens its own in-memory SQLite database (one
uses the EF Core in-memory provider), and a few tests write files under the temp directory.
