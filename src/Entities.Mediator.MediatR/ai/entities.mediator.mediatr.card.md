# Regira.Entities.Mediator.MediatR — index card

> MediatR dispatch for the Regira Entities operations. The guide lives on `Regira.Entities`:
> `get_package(id: "Regira.Entities", section: "entities.patterns", heading: "Dispatching through MediatR")`. The
> requests it carries are in *Entity operations outside a controller*, the section that heading belongs to.

- **One call:** `options.UseMediatR()` inside `UseEntities(…)` (namespace `Regira.Entities.Mediator.MediatR`), before
  or after the rest. The app registers MediatR itself: `services.AddMediatR(cfg => …)`.
- **Choose the MediatR version by licence.** MediatR 13 and later need the app's licence key; an app without one pins
  **MediatR 12.5.0**, the last Apache-2.0 release. The adapter works with 12, 13 and 14.
- **Every entity request is one MediatR request type**, `EntityRequestMessage`, holding the entity request: a
  behaviour reads `message.Request.EntityType` and `message.Request.Operation`. A behaviour keyed on a single entity
  request type never sees one.
- **Unchanged under MediatR:** a closed `IEntityRequestHandler<,>` override, the `IEntityPipelineBehavior<,>`s and
  `Duration` — the envelope's handler hands the request to the same executor the in-house sender uses.
- ⚠️ **Let the entity exceptions through.** A behaviour that wraps `EntityInputException`,
  `EntityConstraintException` or `EntityConcurrencyException` in an exception of its own turns the endpoints' 400
  and 409s into 500s.
