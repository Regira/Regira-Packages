using NUnit.Framework.Legacy;
using Regira.Utilities;
using System.Text.Json;

namespace Common.Testing.Utilities;

struct Country
{
    public string Code { get; set; }
    public string Title { get; set; }
    public DateTime? Created { get; set; }
    public float LifeExpectancy { get; set; }
    public int Population { get; set; }
    public string CallingCode { get; set; }
}
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class DictionaryUtilityTests
{
    private object? _plainObj;
    private object? _objWithArray;
    private object? _nestedObj;
    private readonly IList<Country> _countries;
    public DictionaryUtilityTests()
    {
        var assetsDir = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../assets")).FullName;
        var countriesJson = File.ReadAllText(Path.Combine(assetsDir, "countries.json"));
        _countries = JsonSerializer.Deserialize<IList<Country>>(countriesJson)!;
    }

    [SetUp]
    public void Setup()
    {
        _plainObj = new
        {
            Id = 1,
            Title = "Test object",
            Value = (string?)null,
            Created = new DateTime(2012, 10, 21)
        };
        _objWithArray = new
        {
            Values = new[] { 1, 2, 3 }
        };
        _nestedObj = new
        {
            Id = 3,
            Title = "Test object",
            Value = (string?)null,
            Created = new DateTime(2012, 10, 21),
            Parent = new
            {
                Id = 1,
                Title = "Parent object",
                Parent = new
                {
                    Id = 1,
                    Title = "Root object"
                },
                Collection = new[] {
                    new {
                        Id = "1.1",
                        Title = "Nested collection item #1"
                    }
                }
            }
        };
    }

    #region ToDictonary
    [Test]
    public void Args_To_Dictionary()
    {
        var args = new[]
        {
            "-input", "input directory/file.sql",
            "-p",
            "-o", "output",
            "ignore"
        };
        var dic = args.ToDictionary();
        Assert.That(dic["input"], Is.EqualTo("input directory/file.sql"));
        Assert.That(dic["p"], Is.Null);
        Assert.That(dic["o"], Is.EqualTo("output"));
        Assert.That(dic.ContainsKey("ignore"), Is.False);
        Assert.That(dic.Values.Contains("ignore"), Is.False);
    }
    [Test]
    public void Plain_Object_To_Dictionary()
    {
        dynamic? obj = _plainObj;
        var dic = DictionaryUtility.ToDictionary(obj);
        CollectionAssert.IsNotEmpty(dic);
        Assert.That(obj?.Id, Is.EqualTo(dic["Id"]));
        Assert.That(obj?.Title, Is.EqualTo(dic["Title"]));
        Assert.That(obj?.Created, Is.EqualTo(dic["Created"]));
    }
    [Test]
    public void Plain_Object_To_Dictionary_With_CaseInsensitive()
    {
        dynamic? obj = _plainObj;
        var dic = DictionaryUtility.ToDictionary(obj);
        var keys = ((ICollection<string>)dic.Keys).ToArray(); // use ToArray to remove case insensitivity on key collection
        Assert.That(keys, Has.Member("Id"));
        Assert.That(keys, Has.No.Member("id"));
        Assert.That(obj?.Id, Is.EqualTo(dic["id"]));
        Assert.That(obj?.Title, Is.EqualTo(dic["title"]));
        Assert.That(obj?.Created, Is.EqualTo(dic["created"]));
    }
    [Test]
    public void Object_With_Array_To_Dictionary()
    {
        dynamic? obj = _objWithArray;
        var dic = DictionaryUtility.ToDictionary(obj);
        CollectionAssert.IsNotEmpty(dic["Values"]);
        var src = (int[])obj!.Values;
        var result = (IList<object>)dic["Values"];
        Assert.That(result, Is.EqualTo(src).AsCollection);
    }
    [Test]
    public void Nested_Object()
    {
        dynamic obj = _nestedObj!;
        IDictionary<string, object?> dic = DictionaryUtility.ToDictionary(obj);
        IDictionary<string, object?> parent = (IDictionary<string, object?>)dic["Parent"]!;
        ClassicAssert.IsInstanceOf<IDictionary<string, object?>>(parent);
        Assert.That(obj.Parent.Id, Is.EqualTo(parent["Id"]));
        Assert.That(obj.Parent.Title, Is.EqualTo(parent["Title"]));
        IDictionary<string, object?> root = (IDictionary<string, object?>)parent["Parent"]!;
        Assert.That(obj.Parent.Id, Is.EqualTo(root["Id"]));
    }
    #endregion

    #region TableArray tests
    [Test]
    public void Dictionaries_To_TableArray()
    {
        var countries = _countries
            .Select(c => DictionaryUtility.ToDictionary(c))
            .ToList();
        var table = countries.ToTableArray();
        // test headers
        Assert.That(table[0, 0], Is.EqualTo("Code"));
        Assert.That(table[0, 1], Is.EqualTo("Title"));
        Assert.That(table[0, 2], Is.EqualTo("Created"));

        // test values
        var keys = countries[0].Keys.ToArray();
        for (var i = 0; i < countries.Count(); i++)
        {
            for (var j = 0; j < keys.Length; j++)
            {
                var key = keys[j];
                Assert.That(countries[i][key], Is.EqualTo(table[i + 1, j]));
            }
        }
    }
    [Test]
    public void TableArray_To_Dictionaries()
    {
        var countries = _countries
            .Select(c => DictionaryUtility.ToDictionary(c))
            .ToList();
        var table = countries.ToTableArray();
        var dicList = DictionaryUtility
            .FromTableArray(table)
            .ToArray();

        // test values
        var keys = countries[0].Keys.ToArray();
        Assert.That(keys[0], Is.EqualTo("Code"));
        Assert.That(keys[1], Is.EqualTo("Title"));
        Assert.That(keys[2], Is.EqualTo("Created"));
        for (var i = 0; i < countries.Count(); i++)
        {
            for (var j = 0; j < keys.Length; j++)
            {
                var key = keys[j];
                Assert.That(table[i + 1, j], Is.EqualTo(dicList[i][key]));
            }
        }
    }
    #endregion

    #region Flatten tests
    [Test]
    public void Flatten_Returns_Dictionary()
    {
        var source = new Dictionary<string, object?>();
        var target = source.Flatten();
        Assert.That(target, Is.Not.Null);
        Assert.That(target, Is.InstanceOf<IDictionary<string, object?>>());
    }
    [Test]
    public void Flatten_Simple_Values()
    {
        var source = new Dictionary<string, object?> {
            { "factuurNummer", "0123456" },
            { "factuurDatum", "2019-11-19" }
        };
        var target = source.Flatten();
        Assert.That(source["factuurNummer"], Is.EqualTo(target["factuurNummer"]));
        Assert.That(source["factuurDatum"], Is.EqualTo(target["factuurDatum"]));
    }
    [Test]
    public void Flatten_Primitive_Collection()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };
        var source = new Dictionary<string, object?>
        {
            ["Numbers"] = numbers,
            ["MyInt"] = 42,
            ["MyString"] = "Hello World",
            ["Date"] = DateTime.Today,
        };
        var target = source.Flatten();
        Assert.That(target, Is.Not.Null);
        Assert.That(target, Is.InstanceOf<IDictionary<string, object?>>());
        Assert.That(target.ContainsKey("Numbers"), Is.False);
        Assert.That(target.ContainsKey("MyInt"), Is.True);
        Assert.That(target.ContainsKey("MyString"), Is.True);
        Assert.That(target.ContainsKey("Date"), Is.True);
        for (var i = 0; i < numbers.Length; i++)
        {
            Assert.That(target[$"Numbers.{i}"], Is.EqualTo(numbers[i]));
        }
    }
    [Test]
    public void Flatten_CollectionObjects()
    {
        var source = new Dictionary<string, object?> {
            { "factuurLijnen", new[] {
                new Dictionary<string, object?> {
                    { "omschrijving", "Vervoer" },
                    { "eenheidsPrijs", 160.50 }
                },
                new Dictionary<string, object?> {
                    { "omschrijving", "Diensten" },
                    { "eenheidsPrijs", 180.50 }
                }
            } }
        };
        var target = source.Flatten();
        Assert.That(target.ContainsKey("factuurLijnen"), Is.False);
        Assert.That(target.ContainsKey("factuurLijnen.0.omschrijving"), Is.True);
        var sourceFactuurLijnen = (IDictionary<string, object?>[])source["factuurLijnen"]!;
        Assert.That(sourceFactuurLijnen[0]["omschrijving"], Is.EqualTo(target["factuurLijnen.0.omschrijving"]));
    }
    [Test]
    public void Flatten_Ignore_CollectionValues()
    {
        var source = new Dictionary<string, object?>
        {
            ["numbers"] = new[] { 1, 2, 3 },
            ["factuurLijnen"] = new[] {
                new Dictionary<string, object?> {
                    { "omschrijving", "Vervoer" },
                    { "eenheidsPrijs", 160.50 }
                },
                new Dictionary<string, object?> {
                    { "omschrijving", "Diensten" },
                    { "eenheidsPrijs", 180.50 }
                }
            }
        };
        var target = source.Flatten(new FlattenOptions { IgnoreCollections = true });
        Assert.That(target.ContainsKey("numbers"), Is.True);
        Assert.That((int[])target["numbers"]!, Is.Not.Empty);
        Assert.That(target.ContainsKey("factuurLijnen"), Is.True);
        Assert.That((IList<IDictionary<string, object?>>)target["factuurLijnen"]!, Is.Not.Empty);
    }
    [Test]
    public void Flatten_Nested_CollectionValues()
    {
        var source = new Dictionary<string, object?> {
            { "factuurLijnen", new[] {
                new Dictionary<string, object?> {
                    { "nested", new Dictionary<string,object> {
                        { "first", "1st nested" },
                        { "second", "2nd nested" }
                    } }
                }
            } }
        };
        var target = source.Flatten();
        var sourceFactuurLijnen = (IDictionary<string, object?>[])source["factuurLijnen"];
        var sourceLijn = sourceFactuurLijnen.First();
        var sourceFirstNested = (IDictionary<string, object?>)sourceLijn["nested"];
        Assert.That(sourceFirstNested["first"], Is.EqualTo(target["factuurLijnen.0.nested.first"]));
    }
    [Test]
    public void Flatten_Nested_Ignore_Collections()
    {
        var source = new Dictionary<string, object?> {
            { "factuurLijnen", new[] {
                new Dictionary<string, object?> {
                    { "nested", new Dictionary<string,object> {
                        { "first", "1st nested" },
                        { "second", "2nd nested" }
                    } }
                }
            } }
        };
        var target = source.Flatten(new FlattenOptions { IgnoreCollections = true });
        var sourceFactuurLijnen = (IDictionary<string, object?>[])source["factuurLijnen"];
        var sourceLijn = sourceFactuurLijnen.First();
        var sourceFirstNested = (IDictionary<string, object?>)sourceLijn["nested"];
        var targetFactuurLijnen = (IList<IDictionary<string, object?>>)target["factuurLijnen"];
        Assert.That(sourceFirstNested["first"], Is.EqualTo(targetFactuurLijnen[0]["nested.first"]));
    }
    [Test]
    public void Flatten_Removes_Hierarchy()
    {
        var source = new Dictionary<string, object?> {
            { "leverancier", new Dictionary<string,object?> {
                { "code", null },
                { "naam", "LEVERANCIER BVBA" },
                { "telefoon", "003239999999" }
            } }
        };
        // only works for NewtonSoft.Json for now...
        //var json = _serializer.Serialize(source);
        //source = _serializer.Deserialize<Dictionary<string, object>>(json);
        var target = source.Flatten();
        Assert.That(target.ContainsKey("leverancier"), Is.False);
        var sourceLeverancier = (IDictionary<string, object?>)source["leverancier"]!;
        Assert.That(sourceLeverancier["naam"], Is.EqualTo(target["leverancier.naam"]));
    }
    [Test]
    public void Flatten_Removes_Nested_Hierarchy()
    {
        var source = new Dictionary<string, object?> {
            { "leverancier", new Dictionary<string, object?> {
                { "adres", new Dictionary<string, object?> {
                    { "straat", "Kerkstraat 1" },
                    { "postcode", "2000" },
                    { "gemeente", "Antwerpen" }
                } }
            } }
        };
        var target = source.Flatten();
        var sourceLeverancier = (IDictionary<string, object?>)source["leverancier"]!;
        Assert.That(sourceLeverancier.ContainsKey("adres"), Is.True);
        Assert.That(sourceLeverancier["adres"], Is.InstanceOf<IDictionary<string, object?>>());
        var sourceLeverancierAdres = (IDictionary<string, object?>)sourceLeverancier["adres"]!;
        Assert.That(sourceLeverancierAdres["straat"], Is.EqualTo(target["leverancier.adres.straat"]));
    }
    [Test]
    public void Flatten_Nested_Collections_In_Collections()
    {
        var source = new Dictionary<string, object?> {
            { "factuurLijnen", new[] {
                new Dictionary<string, object?> {
                    { "omschrijving", "Vervoer" },
                    { "eenheidsPrijs", 160.50 }
                },
                new Dictionary<string, object?> {
                    { "nested", new Dictionary<string, object?> {
                        { "first", "1st nested" },
                        { "second", new[] {
                            new Dictionary<string, object?> {
                                { "foo", new Dictionary<string, object?> {
                                    { "value", "fooValue" }
                                } }
                            }
                        } },
                        { "third", "3th nested" }
                    } }
                }
            } }
        };
        var target = source.Flatten();
        var sourceFactuurLijnen = ((IEnumerable<IDictionary<string, object?>>)source["factuurLijnen"]).ToArray();
        var sourceLijn2 = sourceFactuurLijnen[1];
        var sourceNestedProp = (IDictionary<string, object?>)sourceLijn2["nested"]!;
        Assert.That(sourceNestedProp["first"], Is.EqualTo(target["factuurLijnen.1.nested.first"]));
        var sourceNestedSecond = ((IEnumerable<IDictionary<string, object?>>)sourceNestedProp["second"]).ToList();
        var sourceNestedSecondFoo = (IDictionary<string, object?>)sourceNestedSecond[0]["foo"]!;
        Assert.That(sourceNestedSecondFoo["value"], Is.EqualTo(target["factuurLijnen.1.nested.second.0.foo.value"]));
    }
    [Test]
    public void Flatten_Full_Unflattened_Sample()
    {
        IDictionary<string, object?> source = new Dictionary<string, object?> {
            { "factuurNummer", "0123456" },
            { "factuurDatum", "2019-11-19" },
            { "factuurType", 0 },
            { "vervalDatum", "2019-12-04" },
            { "betalingsVoorwaarden", "Xxx" },
            { "leverancier.rekeningNummer.iban", "BEXXXXXXXXXXXXXX" },
            { "leverancier.rekeningNummer.bic", "KREDBEBB" },
            { "leverancier.kbo", "BEXXXXXXXXXX" },
            { "leverancier.naam", "LEVERANCIER BVBA" },
            { "leverancier.telefoon", "003239999999" },
            { "leverancier.email", "facturatie@xxx.be" },
            { "leverancier.adres.straat", "Kerkstraat 1" },
            { "leverancier.adres.postcode", "2000" },
            { "leverancier.adres.gemeente", "Antwerpen" },
            { "factuurLijnen", new[] {
                    new Dictionary<string, object?> {
                        { "omschrijving", "Vervoer" },
                        { "eenheidsPrijs", 160.50 },
                        { "nested.first", "1st nested" },
                        { "nested.second", "2nd nested" }
                    },
                    new Dictionary<string, object?> {
                        { "omschrijving", "Diensten" },
                        { "eenheidsPrijs", 180.50 },
                        { "nested.first", "1st nested" },
                        { "nested.second", new[] {
                            new Dictionary<string, object?> {
                                { "foo.value", "fooValue" }
                            }
                        } },
                        { "nested.third", "3th nested" }
                    }
                }
            }
        };
        source = source.Unflatten();
        var target = source.Flatten();

        Assert.That(source["factuurNummer"], Is.EqualTo(target["factuurNummer"]));
        Assert.That(source["factuurDatum"], Is.EqualTo(target["factuurDatum"]));

        var sourceFactuurLijnen = (IList<IDictionary<string, object?>>)source["factuurLijnen"];
        var sourceLijn = sourceFactuurLijnen.First();
        var sourceFirstNested = (IDictionary<string, object?>)sourceLijn["nested"];
        Assert.That(sourceFirstNested["first"], Is.EqualTo(target["factuurLijnen.0.nested.first"]));

        var sourceLeverancier = (IDictionary<string, object?>)source["leverancier"];
        Assert.That(sourceLeverancier.ContainsKey("adres"), Is.True);
        Assert.That(sourceLeverancier["adres"], Is.InstanceOf<IDictionary<string, object?>>());
        var sourceLeverancierAdres = (IDictionary<string, object?>)sourceLeverancier["adres"]!;
        Assert.That(sourceLeverancierAdres["straat"], Is.EqualTo(target["leverancier.adres.straat"]));

        var sourceLijn2 = sourceFactuurLijnen[1];
        var sourceNestedProp = (IDictionary<string, object?>)sourceLijn2["nested"];
        Assert.That(sourceNestedProp["first"], Is.EqualTo(target["factuurLijnen.1.nested.first"]));
        var sourceNestedSecond = ((IEnumerable<IDictionary<string, object?>>)sourceNestedProp["second"]).ToList();
        var sourceNestedSecondFoo = (IDictionary<string, object?>)sourceNestedSecond[0]["foo"];
        Assert.That(sourceNestedSecondFoo["value"], Is.EqualTo(target["factuurLijnen.1.nested.second.0.foo.value"]));
    }
    //        [Test]
    //        public void Flatten_Json_Sample()
    //        {
    //            var json = @"{
    //  ""factuurNummer"": ""0123456"",
    //  ""leverancier"": {
    //    ""naam"": ""Company X"",
    //    ""adres"": {
    //      ""straat"": ""Straat 111"",
    //      ""gemeente"": ""Antwerpen""
    //    }
    //  },
    //  ""factuurLijnen"": [
    //    {
    //      ""volgnummer"": 1,
    //      ""eenheidsPrijs"": 160.5,
    //      ""btw"": {
    //        ""tarief"": 0
    //      }
    //    },
    //    {
    //      ""volgnummer"": 2,
    //      ""eenheidsPrijs"": 180.5,
    //      ""btw"": {
    //        ""tarief"": 0
    //      }
    //    }
    //  ]
    //}";
    //            // only works for NewtonSoft.Json for now...
    //            var source = JsonSerializer.Deserialize<IDictionary<string, object?>>(json)!;
    //            var target = DictionaryUtility.Flatten(source);
    //            //var flatJson = JsonSerializer.Serialize(target);
    //            ClassicAssert.AreEqual(source["factuurNummer"], target["factuurNummer"]);
    //            var leverancier = (IDictionary<string, object?>)source["leverancier"];
    //            var adres = (IDictionary<string, object?>)leverancier["adres"];
    //            ClassicAssert.AreEqual(adres["straat"], target["leverancier.adres.straat"]);
    //            var factuurLijnen = (IList<IDictionary<string, object?>>)source["factuurLijnen"];
    //            ClassicAssert.AreEqual(factuurLijnen[0]["volgnummer"], target["factuurLijnen.0.volgnummer"]);
    //            var firstBtw = (IDictionary<string, object?>)factuurLijnen.First()["btw"];
    //            ClassicAssert.AreEqual(firstBtw["tarief"], target["factuurLijnen.0.btw.tarief"]);
    //        }
    #endregion

    #region Unflatten tests
    [Test]
    public void Unflatten_Returns_Dictionary()
    {
        var source = new Dictionary<string, object?>();
        var target = source.Unflatten();
        Assert.That(target, Is.Not.Null);
        Assert.That(target, Is.InstanceOf<IDictionary<string, object?>>());
    }
    [Test]
    public void Unflatten_Simple_Values()
    {
        var source = new Dictionary<string, object?> {
            {"factuurNummer", "0123456"},
            {"factuurDatum", "2019-11-19"}
        };
        var target = source.Unflatten();
        Assert.That(source["factuurNummer"], Is.EqualTo(target["factuurNummer"]));
        Assert.That(source["factuurDatum"], Is.EqualTo(target["factuurDatum"]));
    }
    [Test]
    public void Unflatten_Primitive_Collection()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };
        var source = new Dictionary<string, object?>
        {
            ["Numbers.0"] = numbers[0],
            ["Numbers.1"] = numbers[1],
            ["Numbers.2"] = numbers[2],
            ["Numbers.3"] = numbers[3],
            ["Numbers.4"] = numbers[4],
            ["MyInt"] = 42,
            ["MyString"] = "Hello World",
            ["Date"] = DateTime.Today,
        };
        var target = source.Unflatten();
        Assert.That(target, Is.Not.Null);
        Assert.That(target, Is.InstanceOf<IDictionary<string, object?>>());
        Assert.That(target.ContainsKey("Numbers"), Is.True);
        Assert.That(target.ContainsKey("MyInt"), Is.True);
        Assert.That(target.ContainsKey("MyString"), Is.True);
        Assert.That(target.ContainsKey("Date"), Is.True);
        Assert.That(target["Numbers"], Is.EquivalentTo(numbers));
    }
    [Test]
    public void Unflatten_CollectionObjects()
    {
        // testing with non-flattened collection values as well
        var source = new Dictionary<string, object?> {
            {
                "factuurLijnen", new[] {
                    new Dictionary<string, object?> {
                        {"omschrijving", "Vervoer"},
                        {"eenheidsPrijs", 160.50}
                    },
                    new Dictionary<string, object?> {
                        {"omschrijving", "Diensten"},
                        {"eenheidsPrijs", 180.50}
                    }
                }
            }
        };
        var target = source.Unflatten();
        var targetFactuurLijnen = target["factuurLijnen"] as IEnumerable<IDictionary<string, object?>>;
        Assert.That(targetFactuurLijnen, Is.InstanceOf<IEnumerable<IDictionary<string, object?>>>());
        // ReSharper disable once AssignNullToNotNullAttribute
        var factuurlijnen = targetFactuurLijnen!.ToList();
        var lijn1 = factuurlijnen[0];
        var sourceFactuurLijnen = (IDictionary<string, object?>[])source["factuurLijnen"];
        Assert.That(sourceFactuurLijnen[0]["omschrijving"], Is.EqualTo(lijn1["omschrijving"]));
    }
    [Test]
    public void Unflatten_Flat_CollectionValues()
    {
        var source = new Dictionary<string, object?> {
            {"factuurLijnen.0.omschrijving", "Vervoer"},
            {"factuurLijnen.0.eenheidsPrijs", 160.50},
            {"factuurLijnen.1.omschrijving", "Diensten"},
            {"factuurLijnen.1.eenheidsPrijs", 180.50}
        };
        var target = source.Unflatten();
        var targetFactuurLijnen = target["factuurLijnen"] as IEnumerable<IDictionary<string, object?>>;
        Assert.That(targetFactuurLijnen, Is.InstanceOf<IEnumerable<IDictionary<string, object?>>>());
        // ReSharper disable once AssignNullToNotNullAttribute
        var factuurlijnen = targetFactuurLijnen.ToList();
        var lijn1 = factuurlijnen[0];
        Assert.That(source["factuurLijnen.0.omschrijving"], Is.EqualTo(lijn1["omschrijving"]));
    }
    [Test]
    public void Unflatten_Nested_CollectionValues()
    {
        var source = new Dictionary<string, object?> {
            { "factuurLijnen", new[] {
                new Dictionary<string, object?> {
                    { "nested.first", "1st nested" },
                    { "nested.second", "2nd nested" }
                }
            } }
        };
        var target = source.Unflatten();
        var sourceFactuurLijnen = (IDictionary<string, object?>[])source["factuurLijnen"]!;
        var sourceLijn = sourceFactuurLijnen[0];
        var targetFactuurLijnen = (IList<IDictionary<string, object?>>)target["factuurLijnen"]!;
        var targetLijn = targetFactuurLijnen[0];
        var targetNested = (IDictionary<string, object?>)targetLijn["nested"]!;
        Assert.That(targetNested["first"], Is.EqualTo(sourceLijn["nested.first"]));
    }
    [Test]
    public void Unflatten_Creates_Hierarchy()
    {
        var source = new Dictionary<string, object?> {
            { "leverancier.naam", "LEVERANCIER BVBA" },
            { "leverancier.telefoon", "003239999999" },
        };
        var target = source.Unflatten();
        Assert.That(target.ContainsKey("leverancier"), Is.True);
        Assert.That(target["leverancier"], Is.InstanceOf<IDictionary<string, object?>>());
        var targetLeverancier = (IDictionary<string, object?>)target["leverancier"]!;
        Assert.That(targetLeverancier["naam"], Is.EqualTo(source["leverancier.naam"]));
    }
    [Test]
    public void Unflatten_Creates_Nested_Hierarchy()
    {
        var source = new Dictionary<string, object?> {
            { "leverancier.adres.straat", "Kerkstraat 1" },
            { "leverancier.adres.postcode", "2000" },
            { "leverancier.adres.gemeente", "Antwerpen" }
        };
        var target = source.Unflatten();
        var targetLeverancier = (IDictionary<string, object?>)target["leverancier"]!;
        Assert.That(targetLeverancier.ContainsKey("adres"), Is.True);
        Assert.That(targetLeverancier["adres"], Is.InstanceOf<IDictionary<string, object?>>());
        var targetLeverancierAdres = (IDictionary<string, object?>)targetLeverancier["adres"]!;
        Assert.That(source["leverancier.adres.straat"], Is.EqualTo(targetLeverancierAdres["straat"]));
    }
    [Test]
    public void Collections_Remain_Collections()
    {
        var source = new Dictionary<string, object?> {
            { "factuurLijnen", new[] {
                new Dictionary<string, object?> {
                    {"omschrijving", "Vervoer"},
                    {"eenheidsPrijs", 160.50}
                },
                new Dictionary<string, object?> {
                    {"omschrijving", "Diensten"},
                    {"eenheidsPrijs", 180.50}
                }
            } }
        };
        var target = source.Unflatten();
        var targetFactuurLijnen = target["factuurLijnen"] as IEnumerable<IDictionary<string, object?>>;
        Assert.That(targetFactuurLijnen, Is.InstanceOf<IEnumerable<IDictionary<string, object?>>>());
        // ReSharper disable once AssignNullToNotNullAttribute
        var factuurlijnen = targetFactuurLijnen!.ToList();
        var lijn1 = factuurlijnen[0];
        var sourceFactuurLijnen = (IDictionary<string, object?>[])source["factuurLijnen"]!;
        Assert.That(sourceFactuurLijnen[0]["omschrijving"], Is.EqualTo(lijn1["omschrijving"]));
    }
    [Test]
    public void Unflatten_Nested_Collections_In_Collections()
    {
        var source = new Dictionary<string, object?> {
            { "factuurLijnen", new[] {
                new Dictionary<string, object?> {
                    { "omschrijving", "Vervoer" },
                    { "eenheidsPrijs", 160.50 }
                },
                new Dictionary<string, object?> {
                    { "nested.first", "1st nested" },
                    { "nested.second", new[] {
                        new Dictionary<string, object?> {
                            { "foo.value", "fooValue" }
                        }
                    } },
                    { "nested.third", "3th nested" }
                }
            } }
        };
        var target = source.Unflatten();
        var targetFactuurLijnen = ((IEnumerable<IDictionary<string, object?>>)target["factuurLijnen"]!).ToArray();
        var sourceFactuurLijnen = (IDictionary<string, object?>[])source["factuurLijnen"]!;
        var targetLijn2 = targetFactuurLijnen[1];
        var sourceLijn2 = sourceFactuurLijnen[1];
        Assert.That(targetLijn2["nested"] as IDictionary<string, object?>, Is.Not.Null);
        var targetNestedProp = (IDictionary<string, object?>)targetLijn2["nested"]!;
        Assert.That(sourceLijn2["nested.first"], Is.EqualTo(targetNestedProp["first"]));
        var targetNestedSecond = ((IEnumerable<IDictionary<string, object?>>)targetNestedProp["second"]!).ToList();
        var sourceNestedSecond = ((IEnumerable<IDictionary<string, object?>>)sourceLijn2["nested.second"]!).ToList();
        var targetNestedSecondFoo = (IDictionary<string, object?>)targetNestedSecond[0]["foo"]!;
        Assert.That(sourceNestedSecond[0]["foo.value"], Is.EqualTo(targetNestedSecondFoo["value"]));
    }
    [Test]
    public void Unflatten_Full_Sample_With_Custom_Separator()
    {
        var source = new Dictionary<string, object?> {
            { "factuurNummer", "0123456" },
            { "factuurDatum", "2019-11-19" },
            { "factuurType", 0 },
            { "vervalDatum", "2019-12-04" },
            { "betalingsVoorwaarden", "Xxx" },
            { "leverancier:rekeningNummer:iban", "BEXXXXXXXXXXXXXX" },
            { "leverancier:rekeningNummer:bic", "KREDBEBB" },
            { "leverancier:kbo", "BEXXXXXXXXXX" },
            { "leverancier:naam", "LEVERANCIER BVBA" },
            { "leverancier:telefoon", "003239999999" },
            { "leverancier:email", "facturatie@xxx:be" },
            { "leverancier:adres:straat", "Kerkstraat 1" },
            { "leverancier:adres:postcode", "2000" },
            { "leverancier:adres:gemeente", "Antwerpen" },
            { "factuurLijnen:0:omschrijving", "Vervoer" },
            { "factuurLijnen:0:eenheidsPrijs", 160.50 },
            { "factuurLijnen:0:nested:first", "1st nested" },
            { "factuurLijnen:0:nested:second", "2nd nested" },
            { "factuurLijnen:1:omschrijving", "Diensten" },
            { "factuurLijnen:1:eenheidsPrijs", 180.50 },
            { "factuurLijnen:1:nested:first", "1st nested" },
            { "factuurLijnen:1:nested:second:0:foo:value", "fooValue" },
            { "factuurLijnen:2:nested:nested:third", "3th nested" }
        };
        var target = source.Unflatten(new FlattenOptions { Separator = ":" });

        var targetLeverancier = (IDictionary<string, object?>)target["leverancier"]!;
        Assert.That(targetLeverancier.ContainsKey("adres"), Is.True);
        Assert.That(targetLeverancier["adres"], Is.InstanceOf<IDictionary<string, object?>>());
        var targetLeverancierAdres = (IDictionary<string, object?>)targetLeverancier["adres"]!;
        Assert.That(source["leverancier:adres:straat"], Is.EqualTo(targetLeverancierAdres["straat"]));

        var targetFactuurLijnen = ((IEnumerable<IDictionary<string, object?>>)target["factuurLijnen"]!).ToArray();
        var targetLijn2 = targetFactuurLijnen[1];
        Assert.That(targetLijn2["nested"] as IDictionary<string, object?>, Is.Not.Null);
        var targetNestedProp = (IDictionary<string, object?>)targetLijn2["nested"]!;
        Assert.That(source["factuurLijnen:0:nested:first"], Is.EqualTo(targetNestedProp["first"]));
        var targetNestedSecond = ((IEnumerable<IDictionary<string, object?>>)targetNestedProp["second"]!).ToList();
        var targetNestedSecondFoo = (IDictionary<string, object?>)targetNestedSecond[0]["foo"]!;
        Assert.That(source["factuurLijnen:1:nested:second:0:foo:value"], Is.EqualTo(targetNestedSecondFoo["value"]));
    }
    #endregion
}