using ClosedXML.Excel;
using Office.Excel.Testing.Models;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Excel.Abstractions;
using Regira.Office.Excel.Models;

namespace Office.Excel.Testing;

/// <summary>
/// The edge cases every backend is held to: empty sheets, rows with different keys, sheet names Excel
/// refuses, and header rows with blank or repeated headers. Input workbooks are built with ClosedXML
/// directly, so a read scenario does not depend on the writer under test.
/// </summary>
public static partial class ExcelTestExtensions
{
    public static async Task Run_Create_Empty_Sheets<TService>(this TService service)
        where TService : IExcelService
    {
        using var excelFile = await service.Create([
            new ExcelSheet { Name = "Empty", Data = [] },
            new ExcelSheet { Name = "Null" },
            new ExcelSheet { Name = "Items", Data = [new Dictionary<string, object?> { ["Title"] = "Item #1" }] }
        ]);
        AssertReadableWithoutRewind(excelFile);

        var sheets = (await service.Read(excelFile.ToBinaryFile())).ToList();
        Assert.That(sheets.Select(s => s.Name), Is.EqualTo(new[] { "Empty", "Null", "Items" }).AsCollection);
        Assert.Multiple(() =>
        {
            Assert.That(sheets[0].Data, Is.Empty);
            Assert.That(sheets[1].Data, Is.Empty);
            Assert.That(Row(sheets[2], 0)["Title"], Is.EqualTo("Item #1"));
        });
    }

    public static async Task Run_Create_Rows_With_Different_Keys<TService>(this TService service)
        where TService : IExcelService
    {
        var created = new DateTime(2026, 10, 7, 14, 30, 0);
        using var excelFile = await service.Create([new ExcelSheet
        {
            Name = "Items",
            Data = [
                new Dictionary<string, object?> { ["Title"] = "Item #1", ["Value"] = 1 },
                new Dictionary<string, object?> { ["Title"] = "Item #2", ["Created"] = created },
                new Dictionary<string, object?> { ["Note"] = "only here" }
            ]
        }]);
        AssertReadableWithoutRewind(excelFile);

        var sheet = (await service.Read(excelFile.ToBinaryFile())).Single();
        Assert.That(sheet.Data, Has.Count.EqualTo(3));
        Assert.That(Row(sheet, 0).Keys, Is.EqualTo(new[] { "Title", "Value", "Created", "Note" }).AsCollection, "Every key, in the order it first appears");
        Assert.Multiple(() =>
        {
            Assert.That(Convert.ToDouble(Row(sheet, 0)["Value"]), Is.EqualTo(1));
            Assert.That(Row(sheet, 0)["Note"], Is.Null);
            Assert.That(Row(sheet, 1)["Created"], Is.EqualTo(created));
            Assert.That(Row(sheet, 1)["Value"], Is.Null);
            Assert.That(Row(sheet, 2)["Title"], Is.Null);
            Assert.That(Row(sheet, 2)["Note"], Is.EqualTo("only here"));
        });
    }

    public static async Task Run_Create_Mixed_Value_Types<TService>(this TService service)
        where TService : IExcelService
    {
        var created = new DateTime(2026, 10, 7, 14, 30, 0);
        using var excelFile = await service.Create([new ExcelSheet
        {
            Name = "Items",
            Data = [
                new Dictionary<string, object?> { ["Value"] = created },
                new Dictionary<string, object?> { ["Value"] = "Item #1" },
                new Dictionary<string, object?> { ["Value"] = 12.5m }
            ]
        }]);

        var sheet = (await service.Read(excelFile.ToBinaryFile())).Single();
        // each cell keeps its own value's type, whatever the key's first value was
        Assert.Multiple(() =>
        {
            Assert.That(Row(sheet, 0)["Value"], Is.EqualTo(created));
            Assert.That(Row(sheet, 1)["Value"], Is.EqualTo("Item #1"));
            Assert.That(Convert.ToDouble(Row(sheet, 2)["Value"]), Is.EqualTo(12.5));
        });
    }

    public static async Task Run_Create_Keys_Differing_By_Case<TService>(this TService service)
        where TService : IExcelService
    {
        using var excelFile = await service.Create([new ExcelSheet
        {
            Name = "Items",
            Data = [
                new Dictionary<string, object?> { ["Id"] = 1, ["ID"] = 2 },
                new Dictionary<string, object?> { ["id"] = 3 }
            ]
        }]);

        var sheet = (await service.Read(excelFile.ToBinaryFile())).Single();
        // one column, spelled and valued as the key first appears, within a row as across rows
        Assert.That(Row(sheet, 0).Keys, Is.EqualTo(new[] { "Id" }).AsCollection);
        Assert.Multiple(() =>
        {
            Assert.That(Convert.ToDouble(Row(sheet, 0)["Id"]), Is.EqualTo(1));
            Assert.That(Convert.ToDouble(Row(sheet, 1)["Id"]), Is.EqualTo(3));
        });
    }

    public static async Task Run_Create_Nested_Object<TService>(this TService service)
        where TService : IExcelService
    {
        var item = new PricedItem { Title = "Item #1", Price = new Money(12.5m, "EUR") };
        using var excelFile = await service.Create([new ExcelSheet { Name = "Items", Data = [item] }]);

        var row = Row((await service.Read(excelFile.ToBinaryFile())).Single(), 0);
        // a property holding an object is written as the object's text, not as the object's properties
        Assert.That(row["Price"], Is.EqualTo(item.Price.ToString()));
    }

    public static async Task Run_Create_With_Invalid_Sheet_Names<TService>(this TService service)
        where TService : IExcelService
    {
        string[] invalidNames = ["", " ", new string('n', 32), "a:b", "a\\b", "a/b", "a?b", "a*b", "a[b", "a]b", "'Items", "Items'", "History"];
        using (Assert.EnterMultipleScope())
        {
            foreach (var name in invalidNames)
            {
                await Assert.ThrowsAsync<ArgumentException>(() => service.Create([Sheet(name)]), $"Sheet name '{name}'");
            }
            await Assert.ThrowsAsync<ArgumentException>(() => service.Create([Sheet("Items"), Sheet("ITEMS")]), "A name used twice, compared case-insensitively");
        }

        // the longest name Excel allows, and a generated name that steers clear of an explicit one
        var longest = new string('n', 31);
        using var excelFile = await service.Create([Sheet(longest), Sheet(null), Sheet("Sheet-1"), Sheet(null)]);
        var names = (await service.Read(excelFile.ToBinaryFile())).Select(s => s.Name).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(names, Has.Count.EqualTo(4));
            Assert.That(names[0], Is.EqualTo(longest));
            Assert.That(names[2], Is.EqualTo("Sheet-1"));
            Assert.That(names, Is.Unique);
        });

        static ExcelSheet Sheet(string? name) => new() { Name = name, Data = [new Dictionary<string, object?> { ["Title"] = "Item #1" }] };
    }

    public static async Task Run_Read_Empty_Sheet<TService>(this TService service)
        where TService : IExcelService
    {
        var input = BuildWorkbook(
            ("Empty", []),
            ("Items", [["Title"], ["Item #1"]]));

        var sheets = (await service.Read(input)).ToList();
        Assert.That(sheets.Select(s => s.Name), Is.EqualTo(new[] { "Empty", "Items" }).AsCollection);
        Assert.Multiple(() =>
        {
            Assert.That(sheets[0].Data, Is.Empty);
            Assert.That(sheets[1].Data, Has.Count.EqualTo(1));
            Assert.That(Row(sheets[1], 0)["Title"], Is.EqualTo("Item #1"));
        });
    }

    public static async Task Run_Read_Blank_And_Repeated_Headers<TService>(this TService service)
        where TService : IExcelService
    {
        var input = BuildWorkbook(("Items", [
            ["Title", null, "Value", "Value", "Value_2"],
            ["Item #1", "no header", 1, 2, 3]
        ]));

        var row = Row((await service.Read(input)).Single(), 0);
        // a blank header becomes Column{n}; a repeated one Value_{i}, skipping a name another header already has
        Assert.That(row.Keys, Is.EqualTo(new[] { "Title", "Column2", "Value", "Value_3", "Value_2" }).AsCollection);
        Assert.Multiple(() =>
        {
            Assert.That(row["Column2"], Is.EqualTo("no header"));
            Assert.That(Convert.ToDouble(row["Value"]), Is.EqualTo(1));
            Assert.That(Convert.ToDouble(row["Value_3"]), Is.EqualTo(2));
            Assert.That(Convert.ToDouble(row["Value_2"]), Is.EqualTo(3));
        });
    }

    public static async Task Run_Read_Selected_Headers<TService>(this TService service)
        where TService : IExcelService
    {
        var input = BuildWorkbook(("Items", [
            ["Title", "Value", "Created"],
            ["Item #1", 1, new DateTime(2026, 10, 7)]
        ]));

        var row = Row((await service.Read(input, ["title", "VALUE"])).Single(), 0);
        Assert.That(row.Keys, Is.EqualTo(new[] { "Title", "Value" }).AsCollection, "Matched case-insensitively, keyed as the workbook spells them");
    }

    public static async Task Run_Create_Typed_Empty_Sheets<TService>(this TService service)
        where TService : IExcelService<ExcelCountry>
    {
        using var excelFile = await service.Create([
            new ExcelSheet<ExcelCountry> { Name = "Empty", Data = [] },
            new ExcelSheet<ExcelCountry> { Name = "Null" }
        ]);
        AssertReadableWithoutRewind(excelFile);

        var sheets = (await service.Read(excelFile.ToBinaryFile())).ToList();
        Assert.That(sheets.Select(s => s.Name), Is.EqualTo(new[] { "Empty", "Null" }).AsCollection);
        Assert.That(sheets.Select(s => s.Data), Has.All.Empty);

        // an empty sheet still carries the header row
        using var workbook = new XLWorkbook(excelFile.GetStream()!);
        Assert.That(workbook.Worksheets.Select(ws => ws.Cell(1, 1).GetString()), Has.All.EqualTo(nameof(ExcelCountry.Name)));
    }

    private static IDictionary<string, object?> Row(ExcelSheet sheet, int index)
        => (IDictionary<string, object?>)sheet.Data!.ElementAt(index);

    internal static IBinaryFile BuildWorkbook(params (string Name, object?[][] Rows)[] sheets)
    {
        using var workbook = new XLWorkbook();
        foreach (var (name, rows) in sheets)
        {
            var worksheet = workbook.AddWorksheet(name);
            for (var r = 0; r < rows.Length; r++)
            {
                for (var c = 0; c < rows[r].Length; c++)
                {
                    if (rows[r][c] != null)
                    {
                        worksheet.Cell(r + 1, c + 1).Value = XLCellValue.FromObject(rows[r][c]);
                    }
                }
            }
        }
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray().ToBinaryFile();
    }
}
