using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Keywords;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Testing;

// FilterQ over explicit fields is what makes ?q= functional for entities that do not implement
// IHasNormalizedContent. The predicate is composed as an expression tree, so every case here also pins
// that EF translates it to SQL rather than falling back to client evaluation.
[TestFixture]
public class MultiFieldKeywordSearchTests
{
    private SqliteConnection _connection = null!;
    private ContosoContext _db = null!;
    private readonly QKeywordHelper _qHelper = new();

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _db = new ContosoContext(new DbContextOptionsBuilder<ContosoContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        // normalized columns are populated explicitly: this fixture exercises the query, not the primers
        _db.Set<Student>().AddRange(
            new Student { GivenName = "John", NormalizedGivenName = "JOHN", LastName = "Doe", NormalizedLastName = "DOE" },
            new Student { GivenName = "Jane", NormalizedGivenName = "JANE", LastName = "Smith", NormalizedLastName = "SMITH" },
            // "Doe" as a given name, so a term can match either column; the last name deliberately shares no
            // substring with the other rows' terms
            new Student { GivenName = "Doe", NormalizedGivenName = "DOE", LastName = "Baker", NormalizedLastName = "BAKER" });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _connection.Close();
    }

    private Task<List<Student>> Search(string q)
        => _db.Set<Student>()
            .FilterQ(_qHelper.Parse(q), x => x.NormalizedGivenName, x => x.NormalizedLastName)
            .ToListAsync();

    [Test]
    public async Task Matches_A_Keyword_In_Any_Of_The_Given_Fields()
    {
        // "doe" is a last name on one row and a given name on another — both come back
        var results = await Search("doe");
        Assert.That(results.Select(x => x.LastName), Is.EquivalentTo(new[] { "Doe", "Baker" }));
    }

    [Test]
    public async Task Every_Keyword_Must_Match_Some_Field()
    {
        // AND across keywords, OR across fields: John Doe satisfies both terms across two different columns
        var results = await Search("john doe");
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].GivenName, Is.EqualTo("John"));
    }

    [Test]
    public async Task Keyword_Matching_No_Field_Excludes_The_Row()
    {
        Assert.That(await Search("john smith"), Is.Empty);
    }

    [Test]
    public async Task Blank_Query_Leaves_The_Query_Untouched()
    {
        Assert.That(await Search(null!), Has.Count.EqualTo(3));
        Assert.That(await Search(" "), Has.Count.EqualTo(3));
    }

    [Test]
    public async Task Single_Field_Search_Is_Supported()
    {
        var results = await _db.Set<Student>()
            .FilterQ(_qHelper.Parse("doe"), x => x.NormalizedLastName)
            .ToListAsync();

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].GivenName, Is.EqualTo("John"));
    }
}
