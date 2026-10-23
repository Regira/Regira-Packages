# Overriding the entity request handlers
<!-- {% raw %} -->

As of 2026-10-23. Sources: the `Regira-Packages` repository, branch `wip` at `eb79418` (Regira.Entities.Mediator 6.6.0, published), read in `src/Entities.Mediator`, `src/Entities.Web/Endpoints/EntityEndpointMapper.cs`, `src/Entities.DependencyInjection/Validation`, `tests/Entities.Mediator.Testing`, `src/Common.Entities/docs/web-endpoints.md` and `src/Common.Entities/ai/entities.patterns.md` (*Entity operations outside a controller*). Findings come from reading the code. Nothing here was probed or built.

**Status: built 2026-10-23 with option (a), uncommitted on `wip` (see *Outcome*). A first version also proposed replacing a default handler for every entity; that was dropped on review (see *Considered and left out*).**

## Answer

**Overriding one operation of one entity already works, and it is the right extension point.** A consumer registers a closed `IEntityRequestHandler<TRequest, TResponse>`, usually derived from the public default handler, and overrides its `virtual Handle`. It answers for every caller. Pipeline behaviours wrap every operation, and the whole sender can be swapped. Tests cover all three, and the guide documents them.

Replacing an endpoint does not cover the same ground. It changes one HTTP route. A job, an import, a domain action or a MediatR pipeline sends the same request without passing through that route, and controllers and mapped endpoints are separate, so an endpoint override has to be written once per surface. Each layer has its own job:

| Layer | Who passes through it | What belongs there |
|---|---|---|
| Endpoint | That one route | HTTP concerns: status codes, headers, attributes |
| Handler | Every sender: both endpoint surfaces, jobs, MediatR | Operation-level work: changing what a request answers (the guide's `ProductDetailsHandler` adds live stock), wrapping the whole save (the guide's Reservation room lock) |
| Write pipeline (preppers, validators, global filters) | Everything, direct `IEntityService` calls included | Rules about the data itself |

**Two gaps remain**, and each one fails without the consumer seeing it:

1. **A `PATCH` skips a `SaveCommand` override.** `PatchHandler` saves through the shared routine (`EntityHandlerServices.Save`), not through the `SaveCommand` handler. An authorization rule placed in a `SaveHandler` override therefore never runs on `PATCH`. The write pipeline does run, since the routine calls `IEntityService.Save`. The guide warns about the gap in one sentence ("overrides both").
2. **An override for the wrong type list is ignored without a word.** The sender asks the container for exactly the closed request type that a surface sends. A handler registered for `ListQuery<Product, int, SearchObject<int>, ProductDto>`, while the mapped endpoint sends `ListQuery<Product, int, SearchObject, ProductDto>`, is never resolved, and the default answers instead. The guide spells out this exact case, because a test run hit it.

The proposal closes both: a one-line change to what `PatchHandler` calls (or a warning instead, see *Decision*), a startup check, and a guide that points data rules at the write pipeline first.

## What exists today

| What | Where | Effect |
|---|---|---|
| Closed handler override | `EntitySender.ResolveHandler`: `GetService<IEntityRequestHandler<TRequest, TResponse>>()`, otherwise the default | Replaces one request type of one entity. Tested in `A_Closed_Handler_Replaces_The_Default_For_Its_Request_Alone` |
| Default handlers to derive from | `Regira.Entities.Mediator.Handlers`: 12 public, unsealed classes with `protected IServiceProvider Services` and `public virtual Handle` | `base.Handle` covers work before and after (authorize, enrich, re-shape the request with `with { }`). Tested in `A_Subclassed_Default_Handler_Can_Change_One_Operation` |
| Pipeline behaviours | `IEntityPipelineBehavior<,>`, open or closed, first registered outermost | Cross-cutting work around every operation, or around one request type |
| Sender replacement | `AddEntityMediator()` is a `TryAddScoped`; `EntitySender.Send` is `virtual` | A mediator library, or a custom dispatcher, ends in an `EntitySender` (recipe tested in `MediatorRecipeTests`) |
| PATCH save | `PatchHandler.Handle` ends in `Services.Save<…>(mergedInput, request.Id, token)` | Runs the write pipeline but skips any `SaveCommand` override. Documented in `web-endpoints.md` and in `entities.patterns` → *Replacing one operation for one entity*, whose Reservation example overrides both handlers |
| Type lists the surfaces send | `EntityEndpointMapper` (a plain `int` `For<>()` sends the `SearchObject` record), the controller bases' generic arguments; `ControllerDtoShapeSource` and `EntityEndpointDtoShapeSource` already read both for the startup checks | The information a mismatch check needs is already collected |
| Startup checks | `IEntityRegistrationValidator`, registered by the DI package; Web adds its own (`ControllerRegistrationValidator`, `EntityEndpointValidator`) | A new check is a single class |

## Design

### 1. The PATCH gap

**Option (a): a `PATCH` ends in the `SaveCommand` handler.** `PatchHandler.Handle` keeps everything up to the merged, validated input. Its last line changes:

```csharp
// before
return await Services.Save<TEntity, TKey, TDto, TInputDto>(mergedInput, request.Id, token);
// after: the SaveCommand handler the sender would pick, the app's override or the default
var save = EntityRequestHandlers.Resolve<SaveCommand<TEntity, TKey, TDto, TInputDto>, SaveResult<TDto>>(Services);
return await save.Handle(new SaveCommand<TEntity, TKey, TDto, TInputDto>(mergedInput, request.Id, ValidateInput: false), token);
```

- The handler is called directly, not sent. Behaviours therefore run once, for the `PatchCommand`: an audit behaviour still logs one operation, and `Duration` is written once.
- `ValidateInput: false`, because the merged input has just been checked. That is what the controllers pass for the same reason.
- The resolution `EntitySender` uses today moves into an `internal static EntityRequestHandlers.Resolve<TRequest, TResponse>(IServiceProvider)`, shared by the sender and `PatchHandler`. No public signature changes.

What it means for an app:

| App | Effect |
|---|---|
| No `SaveCommand` override (most apps) | None: the default `SaveHandler` with `ValidateInput: false` *is* `Services.Save` |
| Overrides `SaveCommand` only | `PATCH` is now covered. This is the fix |
| Overrides both, as the guide advised | The save override now runs inside the patch override, so that logic runs twice. Harmless for a check, or for the guide's Reservation lock: `RoomLock` takes its "transaction already open" path on the inner call, and a second lock on the same rows inside one transaction does nothing more. Wrong for a side effect such as an email or an audit row |

The guide can then probably drop `ReservationPatchHandler`, since the save override sees the merged rooms. Its test should confirm that before the example changes.

**Option (c): no runtime change, a startup warning instead.** The check of item 2 also reports a **Warning** for an entity that has a `SaveCommand` override but no `PatchCommand` override, while its `PATCH` is served (a controller, or a mapped endpoint not excluded with `Exclude(EntityEndpoint.Patch)`). Example: *"ReservationSaveHandler overrides SaveCommand<Reservation, …> but PATCH reservations/{id} saves without it. Override PatchCommand<Reservation, …> too, or move the rule into the write pipeline."*

An opt-in switch (`EntityMediatorOptions.PatchUsesSaveHandler`) was also considered. It keeps the published default like (c), but adds an option to keep and still leaves the gap open for every app that does not know the flag, so it is not offered.

### 2. A startup warning for an override no surface reaches

A new `EntityRequestHandlerValidator` in `Regira.Entities.Web` (the package that knows what both surfaces send) reads every closed `IEntityRequestHandler<TRequest, TResponse>` registration whose `TRequest` is a built-in request:

- If `TEntity` has no `For<>()` registration, it says nothing: the request was never going to work, and its first send already names what was registered.
- If `TEntity` is registered and neither its controller nor its mapped endpoints send exactly `TRequest`, it reports a **Warning**. The message names the handler, the type list it was registered for, and the type list or lists the surfaces send, and ends with "unless the app sends this type list itself".

Example: *"ProductListHandler answers ListQuery<Product, int, SearchObject<int>, ProductDto>, but the mapped endpoints of Product send ListQuery<Product, int, SearchObject, ProductDto>. It is reached only by a sender of the app's own."*

It reuses the type lists `ControllerDtoShapeSource` and `EntityEndpointDtoShapeSource` already gather. Like the other checks, it runs where startup validation runs (Development by default). Under option (c) the PATCH warning lives in the same class.

### 3. The guide: data rules go to the write pipeline first

`entities.patterns` → *Replacing one operation for one entity* and `web-endpoints.md` gain the layer table above, in short form. A rule about the data (who may write a row, what a field may hold) goes into the write pipeline, which every path runs, PATCH and direct `IEntityService` calls included. A handler override is for work that has to wrap the whole operation or change what it answers, and an endpoint override is for HTTP. This shrinks the set of apps that ever need to override `SaveCommand`, whichever option is picked.

### Considered and left out

- **Replacing a default handler for every entity**, with `services.AddEntityRequestHandler(typeof(SaveCommand<,,,>), typeof(MySaveHandler<,,,>))` and a per-container handler map. Dropped on review: a rule for every entity belongs in the write pipeline, and work around every operation is a behaviour. That leaves new API with no defect behind it. If a consumer asks for it later, the earlier draft's design still applies: precedence closed → map → default, checked at the call, a constraint miss throws, the factory cache moves off the `static` dictionary. One cost of leaving it out: an app's own open-generic request (`RestoreCommand<TEntity, TKey>`) still needs one closed registration per entity.
- **`services.AddEntityRequestHandler<THandler>()`**, reading the closed interfaces off the handler class, and assembly scanning. It saves typing the response type once. The closed registration is already checked by the compiler, so it fixes nothing.
- **Protected step methods on the default handlers** (`MapInput`, `Persist`, `Reread`), or making `EntityHandlerServices` public. These are a large new surface to keep compatible. Every customization found in the guides and test runs fits before or after `base.Handle`, in the write pipeline, or in a behaviour. The PATCH save is the one case that did not fit, and item 1 handles it.
- **Unsealing the request records.** A request of the app's own is its own record, and a subclass of `SaveCommand` would not reach the `SaveCommand` handler anyway.

## Decision

**The PATCH gap: option (a), a `PATCH` through the `SaveCommand` handler, or option (c), a startup warning?**

*Recommended: (a).* It closes the gap instead of pointing at it, and the rule becomes simple: every write ends in the `SaveCommand` handler. It changes behaviour only for apps that override `SaveCommand`, and for those that override both handlers it runs the save override twice. The package was published recently, and the guide's own example survives the change.

*(c)* changes no published behaviour and adds no API. Together with item 2 it is one validator. The consumer still has to act on the warning, and the warning only shows where startup validation runs.

Versioning, your call as always: (a) changes what an existing call does, so a minor for `Regira.Entities.Mediator` fits it best. The new check follows the precedent of the 6.6.1 `QSearchValidator` warning, which shipped as a patch.

## Steps

1. Move handler resolution into the internal `EntityRequestHandlers.Resolve`. No behaviour change. *(Option (a) only.)*
2. Option (a): `PatchHandler` ends in the resolved `SaveCommand` handler. Tests: a `SaveCommand` override runs on `PATCH`, on the controller and on the mapped surface; behaviours run once per `PATCH`; the existing patch tests pass unchanged without an override.
   Option (c): the save-without-patch warning in the validator of step 3. Tests: it warns for a `SaveCommand` override alone; it is silent with both overrides, and when `PATCH` is excluded.
3. `EntityRequestHandlerValidator` in Entities.Web. Tests: the `SearchObject<int>` case warns on a mapped-endpoint entity; a matching override and an unregistered entity say nothing.
4. Docs and guides:
   - `web-endpoints.md` and `entities.patterns` → *Replacing one operation for one entity*: the layer table; under (a), drop the "overrides both" bullet and simplify the Reservation example; under (c), point the bullet at the new warning.
   - The Mediator card and the CHANGELOG (under (a), with the double-run note and its fix).
   - The `entity-operations` how_to fallback in Regira-Tools `HowToTools.cs`, which must stay identical to the recipe body (its test compares the two).
   - Rebuild the knowledge base.

## Risks

- **Double execution for apps that override both handlers** (option (a)). Narrow, and called out in the CHANGELOG with the fix: drop the `PatchCommand` override, or make it skip what the `SaveCommand` override now does.
- **A warning with no defect behind it** (item 2) for an app that deliberately overrides a type list only its own code sends. It is a warning and its text says so. `EntityValidationOptions` can only switch every check off at once (`Enabled`), so there is no per-check opt-out. Add one only if a consumer asks for it.

## Outcome

Built on 2026-10-23 with option (a), as designed, with these deviations:

- **The Reservation example keeps `ReservationPatchHandler`.** Without it, the merge would read the stored booking before the lock is taken, so a second concurrent patch of the same booking would merge from what it read before the first one committed. The override stays, and its comment now says why: it takes the locks before the merge. The save it ends in runs `ReservationSaveHandler` too, which locks again inside the same transaction, harmlessly. No test covers the recipe; this was decided by reading `RoomLock`.
- **The surfaces' type lists are recorded per mapped entity.** `MappedEntity` gained `Requests`, filled in `EntityEndpointMapper.Map` from the same type arguments it maps with. Controllers' lists come from their `EntityControllerBase<…>` arguments (`EntityRequestTypes.OfController`). Both live in `Validation/EntityRequestHandlerValidator.cs`, because `Regira.Entities.Mediator` has no `InternalsVisibleTo` for Web.
- **The check covers the six entity requests only.** It skips the attachment requests, whose type lists name the link entity.
- **The layer guidance went into the *Replacing one operation for one entity* subsection**, not the recipe body above it, so the Regira-Tools `entity-operations` fallback needed no change.

What changed:

- `Regira.Entities.Mediator`:
  - new internal `EntityRequestHandlers` (the resolution and default map moved out of `EntitySender`);
  - `PatchHandler` ends in the resolved `SaveCommand` handler;
  - the `PatchCommand` XML doc and the card were updated.
- `Regira.Entities.Web`:
  - `EntityRequestHandlerValidator`, registered by `ValidateEntityControllers()`;
  - `MappedEntity.Requests`.
- Docs and guides: `web-endpoints.md`, `entities.patterns` (*Replacing one operation for one entity*, the Reservation comment), and the CHANGELOG (two bullets plus the guides clause).

Tests:
- Mediator: 28 (two new: `A_Patch_Ends_In_The_Save_Handler_Of_The_App`, `A_Patch_Runs_The_Behaviours_Once`).
- Web: 298 (two new: `A_Patch_Ends_In_The_Save_Handler_On_Both_Surfaces`, `A_Handler_Override_No_Surface_Reaches_Is_Reported_At_Startup`).
- GuideVerifier: entities, entities-examples and web, 90 snippets.

Still to do:
- The `<Version>` of both packages is still 6.6.0, which is already published. The CHANGELOG bullets say 6.6.1, the cycle's number, while the rule calls for a minor for Mediator's behaviour change.
- The knowledge base needs a rebuild.
<!-- {% endraw %} -->
