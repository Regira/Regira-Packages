# Regira.Entities.Mediator — index card

> The Regira Entities operations as requests sent through `IEntitySender` — what every generated endpoint sends, and
> what a job, an import or the app's own endpoint sends to get the same answer. `Regira.Entities.Web` brings it and
> `UseEntities()` registers it. The guide lives on `Regira.Entities`:
> `get_package(id: "Regira.Entities", section: "entities.patterns", heading: "Entity operations outside a controller")`.

- **Requests** (`Regira.Entities.Mediator.Requests`): `DetailsQuery`, `ListQuery`, `SearchQuery`, `SaveCommand`,
  `PatchCommand`, `DeleteCommand`, plus an owner's attachment routes. They are positional records, built with their
  constructor: `new DetailsQuery<Product, int, ProductDto>(id)`. The type list is the endpoint's — a plain
  `For<Product>()` lists with the `SearchObject` record, and so does `For<Product, int>()` on mapped endpoints.
- **`null` means not found;** a refused write throws `EntityInputException`, `EntityConstraintException` or
  `EntityConcurrencyException`.
- **Replace one operation for one entity** with a closed `IEntityRequestHandler<TRequest, TResponse>`, usually derived
  from the default handler (`Regira.Entities.Mediator.Handlers`); it answers for every surface and sender. A `PATCH`
  ends in the `SaveCommand` handler, so a save override covers it. Startup validation warns for a handler of the six
  requests above whose type list no endpoint sends. An owner's attachment routes are replaced the same way, through
  their own requests, which no startup check covers —
  `get_package(id: "Regira.Entities", section: "entities.patterns", heading: "Changing an attachment operation")`.
- **Wrap every operation** with an open-generic `IEntityPipelineBehavior<,>`, or one request type with a closed one;
  open and closed share one registration order, the first registered outermost.
- **A request of the app's own** implements `IEntityRequest<TResponse>`, declares its `EntityType` and `Operation`,
  and needs a registered handler.
- **Through a mediator library** such as MediatR: replace the `IEntitySender` registration with a sender whose handler
  ends in the in-house `EntitySender` —
  `get_package(id: "Regira.Entities", section: "entities.patterns", heading: "Dispatching through a mediator library")`.
- The response envelopes (`DetailsResult<>`, `ListResult<>`, …) ship here under their `Regira.Entities.Web.Models`
  namespace.
