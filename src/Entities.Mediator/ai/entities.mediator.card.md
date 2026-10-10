# Regira.Entities.Mediator — index card

> The Regira Entities operations as requests sent through `IEntitySender` — what every generated endpoint sends, and
> what a job, an import or the app's own endpoint sends to get the same answer. `Regira.Entities.Web` brings it and
> `UseEntities()` registers it. The guide lives on `Regira.Entities`:
> `get_package(id: "Regira.Entities", section: "entities.patterns", heading: "Entity operations outside a controller")`.

- **Requests** (`Regira.Entities.Mediator.Requests`): `DetailsQuery`, `ListQuery`, `SearchQuery`, `SaveCommand`,
  `PatchCommand`, `DeleteCommand`, plus an owner's attachment routes. They are positional records, built with their
  constructor: `new DetailsQuery<Product, int, ProductDto>(id)`. The type list is the endpoint's — a plain
  `For<Product>()` lists with the `SearchObject` record.
- **`null` means not found;** a refused write throws `EntityInputException`, `EntityConstraintException` or
  `EntityConcurrencyException`.
- **Replace one operation for one entity** with a closed `IEntityRequestHandler<TRequest, TResponse>`, usually derived
  from the default handler (`Regira.Entities.Mediator.Handlers`); it answers for every surface and sender.
- **Wrap every operation** with an open-generic `IEntityPipelineBehavior<,>`, the first registered outermost.
- **A request of the app's own** implements `IEntityRequest<TResponse>`, declares its `EntityType` and `Operation`,
  and needs a registered handler.
- **MediatR:** `Regira.Entities.Mediator.MediatR` (`get_package_card(id: "Regira.Entities.Mediator.MediatR")`).
- The response envelopes (`DetailsResult<>`, `ListResult<>`, …) ship here under their `Regira.Entities.Web.Models`
  namespace.
