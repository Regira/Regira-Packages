using Regira.DAL.MongoDB.Abstractions;
using Regira.DAL.MongoDB.Core;
using Regira.Serializing.Abstractions;

namespace DAL.MongoDB.Testing;

public class Person
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public DateTime? BirthDate { get; set; }
}

public class PersonRepository(MongoCommunicator communicator, ISerializer serializer)
    : MongoDbRepositoryBase<Person>(communicator, serializer, p => p.Id, (p, id) => p.Id = id, "persons");