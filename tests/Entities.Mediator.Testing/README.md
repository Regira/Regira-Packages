# Entities.Mediator.Testing

xUnit tests for [Entities.Mediator](../../src/Entities.Mediator/README.md) and
[Entities.Mediator.MediatR](../../src/Entities.Mediator.MediatR/README.md): the executor (default and registered
handlers, behaviour order, `Duration`, untyped execution), the input check of a save and a patch, and MediatR dispatch
from the generated endpoints and from a job. The HTTP tests host `ProductsController` on a test server; every test
gets its own in-memory SQLite database.

## Running

```bash
dotnet test tests/Entities.Mediator.Testing
```

No external requirements. The tests run on the latest MediatR by default; the adapter references MediatR 12.0.0 as
its floor, and the same tests run on it with:

```bash
dotnet test tests/Entities.Mediator.Testing -p:MediatRTestVersion=12.0.0
```
