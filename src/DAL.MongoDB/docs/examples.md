# Regira DAL.MongoDB — Examples

## Example 1: Connect to MongoDB and run a query

```csharp
var settings = MongoSettings.FromConnectionString(
    configuration.GetConnectionString("MongoDB")!);

var comm   = new MongoCommunicator(settings);
var repo   = new ProductRepository(comm);

var products = await repo.List(new ProductSearchObject { Category = "electronics" });
```

---

## Example 2: Custom MongoDB repository

```csharp
public class ProductRepository(MongoCommunicator comm)
    : MongoDbRepositoryBase<Product>(comm)
{
    protected override FilterDefinition<Product> GetFilter(object? so)
    {
        var filter = Builders<Product>.Filter.Empty;

        if (so is ProductSearchObject search)
        {
            if (!string.IsNullOrEmpty(search.Category))
                filter &= Builders<Product>.Filter.Eq(p => p.Category, search.Category);

            if (search.MinPrice.HasValue)
                filter &= Builders<Product>.Filter.Gte(p => p.Price, search.MinPrice.Value);
        }

        return filter;
    }

    protected override IFindFluent<Product, Product> SortResult(
        IFindFluent<Product, Product> query, object? so)
        => query.SortBy(p => p.Name);
}
```

---

## Example 3: Backup a MongoDB database

```csharp
var options = new MongoOptions
{
    DbSettings     = new MongoSettings("mongo.example.com", "prod-db", username: "admin", password: "pass"),
    ToolsDirectory = "/usr/bin"
};

IMemoryFile backup = await new MongoBackupService(options).Backup();

// Store the backup via IO.Storage
await fileService.Save($"backups/{DateTime.Today:yyyyMMdd}.archive", backup.Bytes!);
```

---

## Overview

1. [Index](../README.md) — Settings, communicator, repository, and backup/restore
1. **[Examples](examples.md)** — Connect, query, and backup
