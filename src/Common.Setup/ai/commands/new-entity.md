Scaffold a complete Regira entity with full CRUD. Read the required guides before generating any code.

## Step 1 — Load guides (mandatory)

Before writing a single line of code, read these files in full from `.regira/instructions/`:
- `entities.instructions.md`
- `entities.signatures.md`
- `entities.setup.md` — §Project Structure only: it says whether the solution is a single project or a layered one, which decides where every file below goes

If they do not exist, run `dotnet build` to trigger extraction from the installed `Regira.Entities` package.

## Step 2 — Gather entity details

Ask the user:
1. Entity name (e.g. `Product`)
2. Primary key type (`int`, `long`, `Guid`, `string`)
3. Which optional interfaces apply — work through the *Interface selection checklist* in `entities.instructions.md` (Step 1), for example:
   - Does it have a short display name? → `IHasTitle`
   - Does it have a short unique code? → `IHasCode`
   - Does it need created/modified timestamps? → `IHasTimestamps`
   - Does it need soft delete? → `IArchivable`
   - Does it have file attachments? → `IHasAttachments`
   - Is it self-referencing (a parent/children tree)? → no interface: `entities.patterns.md` → Hierarchical Data (Self-referencing)
4. What properties does it have?
5. Does it belong to a parent entity (master-detail)?
6. What relationships does it have (one-to-many, many-to-many)?

## Step 3 — Generate the entity layer

Produce all of the following, in this order:

**Placement:** put each file where `entities.setup.md` §Project Structure puts it for the layout the solution already has — single project: `Entities/{Plural}/` for 1–5, `Data/{App}DbContext.cs` for 6–7, `Controllers/` for 8; layered solution: the same files split by layer, `{App}.Models/{Domain}/{Plural}/` for 1–5, `{App}.Data/{App}DbContext.cs` for 6–7, the host's `Controllers/` for 8. Do not introduce the other layout.

1. **Entity model class** — implement `IEntityWithSerial` (an `int` key) or `IEntity<TKey>` plus applicable optional interfaces
2. **`{Entity}SearchObject`** — inherit from `SearchObject` (or `SearchObject<TKey>`) and add only entity-specific filter properties
3. **`{Entity}SortBy` enum** — a plain (non-`[Flags]`) enum of your own, used in place of the default `EntitySortBy` (only on a complex registration)
4. **`{Entity}Includes` enum** — a `[Flags]` enum, used in place of the default `EntityIncludes` (only on a complex registration, when relationships exist)
5. **DTO classes** — `{Entity}Dto` (read) and `{Entity}InputDto` (write)
6. **DbSet addition** — the `DbSet<{Entity}>` line in the `DbContext`
7. **EF Core configuration** — fluent API or data annotations as appropriate
8. **Controller** — inheriting from the correct Regira controller base class per `entities.signatures.md`. Skip it when the app maps its entities with `app.MapEntityEndpoints()` instead of controllers: the registration then declares the DTO pair (`e.UseMapping<{Entity}Dto, {Entity}InputDto>()`), and the entity's endpoints come with it (`entities.patterns.md` → Mapped entity endpoints)

## Step 4 — Checkpoint before DI registration

Stop here. Present the generated code for review.

Ask: **"Does the entity look correct? Ready to add the DI registration?"**

## Step 5 — DI registration

Only after confirmation, generate the `IServiceCollection` extension method that registers the entity's services, repository, and controller mapping using the patterns in `entities.instructions.md`. In a layered solution that method is `{App}.DependencyInjection/{Domain}/{Entity}ServiceConfiguration.cs`, chained from the domain aggregator (`Add{Domain}()`) rather than from the root; any query builder, normalizer or manager it registers goes in `{App}.Services/{Domain}/{Plural}/`.

## Rules

- Never guess a namespace, interface, or method name — look it up in `entities.signatures.md`
- Follow the exact generic type naming from `entities.instructions.md` (`TEntity`, `TKey`, `TDto`, etc.)
- Do not introduce abstractions or patterns not present in the guides
