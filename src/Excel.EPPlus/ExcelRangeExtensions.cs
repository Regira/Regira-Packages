using OfficeOpenXml;

namespace Regira.Office.Excel.EPPlus;

internal static class ExcelRangeExtensions
{
    public static object? Read(this ExcelRange cell)
    {
        var value = cell.Value;
        if (value is double date && (cell.Style?.Numberformat?.Format?.Contains("yyyy") ?? false))
        {
            // assume value is a date
            value = DateTime.FromOADate(date);
        }
        return value;
    }
}
