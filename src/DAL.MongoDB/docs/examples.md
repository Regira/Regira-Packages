# Regira DAL.MongoDB — Examples

## Example 1: Connect to MongoDB and run a query

<!-- no-compile -->
```csharp
var settings = MongoSettings.FromConnectionString(
    configuration.GetConnectionString("MongoDB")!);

var comm   = new MongoCommunicator(settings);
var repo   = new ProductRepository(comm, serializer);   // serializer: an ISerializer, e.g. Regira.Serializing.Newtonsoft

var products = await repo.List(new ProductSearchObject { Category = "electronics" });
```

---

## Example 2: Custom MongoDB repository

`List(searchObject)` hands the overrides the search object as a dictionary keyed by its property names, and the
collection holds `BsonDocument`s, so the filter and the sort address fields by name.

```csharp
public class Product
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Category { get; set; }
    public decimal Price { get; set; }
}

public class ProductSearchObject
{
    public string? Category { get; set; }
    public decimal? MinPrice { get; set; }
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
        var filter = base.GetFilter(so);   // keeps the Id filter

        if (so?.TryGetValue(nameof(ProductSearchObject.Category), out var category) == true && category != null)
            filter &= Builders<BsonDocument>.Filter.Eq(nameof(Product.Category), category.ToString());

        if (so?.TryGetValue(nameof(ProductSearchObject.MinPrice), out var minPrice) == true && minPrice != null)
            filter &= Builders<BsonDocument>.Filter.Gte(nameof(Product.Price), System.Convert.ToDecimal(minPrice));

        return filter;
    }

    protected override IFindFluent<BsonDocument, BsonDocument> SortResult(
        IFindFluent<BsonDocument, BsonDocument> result, IDictionary<string, object?> so)
        => result.Sort(Builders<BsonDocument>.Sort.Ascending(nameof(Product.Name)));
}
```

---

## Example 3: Backup a MongoDB database

<!-- no-compile -->
```csharp
var options = new MongoOptions
{
    DbSettings     = new MongoSettings("mongo.example.com", "prod-db", username: "backup", password: "pass")
    {
        // omit when the credentials live in prod-db itself
        AuthenticationDatabase = "admin"
    },
    ToolsDirectory = "/usr/bin"
};

IMemoryFile backup = await new MongoBackupService(options, new ProcessHelper()).Backup();

// Store the backup via IO.Storage
await fileService.Save($"backups/{DateTime.Today:yyyyMMdd}.archive", backup.GetBytes()!);
```

The password never reaches `mongodump`'s command line — it goes into a temporary `--config` file that is deleted again once the dump has run.

---

## Overview

1. [Index](../README.md) — Settings, communicator, repository, and backup/restore
1. **[Examples](examples.md)** — Connect, query, and backup
