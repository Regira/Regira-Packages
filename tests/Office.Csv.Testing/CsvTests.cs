using System.Globalization;
using Bogus;
using NUnit.Framework.Legacy;
using Office.Csv.Testing.Infrastructure;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Office.Csv.CsvHelper;
using Regira.Utilities;

[assembly: Parallelizable(ParallelScope.Fixtures)]

namespace Office.Csv.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class CsvTests
{
    private const string SAMPLE_1 = @"Id,Name
1,one
2,two
3,three";

    private readonly string _inputDir;
    private readonly string _outputDir;
    public CsvTests()
    {
        var assetsDir = Path.Combine(AppContext.BaseDirectory, "../../../Assets");
        assetsDir = new DirectoryInfo(assetsDir).FullName;
        _inputDir = Path.Combine(assetsDir, "Input");
        _outputDir = Path.Combine(assetsDir, "Output");
        Directory.CreateDirectory(_outputDir);
    }

    [TestCase(SAMPLE_1)]
    public async Task Read(string input)
    {
        var csvMgr = new CsvManager<dynamic>(new CsvHelperOptions { Culture = CultureInfo.CurrentCulture });
        var records = await csvMgr.Read(input);
        Assert.That(records, Is.Not.Empty);
        ClassicAssert.AreEqual("1", records.First().Id);
        ClassicAssert.AreEqual("one", records.First().Name);
    }
    [Test]
    public async Task Read_Cities()
    {
        var inputPath = Path.Combine(_inputDir, "cities.csv");

        var csvMgr = new CsvManager<CsvCity>(new CsvHelperOptions
        {
            Culture = CultureInfo.CurrentCulture,
            IgnoreBadData = true
        });
        using var inputFile = new BinaryFileItem { Path = inputPath };
        var cities = await csvMgr.Read(inputFile);

        Assert.That(cities, Is.Not.Empty);
        var firstCity = cities.First();
        Assert.That(firstCity.City, Is.EqualTo("Youngstown"));
        var lastCity = cities.Last();
        Assert.That(lastCity.City, Is.EqualTo("Ravenna"));
    }
    [Test]
    public async Task Write_Products()
    {
        var outputPath = Path.Combine(_outputDir, "products.csv");
        var productRule = new Faker<CsvProduct>()
            .RuleFor(p => p.Id, f => f.IndexFaker)
            .RuleFor(p => p.Title, f => f.Commerce.Product())
            .RuleFor(p => p.Price, f => decimal.Parse(f.Commerce.Price()))
            .RuleFor(p => p.Quantity, f => f.Random.Double(0, 100).OrNull(f, .2f))
            .RuleFor(p => p.IsOnline, f => f.Random.Bool())
            .RuleFor(p => p.Created, f => f.Date.Past());
        var products = productRule.Generate(100);

        var csvMgr = new CsvManager<CsvProduct>();

        var csvString = await csvMgr.Write(products);
        Assert.That(csvString, Is.Not.Empty);

        using var csvFile = await csvMgr.WriteFile(products);
        await csvFile.SaveAs(outputPath);
    }
    [Test]
    public async Task Write_Products_Dic()
    {
        var outputPath = Path.Combine(_outputDir, "products-from-dic.csv");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var productRule = new Faker<CsvProduct>()
            .RuleFor(p => p.Id, f => f.IndexFaker)
            .RuleFor(p => p.Title, f => f.Commerce.Product())
            .RuleFor(p => p.Price, f => decimal.Parse(f.Commerce.Price()))
            .RuleFor(p => p.Quantity, f => f.Random.Double(0, 100).OrNull(f, .2f))
            .RuleFor(p => p.IsOnline, f => f.Random.Bool())
            .RuleFor(p => p.Description, f => f.Lorem.Paragraphs(0, 3, Environment.NewLine).Replace("ipsum", "\"ipsum\""))
            .RuleFor(p => p.Created, f => f.Date.Past());
        var products = productRule.Generate(100)
            .Select(p => DictionaryUtility.ToDictionary(p))
            .ToList();

        var csvMgr = new CsvManager(new CsvHelperOptions { Culture = CultureInfo.CurrentCulture });

        var csvString = await csvMgr.Write(products!);
        Assert.That(csvString, Is.Not.Empty);

        using var csvFile = await csvMgr.WriteFile(products!);
        await csvFile.SaveAs(outputPath);
    }
    [Test]
    public async Task Read_Products_Dic()
    {
        var productRule = new Faker<CsvProduct>()
            .RuleFor(p => p.Id, f => f.IndexFaker)
            .RuleFor(p => p.Title, f => f.Commerce.Product())
            .RuleFor(p => p.Price, f => decimal.Parse(f.Commerce.Price()))
            .RuleFor(p => p.Quantity, f => f.Random.Double(0, 100).OrNull(f, .2f))
            .RuleFor(p => p.IsOnline, f => f.Random.Bool())
            .RuleFor(p => p.Created, f => f.Date.Past())
            .RuleFor(p => p.Description, f => f.Lorem.Paragraphs(0, 3, Environment.NewLine).Replace("ipsum", "\"ipsum\""));
        var products = productRule.Generate(100)
            .Select(p => DictionaryUtility.ToDictionary(p))
            .ToList();
        var csvMgr = new CsvManager(new CsvHelperOptions { Culture = CultureInfo.CurrentCulture });

        var csvString = await csvMgr.Write(products!);
        var writtenProducts = await csvMgr.Read(csvString);
        for (var i = 0; i < products.Count; i++)
        {
            var p1 = products[i];
            var p2 = writtenProducts[i];
            foreach (var key in p1.Keys)
            {
                Assert.That(p2[key], Is.EqualTo(p1[key]?.ToString() ?? string.Empty));
            }
        }
    }
}