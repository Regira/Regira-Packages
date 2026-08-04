using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Regira.DAL.EFcore.Extensions;
using Regira.Normalizing;
using Regira.Normalizing.Models;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace DAL.EFcore.Testing;

[TestFixture]
internal class AutoNormalizingTests
{
    private Person John = null!;
    private Person Jane = null!;
    private Person Francois = null!;
    private SqliteConnection _connection = null!;
    private ContosoContext _dbContext = null!;
    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _dbContext = new ContosoContext(new DbContextOptionsBuilder<ContosoContext>().UseSqlite(_connection).Options);
        John = new Person
        {
            GivenName = "John",
            LastName = "Doe",
            Description = "This is a male test person",
            Email = "john.doe@email.com"
        };
        Jane = new Person
        {
            GivenName = "Jane",
            LastName = "Doe",
            Description = "This is a female test person",
            Phone = "001 234 567 890"
        };
        Francois = new Person
        {
            GivenName = "François",
            LastName = "Du sacre Cœur",
            Description = "Le poète parisien du xiiie siècle Rutebeuf se fait gravement l'écho de la faiblesse humaine, de l'incertitude et de la pauvreté à l'opposé des valeurs courtoises. Crème brûlée"
        };
    }
    [TearDown]
    public void TearDown()
    {
        _connection.Close();
        _dbContext.Dispose();
    }

    [Test]
    public void Default_NormalizingOptions()
    {
        _dbContext.Persons.AddRange(John, Jane, Francois);

        _dbContext.AutoNormalizeStringsForEntries();

        Assert.That(John.NormalizedGivenName, Is.EqualTo("John"));
        Assert.That(John.NormalizedLastName, Is.EqualTo("Doe"));
        Assert.That(John.NormalizedContent, Is.EqualTo($"{John.GivenName} {John.LastName} {John.Description} {John.Email}"));
    }
    [Test]
    public void To_UpperCase()
    {
        _dbContext.Persons.AddRange(John, Jane, Francois);

        _dbContext.AutoNormalizeStringsForEntries(new NormalizingOptions { Transform = TextTransform.ToUpperCase });

        Assert.That(John.NormalizedGivenName, Is.EqualTo("JOHN"));
        Assert.That(John.NormalizedLastName, Is.EqualTo("DOE"));
        Assert.That(John.NormalizedContent, Is.EqualTo($"{John.GivenName} {John.LastName} {John.Description} {John.Email}".ToUpper()));
    }
    [Test]
    public void Diacretics()
    {
        var normalizer = new DefaultNormalizer();
        _dbContext.Persons.AddRange(John, Jane, Francois);

        _dbContext.AutoNormalizeStringsForEntries();

        Assert.That(Francois.NormalizedGivenName, Is.EqualTo("Francois"));
        Assert.That(Francois.NormalizedLastName, Is.EqualTo("Du sacre Coeur"));
        Assert.That(Francois.NormalizedContent, Is.EqualTo(normalizer.Normalize("Francois Du sacre Coeur Le poete parisien du xiiie siecle Rutebeuf se fait gravement l echo de la faiblesse humaine de l incertitude et de la pauvrete a l oppose des valeurs courtoises Creme brulee")));
    }
}