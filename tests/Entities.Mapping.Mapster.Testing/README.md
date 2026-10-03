# Entities.Mapping.Mapster.Testing

NUnit tests for [Entities.Mapping.Mapster](../../src/Entities.Mapping.Mapster/README.md): two
`UseEntities<TContext>()` stacks that each call `UseMapsterMapping()` share one `TypeAdapterConfig`, so both
contexts' entity ↔ DTO mappings work whichever stack is registered first.

## Running

```bash
dotnet test tests/Entities.Mapping.Mapster.Testing
```

No external requirements; the contexts use in-memory SQLite.
