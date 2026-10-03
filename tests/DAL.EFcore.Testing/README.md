# DAL.EFcore.Testing

NUnit tests for [DAL.EFcore](../../src/DAL.EFcore/README.md): the auto-normalizing and auto-truncate interceptors
and the entity-type extensions, run against the Contoso model from [Testing.Library](../Testing.Library/README.md).

## Running

```bash
dotnet test tests/DAL.EFcore.Testing
```

No external requirements: the tests use in-memory SQLite, and one auto-truncate test uses a uniquely named SQLite
file in the temp directory that it deletes afterwards.
