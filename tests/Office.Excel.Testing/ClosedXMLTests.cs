using ClosedXML.Excel;
using Regira.IO.Extensions;
using Regira.Office.Excel.Abstractions;
using Regira.Office.Excel.ClosedXML;
using Regira.Office.Excel.Models;

namespace Office.Excel.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class ClosedXMLTests
{
    IExcelService CreateExcelManager() => new ExcelManager();
    //IExcelService<ExcelCountry> CreateTypedExcelManager() => new Regira.Office.Excel.ClosedXML.ExcelManager<ExcelCountry>();

    //[Test]
    //public Task List_To_Excel() => CreateTypedExcelManager()
    //    .Run_List_To_Excel();
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

    //[Test]
    //public Task Export_Countries() => CreateTypedExcelManager()
    //    .Run_Export_Countries();

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
    public Task Create_Rows_With_Different_Keys() => CreateExcelManager()
        .Run_Create_Rows_With_Different_Keys();
    [Test]
    public Task Create_Mixed_Value_Types() => CreateExcelManager()
        .Run_Create_Mixed_Value_Types();
    [Test]
    public Task Create_Nested_Object() => CreateExcelManager()
        .Run_Create_Nested_Object();
    [Test]
    public Task Create_Keys_Differing_By_Case() => CreateExcelManager()
        .Run_Create_Keys_Differing_By_Case();
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
    public async Task Create_Applies_DateFormat()
    {
        var created = new DateTime(2026, 10, 7, 14, 30, 0);
        ExcelSheet Sheet() => new() { Name = "Items", Data = [new Dictionary<string, object?> { ["Created"] = created }] };

        using var formatted = await new ExcelManager(new ExcelManager.Options { DateFormat = "dd/MM/yyyy" }).Create([Sheet()]);
        using var unformatted = await CreateExcelManager().Create([Sheet()]);

        using var formattedBook = new XLWorkbook(formatted.GetStream()!);
        using var unformattedBook = new XLWorkbook(unformatted.GetStream()!);
        var cell = formattedBook.Worksheet(1).Cell(2, 1);
        Assert.Multiple(() =>
        {
            Assert.That(cell.Style.NumberFormat.Format, Is.EqualTo("dd/MM/yyyy"));
            Assert.That(cell.GetValue<DateTime>(), Is.EqualTo(created), "still a date cell, not text");
            Assert.That(unformattedBook.Worksheet(1).Cell(2, 1).Style.NumberFormat.Format, Is.Not.EqualTo("dd/MM/yyyy"));
        });
    }
}
