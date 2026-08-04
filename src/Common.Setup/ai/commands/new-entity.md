Scaffold a complete Regira entity with full CRUD. Read the required guides before generating any code.

## Step 1 — Load guides (mandatory)

Before writing a single line of code, read these files in full from `.regira/instructions/`:
- `entities.instructions.md`
- `entities.signatures.md`

If they do not exist, run `dotnet build` to trigger extraction from the installed `Regira.Entities` package.

## Step 2 — Gather entity details

Ask the user:
1. Entity name (e.g. `Product`)
2. Primary key type (`int`, `long`, `Guid`, `string`)
3. Which optional interfaces apply — work through this checklist from `entities.instructions.md`:
   - Does it have a name/title? → `INamedEntity`
   - Does it have a code/reference? → `ICodedEntity`
   - Does it need created/modified timestamps? → `IHasTimestamps`
   - Does it need soft delete? → `ISoftDeletable`
   - Does it have file attachments? → `IHasAttachments`
   - Is it part of a tree structure? → `ITreeEntity`
4. What properties does it have?
5. Does it belong to a parent entity (master-detail)?
6. What relationships does it have (one-to-many, many-to-many)?

## Step 3 — Generate the entity layer

Produce all of the following, in this order:

1. **Entity model class** — implement `IEntity<TKey>` plus applicable optional interfaces
2. **`SearchObject`** — extend `EntitySearchObject` with entity-specific filter properties
3. **`SortBy` enum** — extend `EntitySortBy` with entity-specific sort fields
4. **`Includes` enum** (if relationships exist)
5. **DTO classes** — `{Entity}Dto` and `{Entity}ShortDto` if different
6. **DbSet addition** — the `DbSet<{Entity}>` line in the `DbContext`
7. **EF Core configuration** — fluent API or data annotations as appropriate
8. **Controller** — inheriting from the correct Regira controller base class per `entities.signatures.md`

## Step 4 — Checkpoint before DI registration

Stop here. Present the generated code for review.

Ask: **"Does the entity look correct? Ready to add the DI registration?"**

## Step 5 — DI registration

Only after confirmation, generate the `IServiceCollection` extension method that registers the entity's services, repository, and controller mapping using the patterns in `entities.instructions.md`.

## Rules

- Never guess a namespace, interface, or method name — look it up in `entities.signatures.md`
- Follow the exact generic type naming from `entities.instructions.md` (`TEntity`, `TKey`, `TDto`, etc.)
- Do not introduce abstractions or patterns not present in the guides
