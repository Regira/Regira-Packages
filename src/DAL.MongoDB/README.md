# Regira DAL — MongoDB

Regira DAL.MongoDB provides lightweight MongoDB connectivity using the MongoDB Driver, plus backup/restore via `mongodump`/`mongorestore`.

## Projects

| Project | Package | Backend | CRUD | Backup / Restore |
|---------|---------|---------|------|-----------------|
| `DAL.MongoDB` | `Regira.DAL.MongoDB` | MongoDB | via MongoDB Driver | ✓ (mongodump) |

## Installation

```xml
<PackageReference Include="Regira.DAL.MongoDB" Version="6.*" />
```

---

## MongoSettings

| Property | Type | Description |
|----------|------|-------------|
| `Host` | `string` | Hostname / replica set |
| `DatabaseName` | `string` | Target database |
| `Port` | `string` | Port (default `27017`) |
| `Username` | `string?` | Auth username |
| `Password` | `string?` | Auth password |
| `UseSecure` (UseTls) | `bool` | TLS/SSL |

```csharp
var settings = new MongoSettings("localhost", "mydb");
// or parse from connection string:
settings = MongoSettings.FromConnectionString("mongodb://user:pass@host:27017/mydb");
```

## MongoCommunicator

```csharp
var settings = new MongoSettings("localhost", "mydb");
var comm = new MongoCommunicator(settings);
var names = comm.ListCollectionNames();   // IAsyncEnumerable<string>
```

The underlying `IMongoDatabase` (`Database`) is `protected internal` — it is not accessible on the communicator from consumer code, only from repository classes derived from `MongoDbRepositoryBase<TEntity>`.

## MongoDbRepositoryBase\<TEntity\>

Extend this to build a repository (`TEntity` must be a class with a parameterless constructor). The base constructor takes the communicator, an `ISerializer`, id accessor delegates, and an optional collection name. Override `GetFilter()`, `SortResult()`, and `PageResult()` for custom queries.

```csharp
public class Product
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}

public class ProductRepository(MongoCommunicator comm, ISerializer serializer)
    : MongoDbRepositoryBase<Product>(
        comm,
        serializer,
        getIdFunc: p => p.Id,
        setIdAction: (p, id) => p.Id = id,
        collectionName: "products")
{
    protected override FilterDefinition<BsonDocument> GetFilter(IDictionary<string, object?>? so)
    {
        var filter = base.GetFilter(so);
        if (so?.TryGetValue("name", out var name) == true && name != null)
        {
            filter &= Builders<BsonDocument>.Filter.Eq("Name", name.ToString());
        }
        return filter;
    }
}
```

CRUD methods:

```csharp no-compile
Task<TEntity?>             Details(object id)
Task<IEnumerable<TEntity>> List(object? searchObject = null)
Task<long>                 Count(object? searchObject = null)
Task<long>                 Save(TEntity item)     // inserted (1) or modified count
Task<long>                 Delete(TEntity item)   // deleted count
```

## MongoBackupService / MongoRestoreService

Requires `mongodump` / `mongorestore` executables in `MongoOptions.ToolsDirectory`. Both services also take an `IProcessHelper` (e.g. `ProcessHelper` from `Regira.System`) to run those executables.

```csharp
var settings = new MongoSettings("localhost", "mydb");
var options = new MongoOptions
{
    DbSettings     = settings,
    ToolsDirectory = "/usr/bin"
};
IProcessHelper processHelper = new ProcessHelper();

IMemoryFile backup = await new MongoBackupService(options, processHelper).Backup();
await new MongoRestoreService(options, processHelper).Restore(backup);
```

## Backup/Restore contracts

Both services implement the shared contracts from [Common](https://regira.github.io/Regira-Packages/src/Common#dal-abstractions):

```csharp
public interface IDbBackupService  { Task<IMemoryFile> Backup(); }
public interface IDbRestoreService { Task Restore(IMemoryFile file); }
```

## Overview

1. **[Index](https://regira.github.io/Regira-Packages/src/DAL.MongoDB/)** — Settings, communicator, repository, and backup/restore
1. [Examples](https://regira.github.io/Regira-Packages/src/DAL.MongoDB/docs/examples.html) — Connect, query, and backup

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
