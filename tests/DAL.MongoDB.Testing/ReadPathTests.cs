using MongoDB.Bson;
using Regira.DAL.MongoDB.Core;
using Regira.Serializing.Newtonsoft.Json;

namespace DAL.MongoDB.Testing;

// The document a repository reads back is the one Save stores: the serializer's JSON, parsed into BSON.
// No server is needed — the communicator connects on first use, and these tests never use it.
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ReadPathTests
{
    private readonly JsonSerializer _serializer = new();
    private ProductRepository CreateRepository() => new(new MongoCommunicator(new MongoSettings("localhost", "unused")), _serializer);

    private BsonDocument Stored(Product product)
    {
        var bson = BsonDocument.Parse(_serializer.Serialize(product));
        bson["_id"] = ObjectId.GenerateNewId();
        return bson;
    }

    [Test]
    public void Reads_Back_Numeric_Fields()
    {
        var product = new Product { Name = "Desk", Category = "furniture", Price = 12.5m, Stock = 3 };
        var stored = Stored(product);

        var read = CreateRepository().ToEntity(stored);

        Assert.That(read.Price, Is.EqualTo(12.5m));
        Assert.That(read.Stock, Is.EqualTo(3));
        Assert.That(read.Name, Is.EqualTo("Desk"));
        Assert.That(read.Id, Is.EqualTo(stored["_id"].ToString()));
    }

    [Test]
    public void Stores_Fields_Under_The_Serializers_Names()
    {
        var stored = Stored(new Product { Name = "Desk", Category = "furniture", Price = 12.5m });

        Assert.That(stored.Names, Is.SupersetOf(new[] { "name", "category", "price" }));
    }
}
