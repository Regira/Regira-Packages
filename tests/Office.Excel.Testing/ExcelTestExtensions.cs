using Office.Excel.Testing.Models;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Office.Excel.Abstractions;
using Regira.Office.Excel.Models;
using Regira.Utilities;
using System.Text.Json;

[assembly: Parallelizable(ParallelScope.Fixtures)]

namespace Office.Excel.Testing;

public static class ExcelTestExtensions
{
    public static async Task Run_List_To_Excel<TService>(this TService service)
        where TService : IExcelService<ExcelCountry>
    {
        var assetsDir = service.GetAssetsDir();
        var items = await ParseCountriesJson(assetsDir);
        var countries = items.ToExcelCountries();

        using var excelFile = await service.CreateExcel(countries);

        Assert.Multiple(() =>
        {
            Assert.That(excelFile.GetBytes(), Is.Not.Null);
            Assert.That(excelFile.GetLength(), Is.GreaterThan(0));
        });
        var outputPath = Path.Combine(assetsDir, "Output", "TestData.xlsx");
        var file = await excelFile.SaveAs(outputPath);

        Assert.That(file.Exists, Is.True);
    }
    public static async Task Run_Compare_DictionaryCollection_Input_With_Output<TService>(this TService service)
        where TService : IExcelService
    {
        var testData = CreateTestData();
        var dicList = testData.Select(x => DictionaryUtility.ToDictionary(x)).ToList();
        using var excelFile = (await service.CreateExcel(dicList))
            .ToBinaryFile();
        _ = await service.SaveExcel(excelFile, "DicList.xlsx");

        var sheets = (await service.Read(excelFile)).ToList();
        var data = sheets[0].Data!.ToList();
        for (var i = 0; i < testData.Length; i++)
        {
            var sourceItem = testData[i];
            var excelItem = DictionaryUtility.ToDictionary(data[i]);
            Assert.Multiple(() =>
            {
                Assert.That(excelItem["Title"], Is.EqualTo(sourceItem.Title));
                Assert.That(Convert.ToDecimal(excelItem["Value"]) - sourceItem.Value, Is.LessThan(.000000001m));
            });
            var excelDate = (DateTime)excelItem["Created"]!;
            Assert.That((sourceItem.Created - excelDate), Is.LessThan(TimeSpan.FromMilliseconds(1)));
        }
    }
    public static async Task Run_Compare_UnTyped_Input_With_Output<TService>(this TService service)
        where TService : IExcelService
    {
        var testData = CreateTestData();
        using var excelFile = (await service.CreateExcel(testData))
            .ToBinaryFile();
        var sheets = (await service.Read(excelFile)).ToList();
        var data = sheets[0].Data!.ToList();
        for (var i = 0; i < testData.Length; i++)
        {
            var sourceItem = testData[i];
            var excelItem = DictionaryUtility.ToDictionary(data[i]);
            Assert.Multiple(() =>
            {
                Assert.That(excelItem["Title"], Is.EqualTo(sourceItem.Title));
                Assert.That(Convert.ToDecimal(excelItem["Value"]) - sourceItem.Value, Is.LessThan(.000000001m));
            });
            var excelDate = (DateTime)excelItem["Created"]!;
            Assert.That((sourceItem.Created - excelDate), Is.LessThan(TimeSpan.FromMilliseconds(1)));
        }
    }
    public static async Task Run_Compare_Typed_Input_With_Output<TService>(this TService service)
        where TService : IExcelService
    {
        var testData = CreateTestData();
        using var excelFile = (await service.CreateExcel(testData))
            .ToBinaryFile();

        var sheets = await service.Read(excelFile);
        var data = sheets.First().Data;
        var excelItems = JsonSerializer.Deserialize<TestObject[]>(JsonSerializer.Serialize(data))!.ToList();

        for (var i = 0; i < testData.Length; i++)
        {
            var sourceItem = testData[i];
            var excelItem = excelItems[i];
            Assert.Multiple(() =>
            {
                Assert.That(excelItem.Title, Is.EqualTo(sourceItem.Title));
                Assert.That(Math.Abs(sourceItem.Value - excelItem.Value), Is.LessThan(.000000001m));
            });
            var excelDate = excelItem.Created;
            Assert.That((sourceItem.Created - excelDate), Is.LessThan(TimeSpan.FromMilliseconds(1)));
        }
    }
    public static async Task Run_Read_With_Duplicate_Headers<TService>(this TService service)
        where TService : IExcelService
    {
        var assetsDir = service.GetAssetsDir();
        var inputPath = Path.Combine(assetsDir, "Input", "input-with-duplicates.xlsx");
        using var inputFile = new BinaryFileItem { Path = inputPath };
        var sheets = (await service.Read(inputFile)).ToList();
        Assert.That(sheets, Is.Not.Empty);
        var data = sheets.First().Data!.ToList();
        Assert.That(data, Is.Not.Empty);
    }

    public static async Task Run_Export_Countries_As_Dictionary<TService>(this TService service)
        where TService : IExcelService
    {
        var assetsDir = service.GetAssetsDir();
        var countriesJSON = await File.ReadAllTextAsync(Path.Combine(assetsDir, "Input", "countries.json"));
        var countries = JsonSerializer.Deserialize<IList<Dictionary<string, object>>>(countriesJSON)!;
        using var excelFile = await service.CreateExcel(countries);
        Assert.Multiple(() =>
        {
            Assert.That(excelFile.GetBytes(), Is.Not.Null);
            Assert.That(excelFile.GetLength() > 0, Is.True);
        });

        var file = await service.SaveExcel(excelFile, "countries-output.xlsx");
        Assert.That(file.Exists, Is.True);
    }
    public static async Task Run_Export_Countries<TService>(this TService service)
        where TService : IExcelService<ExcelCountry>
    {
        var assetsDir = service.GetAssetsDir();
        var items = await ParseCountriesJson(assetsDir);
        var countries = items.ToExcelCountries();

        using var excelFile = await service.CreateExcel(countries);
        Assert.Multiple(() =>
        {
            Assert.That(excelFile.GetBytes(), Is.Not.Null);
            Assert.That(excelFile.GetLength() > 0, Is.True);
        });

        var file = await service.SaveExcel(excelFile, "countries-output.xlsx");
        Assert.That(file.Exists, Is.True);
    }
    public static async Task Run_Export_Countries_As_Sheet<TService>(this TService service)
        where TService : IExcelService
    {
        var assetsDir = service.GetAssetsDir();
        var countriesJSON = await File.ReadAllTextAsync(Path.Combine(assetsDir, "Input", "countries-2.json"));
        var countries = JsonSerializer.Deserialize<IList<Dictionary<string, object>>>(countriesJSON)!;
        var sheet = new ExcelSheet
        {
            Name = "Countries",
            Data = countries.Cast<object>().ToList()
        };
        using var excelFile = await service.Create([sheet]);
        Assert.Multiple(() =>
        {
            Assert.That(excelFile.GetBytes(), Is.Not.Null);
            Assert.That(excelFile.GetLength() > 0, Is.True);
        });

        var file = await service.SaveExcel(excelFile, "countries-as-sheet-output.xlsx");
        Assert.That(file.Exists, Is.True);
    }
    
    public static async Task Run_From_Json<TService>(this TService service)
        where TService : IExcelService
    {
        var assetsDir = service.GetAssetsDir();
        var countriesJSON = await File.ReadAllTextAsync(Path.Combine(assetsDir, "Input", "countries.json"));

        var data = JsonSerializer.Deserialize<IList<Dictionary<string, object>>>(countriesJSON)!
            .Cast<object>()
            .ToList();
        using var excelFile = await service.Create(new List<ExcelSheet>
        {
            new () {Name = "Countries", Data = data}
        });
        Assert.Multiple(() =>
        {
            Assert.That(excelFile.GetBytes(), Is.Not.Null);
            Assert.That(excelFile.GetLength() > 0, Is.True);
        });
        
        var file = await service.SaveExcel(excelFile, "from_json.xlsx");
        Assert.That(file.Exists, Is.True);
    }

    internal static string CountriesJsonInputPath(string assetsDir) => Path.Combine(assetsDir, "Input", "countries.json");
    internal static async Task<IList<Country>> ParseCountriesJson(string assetsDir)
    {
        var jsonPath = CountriesJsonInputPath(assetsDir);
        var json = await File.ReadAllTextAsync(jsonPath);
        return JsonSerializer.Deserialize<List<Country>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
    internal static async Task<IMemoryFile> CreateExcel<TService>(this TService service, IEnumerable<object> input)
        where TService : IExcelService
    {
        var sheet = new ExcelSheet { Data = input.ToList() };
        return await service.Create([sheet]);
    }
    internal static async Task<IMemoryFile> CreateExcel<TService>(this TService service, IEnumerable<ExcelCountry> input)
        where TService : IExcelService<ExcelCountry>
    {
        var sheet = new ExcelSheet<ExcelCountry> { Data = input.ToList() };
        return await service.Create([sheet]);
    }

    internal static string GetAssetsDir<TService>(this TService service)
        where TService : IExcelServiceCore
    {
        var assemblyDir = AssemblyUtility.GetAssemblyDirectory()!;
        var assetsDir = new DirectoryInfo(Path.Combine(assemblyDir, "../../../", "Assets")).FullName;
        Directory.CreateDirectory(assetsDir);

        return assetsDir;
    }
    internal static Task<FileInfo> SaveExcel<TService>(this TService service, IMemoryFile excelFile, string fileName)
        where TService : IExcelServiceCore
    {
        var assetsDir = service.GetAssetsDir();
        var serviceNamespace = service.GetType().Namespace!.Split(".").Last();
        var outputDir = Path.Combine(assetsDir, "Output", serviceNamespace);
        var outputPath = Path.Combine(outputDir, fileName);
        Directory.CreateDirectory(outputDir);
        return excelFile.SaveAs(outputPath);
    }
    internal static TestObject[] CreateTestData() => Enumerable.Range(0, 100)
        .Select((_, i) => new TestObject
        {
            Id = i + 1,
            Title = $"Item #{i + 1}",
            Value = 100 / (decimal)(i + 1),
            Created = DateTime.Now.AddDays(-(100 - i))
        })
        .ToArray();
}