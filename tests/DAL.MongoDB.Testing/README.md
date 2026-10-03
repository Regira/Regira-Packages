# DAL.MongoDB.Testing

NUnit tests for [DAL.MongoDB](../../src/DAL.MongoDB/README.md): connection-string building (`MongoSettings`), the
commands the backup and restore services compose (`mongodump`/`mongorestore` are replaced by a stand-in process
helper, so no MongoDB tools are needed), how a repository reads back the documents it stores, and repository CRUD
against a live server.

## Running

```bash
dotnet test tests/DAL.MongoDB.Testing
```

`RepositoryTests` (category `MongoDb`) connects to a MongoDB at `localhost:27017` without credentials, creates
`Test-{guid}` databases and drops them afterwards. It has no skip guard, so without a local server it fails. The other
fixtures need nothing. To leave it out:

```bash
dotnet test tests/DAL.MongoDB.Testing --filter "TestCategory!=MongoDb"
```
