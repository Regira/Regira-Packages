# Regira.Entities.Mediator.MediatR

[MediatR](https://www.nuget.org/packages/MediatR) dispatch for the entity operations of [Regira Entities](https://regira.github.io/Regira-Packages/src/Common.Entities/). `options.UseMediatR()` in the `UseEntities()` callback replaces the in-house `IEntitySender`, so every entity request — the generated endpoints' and any other caller's — runs inside the application's MediatR pipeline and its behaviours. Each request travels as one MediatR request type, `EntityRequestMessage`, whose handler passes it on to the same executor the in-house sender uses, so handler overrides, entity behaviours and `Duration` work as without MediatR. The application registers MediatR itself, with its licence key from MediatR 13 on; the package works with MediatR 12, 13 and 14.

## Installation

```xml
<PackageReference Include="Regira.Entities.Mediator.MediatR" Version="6.*" />
```

## Documentation

- [Entity Operations](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/web-endpoints.html#entity-operations) — the requests the endpoints send, and dispatching them through MediatR

## License

Regira Commercial License, with a free tier that applies automatically — no key is needed within its limits. A license key removes the limits and is validated fully offline. See the [license text](https://github.com/Regira/Regira-Packages/blob/main/legal/REGIRA-COMMERCIAL-LICENSE.md) and the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html); keys are available at [regira.com/licensing](https://regira.com/licensing).
