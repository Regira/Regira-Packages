using Office.Excel.Testing.Models;
using Regira.Office.Excel.Abstractions;
using Regira.Office.Excel.NpoiMapper;

namespace Office.Excel.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class NpoiMapperTests
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
    public Task Create_Rows_With_Different_Keys() => CreateExcelManager()
        .Run_Create_Rows_With_Different_Keys();
    [Test]
    public Task Create_Mixed_Value_Types() => CreateExcelManager()
        .Run_Create_Mixed_Value_Types();
    // no Create_Nested_Object: object rows go to Npoi.Mapper, which leaves out a property holding an object (docs/excel)
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
    public Task Create_Typed_Empty_Sheets() => CreateTypedExcelManager()
        .Run_Create_Typed_Empty_Sheets();
}
