# Regira.Entities.Web

The ASP.NET Core Web API layer of [Regira Entities](https://regira.github.io/Regira-Packages/src/Common.Entities/). A controller derived from `EntityControllerBase` exposes an entity's REST endpoints — details, list, search with paging, create, modify, patch, save and delete — over its `IEntityService`, mapping to and from the DTO types it names through `IEntityMapper`. It is the top-level entry point for a web API and brings Regira.Entities.DependencyInjection and Regira.Entities.EFcore transitively.

## Installation

```xml
<PackageReference Include="Regira.Entities.Web" Version="6.*" />
```

The package brings EF Core but no database provider: add one (for example `Microsoft.EntityFrameworkCore.Sqlite`) to the application.

## Documentation

- [Quickstart](https://regira.github.io/Regira-Packages/docs/quickstart.html) — from an empty folder to a working entity CRUD API
- [Web Endpoints](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/web-endpoints.html) — choosing a controller base, the standard endpoints, paging, response types and error codes; the generated endpoints ship without authorization
- [Regira Entities](https://regira.github.io/Regira-Packages/src/Common.Entities/) — the entity framework, its pipeline and the full documentation index

## License

Regira Commercial License, with a free tier that applies automatically — no key is needed within its limits. A license key removes the limits and is validated fully offline. See the [license text](https://github.com/Regira/Regira-Packages/blob/main/legal/REGIRA-COMMERCIAL-LICENSE.md) and the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html); keys are available at [regira.com/licensing](https://regira.com/licensing).
