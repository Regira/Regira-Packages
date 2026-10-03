# Regira.Entities.EFcore

The Entity Framework Core implementation behind [Regira Entities](https://regira.github.io/Regira-Packages/src/Common.Entities/), built on [Microsoft.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore) through [Regira.DAL.EFcore](https://regira.github.io/Regira-Packages/src/DAL.EFcore/). `EntityRepository` is the default `IEntityService`, reading and writing through the application's `DbContext` with `EntityReadService` and `EntityWriteService`. The package also holds the built-in query builders, preppers and primers, the SaveChanges interceptors that run primers and reactors, and the soft-delete and concurrency-token conventions.

## Installation

```xml
<PackageReference Include="Regira.Entities.EFcore" Version="6.*" />
```

Regira.Entities.DependencyInjection and Regira.Entities.Web depend on this package, so an application usually receives it transitively. It brings EF Core but no database provider: add one (for example `Microsoft.EntityFrameworkCore.Sqlite`) to the application.

## Documentation

- [Services](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/services.html) — the repository, its read and write operations, and the helper services in its pipeline
- [Built-in Features](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/built-in-features.html) — the ready-made query builders, preppers and primers, and the `DbContext` conventions
- [Regira Entities](https://regira.github.io/Regira-Packages/src/Common.Entities/) — the entity framework this package implements

## License

Regira Commercial License, with a free tier that applies automatically — no key is needed within its limits. A license key removes the limits and is validated fully offline. See the [license text](https://github.com/Regira/Regira-Packages/blob/main/legal/REGIRA-COMMERCIAL-LICENSE.md) and the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html); keys are available at [regira.com/licensing](https://regira.com/licensing).
