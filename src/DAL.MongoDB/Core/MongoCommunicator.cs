using MongoDB.Driver;

namespace Regira.DAL.MongoDB.Core;

public class MongoCommunicator(MongoSettings settings)
{
    private readonly string? _database = settings.DatabaseName;
    protected MongoClientSettings Settings { get; } = settings.ToMongoClientSettings();
    private IMongoClient? _client;
    protected IMongoClient Client => _client ??= new MongoClient(Settings);
    private static IMongoDatabase? _dbContext;
    protected internal IMongoDatabase Database => _dbContext ??= Client.GetDatabase(_database);


    public async IAsyncEnumerable<string> ListCollectionNames()
    {
        var names = await Database.ListCollectionNamesAsync();
        while (await names.MoveNextAsync())
        {
            foreach (var name in names.Current)
            {
                yield return name;
            }
        }
    }
}