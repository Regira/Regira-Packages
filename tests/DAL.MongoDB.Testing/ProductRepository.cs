using MongoDB.Bson;
using MongoDB.Driver;
using Regira.DAL.MongoDB.Abstractions;
using Regira.DAL.MongoDB.Core;
using Regira.Serializing.Abstractions;

namespace DAL.MongoDB.Testing;

public class Product
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Category { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

public class ProductSearchObject
{
    public string? Category { get; set; }
    public decimal? MinPrice { get; set; }
}

// The repository of the package's Examples page: fields are filtered and sorted by the names the serializer stores
public class ProductRepository(MongoCommunicator comm, ISerializer serializer)
    : MongoDbRepositoryBase<Product>(
        comm,
        serializer,
        getIdFunc: p => p.Id,
        setIdAction: (p, id) => p.Id = id,
        collectionName: "products")
{
    public Product ToEntity(BsonDocument bson) => Convert(bson);

    protected override FilterDefinition<BsonDocument> GetFilter(IDictionary<string, object?>? so)
    {
        var filter = base.GetFilter(so);   // keeps the Id filter

        if (so?.TryGetValue(nameof(ProductSearchObject.Category), out var category) == true && category != null)
            filter &= Builders<BsonDocument>.Filter.Eq("category", category.ToString());

        if (so?.TryGetValue(nameof(ProductSearchObject.MinPrice), out var minPrice) == true && minPrice != null)
            filter &= Builders<BsonDocument>.Filter.Gte("price", System.Convert.ToDecimal(minPrice));

        return filter;
    }

    protected override IFindFluent<BsonDocument, BsonDocument> SortResult(
        IFindFluent<BsonDocument, BsonDocument> result, IDictionary<string, object?> so)
        => result.Sort(Builders<BsonDocument>.Sort.Ascending("name"));
}
