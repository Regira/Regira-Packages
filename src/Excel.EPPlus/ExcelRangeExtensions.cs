using OfficeOpenXml;
using Regira.Utilities;

namespace Regira.Office.Excel.EPPlus;

internal static class ExcelRangeExtensions
{
    public static string[] ReadHeaders(this ExcelRange cells, int maxCol)
    {
        var headers = new string[maxCol];
        for (var col = 1; col <= maxCol; col++)
        {
            var cell = cells[1, col];
            headers[col - 1] = cell.Text;
        }
        return headers;
    }
    public static IDictionary<string, object> Read(this ExcelRange cells, string[] headers, int row)
    {
        return headers
            .Select((h, i) => new { Key = h, Value = cells[row, i + 1].Read() })
            .ToDictionary(x => x.Key, x => x.Value, true);
    }

    public static object Read(this ExcelRange cell)
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