using MongoDB.Driver;
using NUnit.Framework.Legacy;
using Regira.DAL.MongoDB.Core;
using Regira.Serializing.Newtonsoft.Json;
using Regira.Utilities;

namespace DAL.MongoDB.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
// Connects to a MongoDB on localhost. The other fixtures in this assembly parse connection strings
// and stub the process helper, so only this one needs a server.
[Category("MongoDb")]
public class RepositoryTests : IDisposable
{
    //private readonly string _personId;
    private readonly MongoCommunicator _mongoCommunicator;
    private readonly PersonRepository _personRepo;
    private readonly ConfigRepository _configRepo;
    private readonly ProductRepository _productRepo;
    private readonly MongoSettings _mongoSettings;
    public RepositoryTests()
    {
        //_personId = "test-person";
        var serializer = new JsonSerializer();
        _mongoSettings = new MongoSettings("localhost", $"Test-{Guid.NewGuid()}");
        _mongoCommunicator = new MongoCommunicator(_mongoSettings);
        _personRepo = new PersonRepository(_mongoCommunicator, serializer);
        _configRepo = new ConfigRepository(_mongoCommunicator, serializer);
        _productRepo = new ProductRepository(_mongoCommunicator, serializer);
    }

    [Test]
    public async Task TestMakeConnection()
    {
        var person = new Person { Title = "B.Verboven", BirthDate = new DateTime(1980, 5, 6) };
        await _personRepo.Save(person);

        var collections = await _mongoCommunicator.ListCollectionNames().ToListAsync();
        Assert.That(collections, Is.Not.Empty);

        _personRepo.Delete(person).Wait();
    }


    [Test]
    public async Task TestCreatePersonAutogenerateId()
    {
        var person = new Person { Title = "B.Verboven", BirthDate = new DateTime(1980, 5, 6) };
        var affected = await _personRepo.Save(person);
        Assert.That(affected, Is.EqualTo(1));
        ClassicAssert.IsNotNull(person.Id);

        await _personRepo.Delete(person);
    }
    [Test]
    public async Task TestCreatePersonWithId()
    {
        var person = new Person { Id = Guid.NewGuid().ToString(), Title = "B.Verboven", BirthDate = new DateTime(1980, 5, 6) };
        var affected = await _personRepo.Save(person);
        Assert.That(affected, Is.EqualTo(1));
        ClassicAssert.IsNotNull(person.Id);

        await _personRepo.Delete(person);
    }

    [Test]
    public async Task TestCreateConfig()
    {
        var item = new Config { ConfigId = "123456", Key = "MyKey", Value = "Testing" };
        var affected = await _configRepo.Save(item);
        Assert.That(affected, Is.EqualTo(1));

        await _configRepo.Delete(item);
    }

    [Test]
    public async Task TestListPersons()
    {
        var person = new Person { Title = "B.Verboven", BirthDate = new DateTime(1980, 5, 6) };
        await _personRepo.Save(person);

        var persons = (await _personRepo.List(new { person.Id })).AsList();
        Assert.That(persons, Is.Not.Empty);
        Assert.That(persons.Count, Is.EqualTo(1));

        await _personRepo.Delete(person);
    }
    [Test]
    public async Task TestGetPersonsWithGivenId()
    {
        var personId = Guid.NewGuid().ToString();
        var person = new Person { Id = personId, Title = "B.Verboven", BirthDate = new DateTime(1980, 5, 6) };
        await _personRepo.Save(person);

        var fetchedPerson = await _personRepo.Details(personId);
        ClassicAssert.IsNotNull(fetchedPerson);

        await _personRepo.Delete(fetchedPerson!);
    }

    [Test]
    public async Task UpdatePerson()
    {
        var person = new Person { Title = "B.Verboven", BirthDate = new DateTime(1980, 5, 6) };
        await _personRepo.Save(person);

        Assert.That(person.BirthDate, Is.EqualTo(new DateTime(1980, 5, 6)));
        // ReSharper disable once PossibleInvalidOperationException
        person.BirthDate = person.BirthDate.Value.AddYears(-1);
        var affected = await _personRepo.Save(person);
        Assert.That(affected, Is.EqualTo(1));
        var person2 = (await _personRepo.List(new { person.Id })).First();
        Assert.That(person2.BirthDate, Is.EqualTo(new DateTime(1979, 5, 6)));

        await _personRepo.Delete(person);
    }

    [Test]
    public async Task UpdatePersonWithGivenId()
    {
        var personId = Guid.NewGuid().ToString();
        var person = new Person { Id = personId, Title = "B.Verboven", BirthDate = new DateTime(1980, 5, 6) };
        await _personRepo.Save(person);

        Assert.That(person.BirthDate, Is.EqualTo(new DateTime(1980, 5, 6)));
        // ReSharper disable once PossibleInvalidOperationException
        person.BirthDate = person.BirthDate.Value.AddYears(-1);
        var affected = await _personRepo.Save(person);
        Assert.That(affected, Is.EqualTo(1));
        var person2 = await _personRepo.Details(personId);
        Assert.That(person2!.BirthDate, Is.EqualTo(new DateTime(1979, 5, 6)));

        await _personRepo.Delete(person);
    }

    [Test]
    public async Task DeletePerson()
    {
        var person = new Person { Title = "B.Verboven", BirthDate = new DateTime(1980, 5, 6) };
        await _personRepo.Save(person);

        var affected = await _personRepo.Delete(person);
        Assert.That(affected, Is.EqualTo(1));
        var persons = await _personRepo.List(new { person.Id });
        ClassicAssert.IsEmpty(persons);
    }
    [Test]
    public async Task DeletePersonWithGivenId()
    {
        var person = new Person { Id = Guid.NewGuid().ToString(), Title = "B.Verboven", BirthDate = new DateTime(1980, 5, 6) };
        await _personRepo.Save(person);

        var affected = await _personRepo.Delete(person);
        Assert.That(affected, Is.EqualTo(1));
        var persons = await _personRepo.List(new { person.Id });
        ClassicAssert.IsEmpty(persons);
    }

    [Test]
    public async Task ListProducts_Filters_And_Sorts_On_The_Stored_Field_Names()
    {
        var category = $"furniture-{Guid.NewGuid()}";
        Product[] products =
        [
            new() { Name = "Desk", Category = category, Price = 120.5m, Stock = 3 },
            new() { Name = "Chair", Category = category, Price = 45m, Stock = 12 },
            new() { Name = "Stool", Category = category, Price = 9.99m, Stock = 7 },
            new() { Name = "Lamp", Category = $"lighting-{Guid.NewGuid()}", Price = 30m, Stock = 1 }
        ];
        foreach (var product in products)
        {
            await _productRepo.Save(product);
        }

        var found = (await _productRepo.List(new ProductSearchObject { Category = category, MinPrice = 10 })).ToList();

        Assert.That(found.Select(p => p.Name), Is.EqualTo(new[] { "Chair", "Desk" }));
        Assert.That(found.Select(p => (p.Price, p.Stock)), Is.EqualTo(new[] { (45m, 12), (120.5m, 3) }));

        foreach (var product in products)
        {
            await _productRepo.Delete(product);
        }
    }

    [Test]
    public async Task Each_Communicator_Uses_Its_Own_Database()
    {
        var otherSettings = new MongoSettings("localhost", $"Test-{Guid.NewGuid()}");
        var otherRepo = new ProductRepository(new MongoCommunicator(otherSettings), new JsonSerializer());
        try
        {
            var category = $"furniture-{Guid.NewGuid()}";
            await _productRepo.Save(new Product { Name = "Desk", Category = category, Price = 120.5m });
            await otherRepo.Save(new Product { Name = "Chair", Category = category, Price = 45m });

            var here = await _productRepo.List(new ProductSearchObject { Category = category });
            var there = await otherRepo.List(new ProductSearchObject { Category = category });

            Assert.That(here.Select(p => p.Name), Is.EqualTo(new[] { "Desk" }));
            Assert.That(there.Select(p => p.Name), Is.EqualTo(new[] { "Chair" }));
        }
        finally
        {
            await new MongoClient(otherSettings.ToMongoClientSettings()).DropDatabaseAsync(otherSettings.DatabaseName);
        }
    }


    public void Dispose()
    {
        var client = new MongoClient(_mongoSettings.ToMongoClientSettings());
        client.DropDatabase(_mongoSettings.DatabaseName);
    }
}