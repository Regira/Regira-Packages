using Regira.Office.Excel.Abstractions;
using Regira.Office.Excel.EPPlus;

namespace Office.Excel.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class EPPlustTests
{
    IExcelService CreateExcelManager() => new ExcelManager();
    //IExcelService<ExcelCountry> CreateTypedExcelManager() => new ExcelManager<ExcelCountry>();

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
    // EPPlus reads `headers` as the keys of the columns, in order, and the first row as data
    [Test]
    public async Task Read_Headers_Name_The_Columns()
    {
        var input = ExcelTestExtensions.BuildWorkbook(("Items", [
            ["Title", "Value", "Created"],
            ["Item #1", 1, null]
        ]));

        var rows = (await CreateExcelManager().Read(input, ["Name", "Name", ""])).Single().Data!
            .Cast<IDictionary<string, object?>>()
            .ToList();
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows[0].Keys, Is.EqualTo(new[] { "Name", "Name_2", "Column3" }).AsCollection, "Repeated and blank keys are made unique as headers are");
        Assert.Multiple(() =>
        {
            Assert.That(rows[0]["Name"], Is.EqualTo("Title"));
            Assert.That(rows[1]["Name"], Is.EqualTo("Item #1"));
            Assert.That(Convert.ToDouble(rows[1]["Name_2"]), Is.EqualTo(1));
        });
    }
}
