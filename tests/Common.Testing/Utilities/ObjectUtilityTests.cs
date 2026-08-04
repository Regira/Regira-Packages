using Regira.Utilities;

namespace Common.Testing.Utilities;

[TestFixture]
public class ObjectUtilityTests
{
    public class Country
    {
        public string? Code { get; set; }
        public string? Title { get; set; }
        public DateTime? Created { get; set; }
        public float LifeExpectancy { get; set; }
        public int Population { get; set; }
        public string? CallingCode { get; set; }
        public ICollection<string>? Languages { get; set; }
        public ICollection<int>? Numbers { get; set; }
    }

    [Test]
    public void Create_Object()
    {
        var expected = new
        {
            Code = "BE",
            Title = "Belgium",
            Created = new DateTime(1830, 7, 21),
            LifeExpectancy = 81f,
            Population = 11_500_000,
            Languages = new[] { "nl", "fr", "de" },
            Numbers = new[] { 1, 3, 4, }
        };
        var obj = ObjectUtility.Create<Country>(expected);
        Assert.That(obj.Code, Is.EqualTo(expected.Code));
        Assert.That(obj.Title, Is.EqualTo(expected.Title));
        Assert.That(obj.Created, Is.EqualTo(expected.Created));
        Assert.That(obj.LifeExpectancy, Is.EqualTo(expected.LifeExpectancy));
        Assert.That(obj.Population, Is.EqualTo(expected.Population));
        Assert.That(obj.CallingCode, Is.Null);
        Assert.That(obj.Languages, Is.EquivalentTo(expected.Languages));
        Assert.That(obj.Numbers, Is.EquivalentTo(expected.Numbers));
    }

    [Test]
    public void Create_From_Dictionary()
    {
        var expected = new
        {
            Code = "BE",
            Title = "Belgium",
            Created = new DateTime(1830, 7, 21),
            LifeExpectancy = 81d,// decimal should be converted to float
            Population = 11_500_000,
            Languages = new[] { "nl", "fr", "de" },
            Numbers = new[] { 1, 3, 4, }
        };
        var dic = new Dictionary<string, object>
        {
            ["Code"] = expected.Code,
            ["Title"] = expected.Title,
            ["created"] = expected.Created,
            ["life expectancy"] = expected.LifeExpectancy,
            ["Population"] = expected.Population,
            ["languages"] = expected.Languages,
            ["numbers"] = expected.Numbers,
        };
        var obj = ObjectUtility.Create<Country>(dic);
        Assert.That(obj.Code, Is.EqualTo(expected.Code));
        Assert.That(obj.Title, Is.EqualTo(expected.Title));
        Assert.That(obj.Created, Is.EqualTo(expected.Created));
        Assert.That(obj.LifeExpectancy, Is.EqualTo(expected.LifeExpectancy));
        Assert.That(obj.Population, Is.EqualTo(expected.Population));
        Assert.That(obj.CallingCode, Is.Null);
        Assert.That(obj.Languages, Is.EquivalentTo(expected.Languages));
        Assert.That(obj.Numbers, Is.EquivalentTo(expected.Numbers));
    }
}