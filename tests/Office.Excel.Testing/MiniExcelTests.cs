using System.IO.Compression;
using Office.Excel.Testing.Models;
using Regira.IO.Extensions;
using Regira.Office.Excel.Abstractions;
using Regira.Office.Excel.MiniExcel;
using Regira.Office.Excel.Models;

namespace Office.Excel.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class MiniExcelTests
{
    IExcelService CreateExcelManager() => new ExcelManager();
    IExcelService<ExcelCountry> CreateTypedExcelManager() => new ExcelManager<ExcelCountry>();

    [Test]
    public Task List_To_Excel() => CreateTypedExcelManager()
        .Run_List_To_Excel();
    [Test]
    public Task Compare_DictionaryCollection_Input_With_Output() => CreateExcelManager()
        .Run_Compare_DictionaryCollection_Input_With_Output();
    [Test]
    public Task Compare_UnTyped_Input_With_Output() => CreateExcelManager()
        .Run_Compare_UnTyped_Input_With_Output();
    [Test]
    public Task Compare_Typed_Input_With_Output() => CreateExcelManager()
        .Run_Compare_Typed_Input_With_Output();
    [Test]
    public Task Read_With_Duplicate_Headers() => CreateExcelManager()
        .Run_Read_With_Duplicate_Headers();
    [Test]
    public Task Export_Countries_As_Dictionary() => CreateExcelManager()
        .Run_Export_Countries_As_Dictionary();

    [Test]
    public Task Export_Countries() => CreateTypedExcelManager()
        .Run_Export_Countries();

    [Test]
    public Task Export_Countries_As_Sheet() => CreateExcelManager()
        .Run_Export_Countries_As_Sheet();

    [Test]
    public Task From_Json() => CreateExcelManager()
        .Run_From_Json();

    [Test]
    public Task Create_Empty_Sheets() => CreateExcelManager()
        .Run_Create_Empty_Sheets();
    [Test]
    public Task Create_No_Sheets() => CreateExcelManager()
        .Run_Create_No_Sheets();
    [Test]
    public Task Create_Rows_With_Different_Keys() => CreateExcelManager()
        .Run_Create_Rows_With_Different_Keys();
    [Test]
    public Task Create_Mixed_Value_Types() => CreateExcelManager()
        .Run_Create_Mixed_Value_Types();
    [Test]
    public Task Create_Nested_Object() => CreateExcelManager()
        .Run_Create_Nested_Object();
    [Test]
    public Task Create_Keys_Differing_By_Case_Across_Rows() => CreateExcelManager()
        .Run_Create_Keys_Differing_By_Case_Across_Rows();
    [Test]
    public Task Create_Keys_Differing_By_Case_Within_A_Row() => CreateExcelManager()
        .Run_Create_Keys_Differing_By_Case_Within_A_Row();
    [Test]
    public Task Create_With_Invalid_Sheet_Names() => CreateExcelManager()
        .Run_Create_With_Invalid_Sheet_Names();
    [Test]
    public Task Read_Empty_Sheet() => CreateExcelManager()
        .Run_Read_Empty_Sheet();
    [Test]
    public Task Read_Blank_And_Repeated_Headers() => CreateExcelManager()
        .Run_Read_Blank_And_Repeated_Headers();
    [Test]
    public Task Read_Selected_Headers() => CreateExcelManager()
        .Run_Read_Selected_Headers();
    [Test]
    public Task Create_Typed_Empty_Sheets() => CreateTypedExcelManager()
        .Run_Create_Typed_Empty_Sheets();

    [Test]
    public async Task Create_Without_DateFormat_Keeps_Excels_Date_Format()
    {
        var created = new DateTime(2026, 10, 7, 14, 30, 0);
        using var excelFile = await new ExcelManager().Create([new ExcelSheet
        {
            Name = "Items",
            Data = [new Dictionary<string, object?> { ["Created"] = created }]
        }]);

        using var zip = new ZipArchive(excelFile.GetStream()!);
        var styles = await new StreamReader(zip.GetEntry("xl/styles.xml")!.Open()).ReadToEndAsync();
        Assert.That(styles, Does.Not.Contain("formatCode=\"yyyy-MM-dd hh:mm:ss\""));
    }

    [Test]
    public async Task Read_Headers_Differing_By_Case_Can_Be_Written_Back()
    {
        using var input = await CreateExcelManager().Create([new ExcelSheet
        {
            Name = "Items",
            Data = [new Dictionary<string, object?> { ["Name"] = "Item #1", ["Title"] = "x" }]
        }]);
        // rewrite the header row as Name, name
        using var workbook = new ClosedXML.Excel.XLWorkbook(input.GetStream()!);
        workbook.Worksheet(1).Cell(1, 2).Value = "name";
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        var sheet = (await CreateExcelManager().Read(ms.ToArray().ToBinaryFile())).Single();
        var row = (IDictionary<string, object?>)sheet.Data!.Single();
        Assert.That(row.Keys, Is.EqualTo(new[] { "Name", "name_2" }).AsCollection);

        using var output = await CreateExcelManager().Create([sheet]);
        var reread = (IDictionary<string, object?>)(await CreateExcelManager().Read(output.ToBinaryFile())).Single().Data!.Single();
        Assert.That(reread["name_2"], Is.EqualTo("x"));
    }
}
