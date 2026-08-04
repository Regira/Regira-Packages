using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using Regira.DAL.MongoDB.Core;
using Regira.Serializing.Abstractions;
using Regira.Utilities;

namespace Regira.DAL.MongoDB.Abstractions;

public abstract class MongoDbRepositoryBase<TEntity>(
    MongoCommunicator communicator,
    ISerializer serializer,
    Func<TEntity, string?> getIdFunc,
    Action<TEntity, string> setIdAction,
    string? collectionName = null)
    where TEntity : class, new()
{
    protected readonly ISerializer Serializer = serializer;
    private readonly string _collectionName = collectionName ?? typeof(TEntity).FullName!;
    protected internal IMongoDatabase Database => communicator.Database;
    private IMongoCollection<BsonDocument>? _collection;
    protected internal IMongoCollection<BsonDocument> Collection => _collection ??= Database.GetCollection<BsonDocument>(_collectionName);


    public async Task<TEntity?> Details(object o)
    {
        return (await List(new { id = o })).SingleOrDefault();
    }
    public async Task<IEnumerable<TEntity>> List(object? o = null)
    {
        var so = GetSearchObject(o);
        var filter = GetFilter(so);

        // ToDo: call async
        var result = Collection.Find(filter);
        result = SortResult(result, so);
        result = PageResult(result, so);

        var documents = result.ToList();
        var list = documents.ConvertAll(x => Convert(x));

        // ToDo: call Collection.FindAsync (see up)
        return await Task.FromResult(list);
    }
    public virtual Task<long> Count(object? o = null)
    {
        var filter = FilterDefinition<BsonDocument>.Empty;
        return Collection.CountDocumentsAsync(filter);
    }

    public virtual async Task<long> Save(TEntity item)
    {
        var content = Serializer.Serialize(item);
        var bson = BsonDocument.Parse(content);

        var itemId = getIdFunc(item);
        if (itemId == null)
        {
            await Collection.InsertOneAsync(bson, new InsertOneOptions());
            setIdAction(item, bson["_id"].ToString()!);
            return 1;
        }

        bson.Remove("id");
        bson.Remove("Id");
        bson["_id"] = itemId;

        var original = await Details(itemId);
        if (original == null)
        {
            await Collection.InsertOneAsync(bson, new InsertOneOptions());
            return 1;
        }

        var filter = GetFilter(DictionaryUtility.ToDictionary(new { id = itemId }));
        bson.Remove("_id");
        var result = await Collection.ReplaceOneAsync(filter, bson, new ReplaceOptions());

        return result.ModifiedCount;
    }
    public virtual async Task<long> Delete(TEntity item)
    {
        var itemId = getIdFunc(item);
        var filter = GetFilter(DictionaryUtility.ToDictionary(new { id = itemId }));
        var result = await Collection.DeleteOneAsync(filter, new DeleteOptions());

        return result.DeletedCount;
    }

    protected internal virtual TEntity Convert(BsonDocument bson)
    {
        var content = bson.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.CanonicalExtendedJson });
        var item = Serializer.Deserialize<TEntity>(content)!;
        setIdAction(item, bson["_id"].ToString()!);
        return item;
    }
    protected internal virtual IDictionary<string, object?> GetSearchObject(object? so)
    {
        return DictionaryUtility.ToDictionary(so is string ? new[] { new KeyValuePair<string, object?>("id", so) } : so);
    }
    protected internal virtual FilterDefinition<BsonDocument> GetFilter(IDictionary<string, object?>? so)
    {
        var filter = FilterDefinition<BsonDocument>.Empty;
        if (so?.Any() != true)
        {
            return filter;
        }

        if (so.TryGetValue("Id", out var id) && id != null)
        {

            if (ObjectId.TryParse(id.ToString(), out ObjectId bsonId))
            {
                filter &= new BsonDocument { ["_id"] = bsonId };
            }
            else
            {
                filter &= new BsonDocument { ["_id"] = id.ToString() };
            }
        }

        return filter;
    }
    protected internal virtual IFindFluent<BsonDocument, BsonDocument> SortResult(IFindFluent<BsonDocument, BsonDocument> result, IDictionary<string, object?> so)
    {
        if (so.ContainsKey("sort"))
        {
            //result = result.Sort(new BsonDocumentSortDefinition<BsonDocument>(new BsonDocument()));
        }

        return result;
    }
    protected internal virtual IFindFluent<BsonDocument, BsonDocument> PageResult(IFindFluent<BsonDocument, BsonDocument> result, IDictionary<string, object?> so)
    {
        so.TryGetValue("page", out int page);
        so.TryGetValue("pageSize", out int pageSize);

        if (pageSize > 0)
        {
            if (page > 0)
            {
                result = result.Skip(page * pageSize);
            }

            result = result.Limit(pageSize);
        }

        return result;
    }
}