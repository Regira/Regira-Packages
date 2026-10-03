# Regira.Entities.Mapping.AutoMapper

[AutoMapper](https://www.nuget.org/packages/AutoMapper) as the entity ↔ DTO mapper for [Regira Entities](https://regira.github.io/Regira-Packages/src/Common.Entities/). `options.UseAutoMapper()` in the `UseEntities()` callback makes AutoMapper the `IEntityMapper`. Unlike the [Mapster adapter](https://regira.github.io/Regira-Packages/src/Entities.Mapping.Mapster/) it does not map by convention: every entity ↔ DTO pair is registered with the per-entity `UseMapping<>()` / `AddMapping<>()` statements, each of which creates the AutoMapper map. No profile assemblies are scanned; an optional callback receives the `IServiceProvider` and the `IMapperConfigurationExpression`.

This adapter is deprecated: new applications use [Regira.Entities.Mapping.Mapster](https://regira.github.io/Regira-Packages/src/Entities.Mapping.Mapster/).

## Installation

```xml
<PackageReference Include="Regira.Entities.Mapping.AutoMapper" Version="6.*" />
```

- The AutoMapper dependency range is `[14.0.0,17.0.0)`, so NuGet resolves 14.0.0, the last MIT-licensed release. AutoMapper 15.0.0 and later are licensed by their vendor under RPL-1.5 or a paid licence; the fix for advisory GHSA-rvv3-g6hj-g44x needs 16.x, which you pin yourself under those terms.
- The package depends on Regira.Entities.Web, and with it ASP.NET Core; the Mapster adapter depends only on Regira.Entities.DependencyInjection.

## Documentation

- [Mapping](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/mapping.html) — registering maps for AutoMapper, configuring the engine, and after-mappers
- [Regira Entities](https://regira.github.io/Regira-Packages/src/Common.Entities/) — the entity framework this package plugs into

## License

Regira Commercial License, with a free tier that applies automatically — no key is needed within its limits. A license key removes the limits and is validated fully offline. See the [license text](https://github.com/Regira/Regira-Packages/blob/main/legal/REGIRA-COMMERCIAL-LICENSE.md) and the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html); keys are available at [regira.com/licensing](https://regira.com/licensing).
