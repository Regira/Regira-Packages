# Entities.Mediator.Testing

xUnit tests for [Entities.Mediator](../../src/Entities.Mediator/README.md): the in-house sender (default and registered
handlers, behaviour order, `Duration`), the input check of a save and a patch, and the guide's recipe for dispatching
through MediatR (`MediatorRecipeTests`), from the generated endpoints and from a job. The HTTP tests host `ProductsController` on a test server; every test
gets its own in-memory SQLite database.

## Running

```bash
dotnet test tests/Entities.Mediator.Testing
```

No external requirements. MediatR is a test dependency only, for the recipe.
