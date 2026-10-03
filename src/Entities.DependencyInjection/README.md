# Regira.Entities.DependencyInjection

The registration API of [Regira Entities](https://regira.github.io/Regira-Packages/src/Common.Entities/). `services.UseEntities<TContext>(o => o.UseDefaults())` sets the global options and wires the `DbContext` plumbing, and each `For<TEntity>(...)` registers an entity's `IEntityService` together with its query builders, preppers, validators, primers and reactors. At host start it logs the registered entities with the resolved license tier and, in the Development environment by default, validates the registrations. It has no ASP.NET Core dependency, so it also serves console and worker hosts; Regira.Entities.Web adds the controllers on top.

## Installation

```xml
<PackageReference Include="Regira.Entities.DependencyInjection" Version="6.*" />
```

- This package enforces the Regira Entities free tier: every entity type registered through `For<>()` counts, up to 5 simple + 2 complex per application. A key registered beforehand with `UseRegira(...)` from [Regira.Licensing](https://regira.github.io/Regira-Packages/src/Common.Licensing/), a dependency of this package, removes the limit.
- It depends on Regira.Entities.EFcore and so brings EF Core, but no database provider: add one (for example `Microsoft.EntityFrameworkCore.Sqlite`) to the application.

## Documentation

- [Dependency Injection](https://regira.github.io/Regira-Packages/src/Common.Entities/#dependency-injection) — a basic setup: `UseRegira`, `UseEntities` and inline or class-based `For<>()` configuration
- [Services → Dependency Injection](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/services.html#dependency-injection) — the full configuration, with every helper service registered globally and per entity
- [Startup validation](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/built-in-features.html#startup-validation) — the registration checks `UseEntities()` runs at host start, and how to configure them

## License

Regira Commercial License, with a free tier that applies automatically — no key is needed within its limits. A license key removes the limits and is validated fully offline. See the [license text](https://github.com/Regira/Regira-Packages/blob/main/legal/REGIRA-COMMERCIAL-LICENSE.md) and the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html); keys are available at [regira.com/licensing](https://regira.com/licensing).
