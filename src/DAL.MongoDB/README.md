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
var settings = MongoSettings.FromConnectionString("mongodb://user:pass@host:27017/mydb");
```

## MongoCommunicator

```csharp
var comm = new MongoCommunicator(settings);
// Access underlying IMongoDatabase via comm.Database
var names = comm.ListCollectionNames();   // IAsyncEnumerable<string>
```

## MongoDbRepositoryBase\<TEntity\>

Extend this to build a repository. Override `GetFilter()`, `SortResult()`, and `PageResult()` for custom queries.

```csharp
public class ProductRepository(MongoCommunicator comm)
    : MongoDbRepositoryBase<Product>(comm)
{
    protected override FilterDefinition<Product> GetFilter(object? so) { … }
}
```

CRUD methods:

```csharp
Task<TEntity?>             Details(object id)
Task<IEnumerable<TEntity>> List(object? searchObject)
Task<int>                  Count(object? searchObject)
Task                       Save(TEntity item)
Task                       Delete(TEntity item)
```

## MongoBackupService / MongoRestoreService

Requires `mongodump` / `mongorestore` executables in `MongoOptions.ToolsDirectory`.

```csharp
var svc = new MongoBackupService(new MongoOptions
{
    DbSettings     = settings,
    ToolsDirectory = "/usr/bin"
});
IMemoryFile backup = await svc.Backup();
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
