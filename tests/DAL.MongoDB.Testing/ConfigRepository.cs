using Regira.DAL.MongoDB.Abstractions;
using Regira.DAL.MongoDB.Core;
using Regira.Serializing.Abstractions;

namespace DAL.MongoDB.Testing;

public class Config
{
    public string? ConfigId { get; set; }
    public string? Key { get; set; }
    public object? Value { get; set; }
}
public class ConfigRepository(MongoCommunicator communicator, ISerializer serializer)
    : MongoDbRepositoryBase<Config>(communicator, serializer, x => x.ConfigId, (x, id) => x.ConfigId = id, "config");