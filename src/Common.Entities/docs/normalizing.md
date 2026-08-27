# Normalizing Entity Properties

- Optional
- Facilitates searching
    - Removes diacritics (accents)
    - Removes special characters
    - Standardizes whitespace
    - Preserves case by default (`Transform = NoChanges`); opt into `ToLowerCase`/`ToUpperCase` via `NormalizeOptions.Transform` (recommended when your database collation is case-sensitive)
- Customizable
    - Format phone numbers

## Saving normalized properties

A normalized property is usually just a **joined string** (using a space), built by normalizing one or more **source properties**.

### Automated

- Use `[Normalized]` attribute on property to be normalized
- Define `SourceProperties` for the source properties
- Use `DefaultEntityNormalizer` to process `NormalizedAttribute` automatically

### Customized

- Implement interface `IEntityNormalizer`
- Derive from base class `EntityNormalizerBase`
- Derive from `DefaultEntityNormalizer` to extend default behavior

```csharp
// base class
public abstract class EntityNormalizerBase<T>(INormalizer? normalizer = null) : IEntityNormalizer<T>
{
    public virtual bool IsExclusive => false;

    public abstract Task HandleNormalize(T item, CancellationToken token = default);
    public virtual async Task HandleNormalizeMany(IEnumerable<T> items, CancellationToken token = default) {...;
}
```

*When `IsExclusive` is true, only **this normalizer** is executed for the entity type.
Otherwise, other compatible normalizers are also executed.*

### Re-normalizing when related data changes (timing)

`NormalizedContent` is computed when the entity is saved. When a parent folds in text from **related**
entities (e.g. a ticket whose searchable content includes its replies), re-normalize the parent whenever
that related data changes.

The catch: a child added in the *same* `SaveChanges` as the parent isn't committed yet, so a normalizer
that **queries the database** for it won't see it — the searchable text lags by one save. Three ways to
handle it, cheapest first:

- **Read the in-memory children.** Fold in the parent's loaded navigation collection (`ticket.Replies`)
  instead of querying the DB — children attached in the same object graph are already visible. One save,
  no extra wiring. Use when the children hang off the parent's navigation.
- **Consult the `ChangeTracker`.** Inject the `DbContext` into the normalizer (normalizers are resolved
  through normal DI) and read the pending siblings — `db.GetPendingEntries<Reply>()` (i.e.
  `db.ChangeTracker.Entries<Reply>()`) includes the `Added` rows of the in-flight save. One save; works
  even when the child isn't on the parent's navigation, as long as it's tracked in the same context.
- **Save twice (two-phase write).** Persist the child first, then re-stamp the parent so its normalizer
  re-runs against the now-committed child. The fallback when the normalizer must query the DB:

  ```csharp
  await replyService.Add(reply);
  await replyService.SaveChanges();      // phase 1: child is committed
  await ticketService.Modify(ticket);    // phase 2: re-attach + re-normalize the parent
  await ticketService.SaveChanges();     // normalizer now sees the committed reply
  ```

  (Bulk seeding uses the same two-phase shape: create parents → add children → re-stamp parents in a final pass.)

These fit simple-to-moderate denormalization. When the cross-entity logic gets genuinely complex, don't
force it into a normalizer — move it to a dedicated service that owns building the searchable text;
it's clearer and easier to test.

## Filtering using normalized properties

- Use `IQKeywordHelper` to normalize search keywords
- Use same `INormalizer` for saving and filtering (by default)

Sample from `FilterHasNormalizedContentQueryBuilder`
```csharp
    public IQueryable<IHasNormalizedContent> Build(IQueryable<IHasNormalizedContent> query, ISearchObject<TKey>? so)
    {
        if (!string.IsNullOrWhiteSpace(so?.Q))
        {
            var keywords = qHelper.Parse(so.Q);
            foreach (var q in keywords)
            {
                query = query.Where(x => EF.Functions.Like(x.NormalizedContent, q.QW));
            }
        }

        return query;
    }
```

## Architecture

### Services

1. **INormalizer** - Property-level normalization (string transformation)
2. **IObjectNormalizer** - Object-level normalization (processes properties with `[Normalized]` attribute)
3. **IEntityNormalizer** - Entity-level normalization (custom business logic)

### Normalized attribute

- `SourceProperty` - Single source property name
- `SourceProperties` - Array of source property names (content concatenated with space)
- `Recursive` - Process nested objects (class-level only, default: true)
- `Normalizer` - Custom normalizer type (must implement `INormalizer` or `IObjectNormalizer`)

```csharp
// Normalize from multiple properties (concatenated with space)
[Normalized(SourceProperties = [nameof(Title), nameof(Description)])]
public string? NormalizedContent { get; set; }
```

## Dependency Injection

### Auto retrieve normalizers

Normalizers run as SaveChanges interceptors. `UseEntities<TContext>(e => e.UseDefaults())` wires the
`EntityNormalizerContainerInterceptor` into the DbContext options automatically; without `UseDefaults()`,
select it explicitly:
```csharp
services.UseEntities<MyDbContext>(e => e.WireDbContext(DbContextWiring.NormalizerInterceptors));
```
*The interceptor resolves all matching normalizers when saving entities.*

### Default services

| Interface | Implementation |
|-----------|----------------|
| `INormalizer` | `DefaultNormalizer` |
| `IQKeywordHelper` | `QKeywordHelper` |
| `IObjectNormalizer` | `ObjectNormalizer` |
| `IEntityNormalizer` | `DefaultEntityNormalizer<IEntity>` |

```csharp
services.UseEntities<DbContext>(e =>
{
    // Registers all default (normalizing) services
    e.AddDefaultEntityNormalizer();
    // or e.UseDefaults(); to also register other default helper services
});
```

### Globally

```csharp
services.UseEntities<DbContext>(e =>
{
    e.AddNormalizer<IEntityInterface, MyGlobalEntityNormalizer>();
});
```

### Per Entity

```csharp
services
    .UseEntities<DbContext>(/*...*/)
    .For<Entity>(entity =>
    {
        entity.AddNormalizer<MyEntityNormalizer>();
    });
```

## Overview

1. [Index](../README.md) — Overview of Regira Entities
1. [Entity Models](models.md) — Creating and structuring entity models
1. [Services](services.md) — Implementing entity services and repositories
1. [Mapping](mapping.md) — Mapping Entities to and from DTOs
1. [Web Endpoints](web-endpoints.md) — Exposing entity operations as HTTP endpoints
1. **[Normalizing](normalizing.md)** — Data normalization techniques
1. [Attachments](attachments.md) — Managing file attachments
1. [Built-in Features](built-in-features.md) — Ready to use components
1. [Checklist](checklist.md) — Step-by-step guide for common tasks
1. [Practical Examples](examples.md) — Complete implementation examples
