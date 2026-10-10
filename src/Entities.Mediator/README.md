# Regira.Entities.Mediator

The entity operations of [Regira Entities](https://regira.github.io/Regira-Packages/src/Common.Entities/) as requests: `DetailsQuery`, `ListQuery`, `SearchQuery`, `SaveCommand`, `PatchCommand` and `DeleteCommand`, sent through `IEntitySender`. The generated endpoints of Regira.Entities.Web send these requests, and a background job, an import or an application's own endpoint can send the same ones, getting what the endpoint answers: paging defaults, the re-read after a save, the DTO mapping and the result envelope. `UseEntities()` registers the sender. A closed `IEntityRequestHandler` replaces one operation for one entity, and an `IEntityPipelineBehavior` runs around every request. An adapter package, such as Regira.Entities.Mediator.MediatR, replaces the in-house sender with a third-party mediator.

## Installation

Regira.Entities.Web brings this package. A host without the web layer references it directly:

```xml
<PackageReference Include="Regira.Entities.Mediator" Version="6.*" />
```

## Documentation

- [Entity Operations](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/web-endpoints.html#entity-operations) — sending the endpoints' requests from other code, input validation, replacing an operation, behaviours and MediatR
- [Web Endpoints](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/web-endpoints.html) — the endpoints that send these requests

## License

Regira Commercial License, with a free tier that applies automatically — no key is needed within its limits. A license key removes the limits and is validated fully offline. See the [license text](https://github.com/Regira/Regira-Packages/blob/main/legal/REGIRA-COMMERCIAL-LICENSE.md) and the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html); keys are available at [regira.com/licensing](https://regira.com/licensing).
