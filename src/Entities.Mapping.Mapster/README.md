# Regira.Entities.Mapping.Mapster

[Mapster](https://www.nuget.org/packages/Mapster.DependencyInjection) as the entity ↔ DTO mapper for [Regira Entities](https://regira.github.io/Regira-Packages/src/Common.Entities/). `options.UseMapsterMapping()` in the `UseEntities()` callback maps each entity to and from its DTOs by convention, so an entity whose DTOs share its shape needs no mapping registration of its own. An optional callback configures the shared `TypeAdapterConfig`.

## Installation

```xml
<PackageReference Include="Regira.Entities.Mapping.Mapster" Version="6.*" />
```

## Documentation

- [Mapping](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/mapping.html) — convention-based mapping, configuring the engine, and after-mappers
- [Regira Entities](https://regira.github.io/Regira-Packages/src/Common.Entities/) — the entity framework this package plugs into

## License

Regira Commercial License, with a free tier that applies automatically — no key is needed within its limits. A license key removes the limits and is validated fully offline. See the [license text](https://github.com/Regira/Regira-Packages/blob/main/legal/REGIRA-COMMERCIAL-LICENSE.md) and the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html); keys are available at [regira.com/licensing](https://regira.com/licensing).
