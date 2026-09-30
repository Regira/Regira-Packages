# Entity validators for Regira Entities

As of 2026-09-29. Sources: the `Regira-Packages` repository, branch `wip`, as read on 2026-09-29. FluentValidation behaviour comes from its documented public API and has not been checked against a build in this repository; step 5's tests pin it down.

**Status: implemented in the 6.5.0 packages, which depart from this proposal in places.** For example, a refused `Add` or `Modify` takes back the rows a `Related()` sync marked, where this page keeps their states. For the current behaviour, read `entities.instructions` § Step 8 → Validators and `docs/services.md` → Entity Validators, not this page.

## Recommendation

Decision: add a validation stage to the entity write pipeline, with its own interfaces:

- a non-generic `IEntityValidator`, which every write service imports as one list, `IEnumerable<IEntityValidator>`;
- `IEntityValidator<TScope>`, where the scope is the entity type, a base class or an interface.

Which validators apply to an entity follows the rules that preppers, primers and global filters already use: a validator scoped to `IHasTenantId` checks every tenant-owned entity, whichever service saves it.

The stage runs inside `EntityWriteService` after every prepper and before the entity is tracked, and on `Remove`. FluentValidation support ships as a separate adapter package, `Regira.Entities.Validation.FluentValidation`, next to the Mapster and AutoMapper adapters.

A validator is only worth adding for what a prepper cannot do:

1. **A fixed position.** Validators run after every prepper, including the `Related()` sync and the global preppers. A validating prepper works only when it is registered last, and a global prepper can never be.
2. **Collected errors, one exception.** Validators add errors to a shared list. The write service throws once, with an `EntityInputException<TEntity>` closed over its own `TEntity`. The client gets every error in one 400, and the generated endpoints' typed `catch` always matches.
3. **Delete coverage.** No prepper runs on `Remove`.
4. **Separate roles.** Preppers change the entity, validators check it. The guides get one place for "reject this input".

A prepper that validates keeps working, but should not be encouraged.

The proposal, in order:

1. `Regira.Entities`: the validator interfaces, a base class, a context and an error record. Also the scope rule shared with global filters, and one extension that runs the matching validators for any service.
2. `Regira.Entities.EFcore`: `ValidateItem` in `Add`, `Modify` and `Remove`, behind a new constructor. The current constructor stays.
3. `Regira.Entities.DependencyInjection`: registration that mirrors the prepper methods, plus startup warnings for a write service that cannot receive validators and for a validator no entity is in scope for.
4. `Regira.Entities.Web`: `DELETE` answers 400 on a rule breach, and one helper maps the exception to `ModelState` for every catch site.
5. The FluentValidation adapter package.
6. Guides, changelog and versions.

## What exists today

| Gap | Where | Effect |
| --- | --- | --- |
| Validation order depends on registration order | `EntityWriteService.PrepareItem` runs preppers in registration order (`EntityWriteService.cs:110`). Global preppers are registered in the `UseEntities()` callback, before any `For<>()`, so they run first (`entities.instructions.md:899`) | A validating prepper must be registered after the preppers that compute values. A global one cannot be |
| Errors surface one at a time | A prepper throws on its first failure, and the preppers after it never run | The client fixes errors one round trip at a time |
| The exception type decides the status code | The generated save catches only `EntityInputException<TEntity>` for its own `TEntity` (`ControllerExtensions.cs:209`, `EntityAttachmentControllerBase.cs:80`). Only `EntityExceptionFilter` catches the base type (`EntityExceptionFilter.cs:27`), and only when `MapEntityExceptions()` or `ConfigureDefaultJsonOptions()` registered it | A global prepper throws with whatever `T` it picks. Without the filter, that is a 500 |
| One message per key | `InputErrors` is `IDictionary<string, string>` (`EntityInputException.cs:17`) | Two failures on one field cannot both reach the client |
| Nothing validates a delete | `Remove` runs no preppers (`EntityWriteService.cs:80`). The guide sends delete rules to a controller `Delete` override or a wrapping service (`entities.instructions.md:509`). The `Delete` helper catches only constraint and concurrency exceptions (`ControllerExtensions.cs:353`–`360`) | An `EntityInputException` thrown from a `Remove` override is a 500 unless the filter is registered |
| The docs have no home for validation | `built-in-features.md:14` and `entities.instructions.md:884` list "Validation" as a wrapping-service use, with no example. `entities.instructions.md:219` lists foreign-key validation as a prepper use | Consumers and agents pick a different place each time |

## Design

### Types, in `Regira.Entities`

Namespace `Regira.Entities.Validators.Abstractions`, beside `Preppers.Abstractions` and `Reactors.Abstractions`.

```csharp
// what a write service imports: IEnumerable<IEntityValidator>
public interface IEntityValidator
{
    Task Validate(IEntityValidatorContext context, CancellationToken token = default);
}
// TScope: the entity type, a base class or an interface
public interface IEntityValidator<in TScope> : IEntityValidator
{
    Task Validate(IEntityValidatorContext<TScope> context, CancellationToken token = default);
}

public interface IEntityValidatorContext
{
    object Item { get; }
    object? Original { get; }
    EntityWriteOperation Operation { get; }
    IReadOnlyList<EntityInputError> Errors { get; }
    void AddError(string key, string message);
}
public interface IEntityValidatorContext<out TEntity> : IEntityValidatorContext
{
    new TEntity Item { get; }
    new TEntity? Original { get; }
}

public enum EntityWriteOperation { Add, Modify, Remove }

public abstract class EntityValidatorBase<TScope> : IEntityValidator<TScope>
    where TScope : class
{
    public abstract Task Validate(IEntityValidatorContext<TScope> context, CancellationToken token = default);
    // per-item opt-out, like a primer's CanPrepare
    public virtual bool CanValidate(TScope item) => true;

    Task IEntityValidator.Validate(IEntityValidatorContext context, CancellationToken token)
    {
        var typed = (IEntityValidatorContext<TScope>)context;
        return CanValidate(typed.Item) ? Validate(typed, token) : Task.CompletedTask;
    }
}
```

`EntityInputError` is a record, `EntityInputError(string Key, string Message)`, in `Regira.Entities.Models` beside `EntityInputException`.

### Scope: which validators apply

Validators follow the rules that preppers, primers and global filters already use. Each family has a non-generic interface that the runtime imports as one list, and a generic interface whose type argument is the scope:

| Family | Imported as | Scope declared by | Matched against | Per-item opt-out |
| --- | --- | --- | --- | --- |
| Preppers | `IEnumerable<IEntityPrepper>`, in `EntityWriteService` | `IEntityPrepper<TEntity>` | the write service's declared `TEntity` (`FindMatchingServices`, `GenericEntityServiceExtensions.cs:25`) | none |
| Primers | `IEnumerable<IEntityPrimer>`, in the `SaveChanges` interceptor | `IEntityPrimer<T>` | the runtime type of each pending entry (`EntityPrimerContainerInterceptor.cs:56`–`66`) | `CanPrepare(T)` |
| Global filters | `IEnumerable<IGlobalFilteredQueryBuilder>`, in `QueryBuilder` | `IGlobalFilteredQueryBuilder<TEntity, TKey>` | the query's `TEntity`, plus every type it derives from or implements (`GlobalFilterScope.cs`, `QueryBuilder.cs:78`) | none |
| **Validators** | `IEnumerable<IEntityValidator>`, in `EntityWriteService` | `IEntityValidator<TScope>` | the item's runtime type, plus every type it derives from or implements | `CanValidate(TScope)` |

The rule: a validator's scopes are the type arguments of the `IEntityValidator<>` interfaces it implements. It applies to an item when the item's runtime type is one of those scopes, derives from one, or implements one.

| Scope | Runs for |
| --- | --- |
| `IEntityValidator<Order>` | `Order` only |
| `IEntityValidator<Party>` | `Person` and `Organization`, whichever service saves them |
| `IEntityValidator<IHasTenantId>` | every tenant-owned entity |
| `IEntityValidator<IEntity<int>>` | every int-keyed entity, as `FilterIdsQueryBuilder<int>` does for queries |

- **The runtime type, like primers.** Preppers match the declared `TEntity`, so a `Person` saved through the `Party` service would miss its `Person` rules. Validators check the row that is actually written.
  - The context is built for the runtime type, as an `EntityValidatorContext<>` closed over `item.GetType()`, with the constructor cached per type. Covariance then gives every validator the view its scope needs: `Person`, `Party` or `IHasTenantId`. A context closed over the service's `Party` could not be cast for a validator scoped to `Person`.
  - The exception stays closed over the service's `TEntity`, so the endpoint's typed `catch` still matches.
- **Every applicable validator runs, in registration order.** Nothing is picked per family, unlike the key variants of global filters (`GlobalFilterSelector.cs`). Validators only add errors, so one never replaces another.
- **The place of registration never narrows the scope.** Every registration adds to the one list that every write service imports. An `IEntityValidator<IHasCode>` registered inside `For<Order>()` runs for every entity that has a code, just as a prepper scoped to an interface does today. The guide says to register such validators once, globally. To keep a validator on one entity, scope it to that entity's concrete type.
- **One definition of an entity's scope types.** "The type, plus everything it derives from or implements" moves from `GlobalFilterScope.EntityScopeTypes` (internal to `Entities.EFcore`) into `Regira.Entities` as `EntityScopeTypes.Of(Type)`. Global filters, validators and both startup checks call it. The match per (validator type, entity type) is cached, since registrations cannot change after the container is built.
- **Any service can run the validators.** `EntityValidatorExtensions.ValidateItem<TEntity>(this IEnumerable<IEntityValidator> validators, TEntity item, TEntity? original, EntityWriteOperation operation, CancellationToken token)` selects the matching validators, runs them against one context and throws. `EntityWriteService` calls it. A custom `IEntityRepository` or a service over another store imports `IEnumerable<IEntityValidator>` and makes the same call.

Why these shapes:

- **The context is an interface that only the framework implements.** A member added later breaks no consumer. Adding a parameter to `Validate` would break every implementation.
- **A covariant context and a contravariant validator.** An `IEntityValidator<IHasTenantId>` receives an `Order`'s context without a cast, and `e.AddValidator<TenantValidator>()` inside `For<Order>()` compiles because `Order` implements the scope.
- **`CanValidate` sits on the base class, not the interface.** It mirrors `CanPrepare`, which `EntityPrimerBase.PrepareManyAsync` calls for each entity. The runtime needs nothing but `Validate`, so the interface stays at one method.
- **`AddError` rather than a throw or a return value.** Every validator runs and adds to one list. `Errors` lets a later validator skip an expensive check when an earlier one already failed: there is no point querying for the customer when `CustomerId` is missing.
- **Keys follow today's `InputErrors` rule.** The C# property path (`CustomerId`, `Lines[0].Quantity`), and `string.Empty` for an error about the entity as a whole, which is also what `ModelState` uses.
- **Validators read and never write.** This is documented, not enforced. `Item` is the instance that gets saved, so a value a validator sets would still be written.

Delegate forms go in `Regira.Entities.EFcore`, beside `Preppers/EntityPrepper.cs`: `EntityValidator<TScope>(Action<IEntityValidatorContext<TScope>>)` and `EntityValidator<TContext, TScope>(TContext, Func<IEntityValidatorContext<TScope>, TContext, Task>)`. They follow the same scope rule as validator classes.

### Where the stage runs, in `Regira.Entities.EFcore`

| Method | Today | With validators |
| --- | --- | --- |
| `Add` | `PrepareItem` → `DbSet.Add` | `PrepareItem` → `ValidateItem` → `DbSet.Add` |
| `Modify` | read the original → `DropStaleReferencesInGraph` → `CaptureClientTokens` → `PrepareItem` → `TrackAsUpdateOf` | the same, with `ValidateItem` between `PrepareItem` and `TrackAsUpdateOf` |
| `Remove` | `RemoveGuarded` | `ValidateItem` → `RemoveItem` (today's body) |

`ValidateItem(TEntity item, TEntity? original, EntityWriteOperation operation, CancellationToken token)` is `public virtual`, like `PrepareItem`. It calls the `ValidateItem` extension on its imported validators. That extension:

- selects the validators whose scope the item is in (see *Scope*);
- runs all of them, in registration order, against one context;
- if the context holds errors, throws one `EntityInputException<TEntity>` closed over the write service's own `TEntity`, with `Item` set, whatever scope the validators were declared on.

On a failure the root entity is not tracked, because validation comes before `DbSet.Add` and `TrackAsUpdateOf`. Rows a `Related()` sync already marked keep their states, which is also what happens today when a prepper throws. On `Remove`, `Item` is the row the caller loaded and `Original` is `null`. A soft delete of an `IArchivable` goes through `Remove` too, so it is validated as a `Remove`.

**Constructor.** Today the constructor is `EntityWriteService<TContext, TEntity, TKey>(dbContext, readService, preppers, loggerFactory = null)` (`EntityWriteService.cs:22`). The primary constructor gains `IEnumerable<IEntityValidator> validators` after `preppers`. The four-parameter signature stays, as a secondary constructor that passes an empty list.

- Existing subclasses compile unchanged, and those already compiled keep binding.
- DI picks the longer constructor, so the default write service receives validators with no change to registration.
- The int-keyed `EntityWriteService<TContext, TEntity>` and `EntityAttachmentWriteService` get the same pair.
- One source-level edge case: a subclass that passes a literal `null` as the fourth argument now matches both constructors (CS0121). It has to name the argument.

**The split of `Remove`.** `EntityAttachmentWriteService` overrides `Remove` and marks the `Attachment` principal as `Deleted` before it calls `base.Remove` (`EntityAttachmentWriteService.cs:21`). If validation stayed inside `base.Remove`, a rejected delete would leave that principal marked. So `Remove` becomes `ValidateItem` followed by a new `protected virtual Task RemoveItem(TEntity item, CancellationToken token)` that holds today's body. The attachment service overrides `RemoveItem` instead, so it marks the principal only after validation has passed.

### How errors reach the client

- **`EntityInputException` gains `Errors`**, an `IList<EntityInputError>` that can hold several messages per key. `ValidateItem` fills `Errors`. It also fills `InputErrors`, with the messages for each key joined by a space, so every existing reader keeps working.
- **One `ModelState` helper in `Regira.Entities.Web`** reads `Errors` when it is set, then `InputErrors`, then `Message`. It replaces the three copies of that loop today: `ControllerExtensions.cs:209`, `EntityAttachmentControllerBase.cs:80` and `EntityExceptionFilter.cs:27`.
- **The wire format is unchanged**, except that a key can now carry more than one message: `{ "Code": ["…", "…"] }`.
- **The `Delete` helper catches `EntityInputException<TEntity>`** and answers 400 through the same helper, whether or not the exception filter is registered.

### Registration, in `Regira.Entities.DependencyInjection`

The validator methods mirror `ServiceCollectionPrepperExtensions`:

| Where | Prepper today | Validator |
| --- | --- | --- |
| entity builder, class | `e.AddPrepper<T>()` | `e.AddValidator<T>()` |
| entity builder, delegate | `e.Prepare(x => …)` | `e.Validate(ctx => …)` |
| entity builder, delegate with the `DbContext` | `e.Prepare(async (x, db) => …)` | `e.Validate(async (ctx, db) => …)` |
| global, in `UseEntities()` | `options.AddPrepper<T>()`, `options.AddPrepper<IX>(x => …)` | `options.AddValidator<T>()`, `options.AddValidator<IX>(ctx => …)` |
| service collection | `services.AddPrepper<…>()` | `services.AddValidator<…>()` |

The builders that re-declare the prepper methods to return their own type (`EntityServiceBuilderBase`, `EntityServiceBuilder`, `EntityIntServiceBuilder`) get the validator methods the same way. Validators run in registration order, so global validators come first, as global preppers do. For validators the order is harmless: none of them changes the entity, and all of them run after every prepper.

- **Two registrations per validator class**, as `AddPrepper<TEntity, TPrepper>` does. One is the non-generic `IEntityValidator`, which the write service imports. The other is the typed `IEntityValidator<TScope>`, for code that wants to inject one validator directly.
- **A validator class registered twice runs once.** Class registrations go through `TryAddEnumerable`, which removes duplicates by implementation type. So adding the same interface-scoped validator to several `For<>()` blocks does not produce duplicate messages. Delegate registrations are always added, because two delegates of the same closed type are different validators.
- **`e.AddValidator<T>()` requires `T : IEntityValidator<TEntity>`.** A validator scoped to an interface or base class satisfies that through variance whenever the entity is in its scope. As *Scope* explains, it then applies to every entity in that scope, not only this one.

**Startup warnings.** Two new internal validators in `Entities.DependencyInjection/Validation/`:

- **A write service that cannot receive validators.** For each entity that has a validator in scope, this check warns when the entity's registered `IEntityWriteService` implementation has no constructor taking `IEnumerable<IEntityValidator>`. Two cases trigger it:
  - a consumer's write service built on the old constructor;
  - a custom `IEntityRepository`, such as the Identity users recipe.

  Either way those validators would never run. The warning names the `ValidateItem` extension as the fix for a custom service. The check skips factory registrations, whose implementation type it cannot see.
- **A validator with no entity in scope.** This warns about a registered validator whose scope no registered entity satisfies, as `GlobalFilterScopeValidator` does for global filters. The usual cause is a validator scoped to the wrong type. It compiles and starts cleanly, then checks nothing.

Both checks use `EntityScopeTypes`, so they agree with the runtime.

### The FluentValidation adapter

The package is `Regira.Entities.Validation.FluentValidation`, in `src/Entities.Validation.FluentValidation/`, named like `Regira.Entities.Mapping.Mapster`. It depends on:

- `Regira.Entities.DependencyInjection`, for the options extension, as the Mapster adapter does;
- `FluentValidation` and `FluentValidation.DependencyInjectionExtensions`.

It does not use `FluentValidation.AspNetCore`, which is no longer maintained; the adapter has no HTTP part anyway.

```csharp
services.UseEntities<AppDbContext>(o =>
{
    // scans the assembly for validators; UseFluentValidation() alone leaves registering them to you
    o.UseFluentValidation(typeof(OrderValidator).Assembly);
});
```

What it does:

- **One global validator for every entity.** `FluentEntityValidator` implements `IEntityValidator<IEntity>`, and every entity implements `IEntity`.
- **FluentValidation validators follow the same scope rule.** For each item, the adapter resolves `IValidator<T>` for every type in `EntityScopeTypes.Of(item.GetType())`: the runtime type, its base classes and its interfaces. An `AbstractValidator<IHasTenantId>` therefore covers every tenant-owned entity, and an `AbstractValidator<Party>` covers `Person` and `Organization`, like an `IEntityValidator` with the same scope.
  - Each scope type is validated with its own `ValidationContext<T>`, through a generic dispatch.
  - Which scope types have a registered `IValidator<T>` is looked up once per entity type, through `IServiceProviderIsService`, and cached.
  - An entity with no FluentValidation validator costs one cached lookup and nothing else.
- **It always calls `ValidateAsync`**, so `MustAsync` rules that query the `DbContext` work.
- **Each operation maps to a rule set.**
  - `Add` and `Modify` run the rules outside any rule set, plus the `"Add"` or `"Modify"` rule set.
  - `Remove` runs only the `"Remove"` rule set, so shape rules and their database lookups do not run on a delete.
  - The names are constants in `EntityRuleSets`.
- **Original and operation go through `RootContextData`**, read with `ctx.GetOriginal()` and `ctx.GetOperation()`, extensions on `ValidationContext<T>`. `RootContextData` also reaches child validators.
- **Only `Severity.Error` blocks the save.** Each such failure becomes `AddError(PropertyName, ErrorMessage)`. Warnings and info are dropped, because the response has no place for them.

```csharp
public class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator(AppDbContext db)
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);

        RuleFor(x => x.CustomerId)
            .MustAsync((id, ct) => db.Customers.AnyAsync(c => c.Id == id, ct))
            .WithMessage(x => $"Customer {x.CustomerId} does not exist");

        // a Related() collection is validated through its parent
        RuleForEach(x => x.Lines).ChildRules(line => line.RuleFor(l => l.Quantity).GreaterThan(0));

        RuleFor(x => x.Status)
            .Must((order, status, ctx) => ctx.GetOriginal() is not { } stored || OrderStatusRules.CanMove(stored.Status, status))
            .WithMessage("Status change not allowed");

        RuleSet(EntityRuleSets.Remove, () =>
            RuleFor(x => x.Status).NotEqual(OrderStatus.Shipped).WithMessage("A shipped order cannot be deleted"));
    }
}
```

## Implementation steps

### Step 0: tests first

Add `tests/Entities.Testing/ValidatorTests.cs`, on in-memory SQLite like `PrepperTests`. The tests cover:

- A validator sees the value a prepper computed. The fixture registers a global prepper and a `Related()` sync both before and after the validator.
- The errors of two validators arrive in one `EntityInputException<TEntity>`.
- Scope, one test per row of the scope table:
  - a validator scoped to the concrete type runs for that entity only;
  - one scoped to a base class runs for a derived entity saved through the base type's service, next to a validator scoped to the derived type;
  - one scoped to an interface runs for every entity that implements it, whichever `For<>()` registered it.
- The same validator class registered in two `For<>()` blocks runs once. Two delegates registered for one entity both run.
- `CanValidate` returning `false` skips the validator.
- A global validator's failure is an `EntityInputException<Order>`, not one of the interface type.
- A custom service that calls the `ValidateItem` extension gets the same errors as `EntityWriteService`.
- A failed `Add` or `Modify` leaves the root untracked.
- `Original` is `null` on `Add` and holds the stored row on `Modify`.
- `Remove` runs validators with `Operation = Remove`, including for an `IArchivable` soft delete.
- A failed `Remove` leaves nothing marked `Deleted`, including an attachment's principal.
- A subclass on the old four-parameter constructor compiles and runs without validators.

In `tests/Entities.Web.Testing`, add tests that:

- `POST`, `PUT`, `PATCH` and `DELETE` answer 400 with the error map;
- a key with two messages carries both;
- `DELETE` answers 400 without `MapEntityExceptions()`.

`tests/Entities.Testing/StartupValidationTests.cs` gains cases for both new startup warnings. The existing global filter tests must stay green once `GlobalFilterScope` reads its scope types from `EntityScopeTypes`.

### Step 1: `Regira.Entities`

- The validator types, `EntityInputError`, and `Errors` on `EntityInputException`.
- `EntityScopeTypes` and the `ValidateItem` extension, with the per-pair match cache.

### Step 2: `Regira.Entities.EFcore`

- The constructor pair, `ValidateItem`, and the stage in `Add`, `Modify` and `Remove`.
- The `RemoveItem` split, with `EntityAttachmentWriteService` overriding `RemoveItem`.
- The delegate validators.
- `GlobalFilterScope.EntityScopeTypes` delegates to `EntityScopeTypes.Of`, with no change in behaviour.

### Step 3: `Regira.Entities.DependencyInjection`

The registration extensions (class registrations through `TryAddEnumerable`), the builder methods and the two startup warnings.

### Step 4: `Regira.Entities.Web`

The `ModelState` helper at the three catch sites, and the `EntityInputException` catch in `Delete`.

### Step 5: the adapter package

- **Project.** Target `net8.0;net10.0` like the rest of the family, and pick a FluentValidation major that supports both.
- **Tests.** Add `FluentEntityValidatorTests` to `tests/Entities.Testing`, with a project reference to the adapter. They cover:
  - an async rule against the `DbContext`;
  - `GetOriginal()` on `Modify`;
  - the rule set that runs for each operation;
  - an `AbstractValidator<IHasTenantId>` runs for every entity that implements the interface, and an `AbstractValidator<Party>` runs for a `Person`;
  - a warning that does not block;
  - child keys such as `Lines[0].Quantity`;
  - an entity without a validator, which must be a no-op.

### Step 6: documentation

Each explanation gets one home per layer. Everything else links to it.

- **AI guides**, in `src/Common.Entities/ai/`:
  - `entities.instructions.md`:
    - Step 8 covers validators beside preppers, so no step is renumbered and no `§Step` link breaks.
    - Row 8 of the optional-steps table (line 219) hands "validate a required FK exists" to validators.
    - The write-pipeline table (line 502) gains a validators row after the preppers, and the stage numbers in the paragraph below it ("Only stages 4 and 5") move with it.
    - The "No prepper runs on `DELETE`" sentence (line 509) points delete rules at a `Remove` validator.
    - The "Validation" item under the wrapping service (line 884) goes.
    - The Global Services section (line 899) lists validators and states the scope rule. It includes the trap that the place of registration never narrows a validator's scope.
  - `entities.signatures.md`, `entities.namespaces.md` and `src/Entities.EFcore/ai/entities.efcore.namespaces.md` list the new types and namespaces.
  - `entities.card.md` gets one line.
  - In `entities.patterns.md`, the batching note (line 20) names validators next to preppers. A FluentValidation recipe is added.
  - The adapter is documented in the hub guide with its registration call, per AGENTS.md. The routing tables in `ai/AGENTS.md:190` and `src/Common.Setup/ai/copilot-instructions.md:100` list the package.
  - Every new snippet compiles under `tools/GuideVerifier`. The adapter project joins the Entities snippet group in `projects.json`.
- **Developer docs.**
  - `built-in-features.md` gains a Validators section beside Preppers (line 329), and "Validation" leaves the wrapping-service list (line 14).
  - `services.md` covers validators beside Entity Preppers (line 173).
  - `checklist.md` gains the item beside "Add Preppers" (line 54).
- **`@regira/modules`** reads the flat error map. A key can now hold more than one message, so check that its form-error display shows them all. Any front-end guide change ships with the MCP knowledge rebuild.
- **`ai/learnings.md`**, only if the implementation turns up a lasting lesson.

### Step 7: changelog and versions

Add one `CHANGELOG.md` bullet per changed package under `## Unreleased`. New types, registrations and an extension point make this a minor of the Entities family line under AGENTS.md. The adapter package starts on that same number. The final numbers are the maintainer's call at implementation time.

## Open questions

1. **`Remove` in the first version.** Recommendation: yes. It is the gap that exists today, and the `RemoveItem` split is needed for it anyway. A broken delete rule answers 400 with the error map, like a save. A 409 would need an exception type of its own.
2. **Child rows from `Related()`.** Recommendation: in the first version, a parent's validator covers its children (FluentValidation's `RuleForEach` does this), and the guide says so.
   - A later version could add `builder.Validate(...)` on `RelatedEntityBuilder`, for each incoming row, with keys prefixed as `Lines[i].`.
   - These validators could not run inside the sync, where nested preppers run today (`RelatedCollectionPrepper.cs:47`–`74`). The sync would have to collect them for the validation stage, or they would lose the guarantee of running after every prepper.
3. **Several messages per key.** Recommendation: the new `Errors` member described above. The fallback joins the messages into `InputErrors` with no public change, but the client then sees `["a b"]` instead of `["a", "b"]`.
4. **Stop after the first failing validator?** Recommendation: run all of them. The point is one 400 with everything in it. A validator can read `context.Errors` to skip an expensive check.
5. **Match the runtime type or the declared type?** Recommendation: the runtime type, like primers. It is the only choice under which a `Person` saved through the `Party` service gets its `Person` rules. The cost is a difference from preppers, which match the declared `TEntity`. The guide has to state it once, in the scope table.
6. **Names.** `IEntityValidator` clashes with nothing. The startup validators, however, already own `Regira.Entities.DependencyInjection.Validation.EntityValidationContext`, which is why the new context is called `IEntityValidatorContext`. The guides must say "input validation" and "startup validation" consistently. `EntityWriteOperation`, `EntityInputError`, `EntityScopeTypes`, `CanValidate`, `EntityRuleSets`, `FluentEntityValidator` and `RemoveItem` are working names. `EntityScope` is avoided because `GlobalFilterSelector` already has a private method with that name.
7. **The adapter's license.** The Mapster and AutoMapper adapters are commercially licensed (`licensing.md:10`), while the interface lives in the Apache-2.0 `Regira.Entities`. This is the maintainer's call. If the adapter is commercial, the package list in `licensing.md` changes with it.

## Out of scope

- **Validating input DTOs at the controller.** DataAnnotations on `TInputDto` keep producing ASP.NET's own 400. FluentValidation on DTOs stays the consumer's choice.
- **Writes through the raw `DbContext`.** Only primers see those. This proposal adds no validating primer.
- **Normalizing before validation.** Normalizers run in the `SaveChanges` interceptor (`EntityNormalizerContainerInterceptor`), after the validators. A rule about a normalized value normalizes it itself.
- **Batch validation.** Validators run once per item, like preppers (`entities.patterns.md:20`). A `ValidateMany` shape waits until a consumer needs it.
- **A validate-only (dry run) endpoint.** `ValidateItem` makes one possible later.
- **Moving preppers and primers onto `EntityScopeTypes`.** For the usual shapes they already reach the same answer through `IsMatch`. Unifying the three families' matching is a change of its own.
- **Choosing one validator per family**, as global filters do for their key variants. Validators only add errors, so there is nothing to choose between.

## Blast radius

| Package | Change | Consumer impact |
| --- | --- | --- |
| `Regira.Entities` | new validator types, `EntityInputError`, `EntityInputException.Errors`, `EntityScopeTypes`, the `ValidateItem` extension | none |
| `Regira.Entities.EFcore` | the validation stage in `Add`, `Modify` and `Remove`; a new constructor beside the old one; `Remove` split into `RemoveItem`, which the attachment write service overrides; `GlobalFilterScope` reads its scope types from `EntityScopeTypes` | none while no validator is registered, and global filters behave as before. A subclass on the old constructor runs no validators, and the startup warning says so. A subclass that overrides `Remove` without calling base skips validation, just as an `Add` override without base skips the preppers today. A literal `null` as the fourth constructor argument needs a name |
| `Regira.Entities.DependencyInjection` | registration methods, class registrations deduplicated through `TryAddEnumerable`, two startup warnings | none |
| `Regira.Entities.Web` | one `ModelState` helper; `DELETE` catches `EntityInputException` | `DELETE` can answer 400. A key in the 400 map can carry more than one message |
| `Regira.Entities.Validation.FluentValidation` | new package | opt-in |
| `Regira.Entities` guides | step 6 | guide changes ship with the family's minor |
| `@regira/modules` | none expected | check that a key with several messages displays them all |
