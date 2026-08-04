using System.Dynamic;
using ClosedXML.Excel;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Excel.Abstractions;
using Regira.Office.Excel.Models;

namespace Regira.Office.Excel.ClosedXML;

public class ExcelManager : IExcelService
{
    public class Options
    {
        public string DateFormat { get; set; } = "yyyy-MM-dd hh:mm:ss";
    }

    private readonly string _dateFormat;
    public ExcelManager(Options? options = null)
    {
        options ??= new Options();
        _dateFormat = options.DateFormat;
    }


    public Task<IEnumerable<ExcelSheet>> Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default)
    {
        var sheets = ReadCore(input, headers).ToList();
        return Task.FromResult<IEnumerable<ExcelSheet>>(sheets);
    }
    private IEnumerable<ExcelSheet> ReadCore(IBinaryFile input, string[]? headers = null)
    {
        using var ms = input.GetStream();
        using var wb = new XLWorkbook(ms);
        foreach (var sheet in wb.Worksheets)
        {
            var data = new List<object>();

            var rows = sheet.RangeUsed().RowsUsed().Skip(1);
            var sheetHeaders = sheet.Row(1).Cells().Select((c, i) => GetValue(c)?.ToString() ?? $"Column{i + 1}").ToArray();

            foreach (var row in rows)
            {
                var item = new Dictionary<string, object?>();
                row.CellCount();
                for (var c = 0; c < sheetHeaders.Length; c++)
                {
                    var key = sheetHeaders[c];
                    if (headers?.Any(h => h.Equals(key, StringComparison.InvariantCultureIgnoreCase)) ?? true)
                    {
                        item[key] = GetValue(row.Cell(c + 1));
                    }
                }
                data.Add(item);
            }

            yield return new ExcelSheet
            {
                Name = sheet.Name,
                Data = data
            };
        }
    }
    private object? GetValue(IXLCell cell)
    {
        var value = cell.Value;
        var type = cell.Value.Type;
        return type switch
        {
            XLDataType.Blank => null,
            XLDataType.Boolean => value.GetBoolean(),
            XLDataType.Number => value.GetNumber(),
            XLDataType.Text => value.GetText(),
            XLDataType.Error => value.GetError(),
            XLDataType.DateTime => value.GetDateTime(),
            XLDataType.TimeSpan => value.GetTimeSpan(),
            _ => throw new InvalidCastException(),
        };
    }

    public Task<IMemoryFile> Create(IEnumerable<ExcelSheet> sheets, CancellationToken cancellationToken = default)
    {
        using var wb = new XLWorkbook();
        var sheetIndex = 0;
        foreach (var sheet in sheets)
        {
            var headers = GetHeaders(sheet.Data.FirstOrDefault());
            var ws = wb.AddWorksheet(sheet.Name ?? $"Sheet-{++sheetIndex}");
            for (var i = 0; i < headers.Length; i++)
            {
                ws.Row(1).Cell(i + 1).Value = headers[i];
            }

            var data = sheet.Data;
            var firstItem = sheet.Data.Skip(1).FirstOrDefault();
            if (firstItem is IDictionary<string, object?>)
            {
                data = sheet.Data.Select(dic => ((IDictionary<string, object?>)dic).Values).ToArray();
            }
            ws.Cell("A2").InsertData(data);
        }

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        return Task.FromResult<IMemoryFile>(ms.ToMemoryFile());
    }

    string[] GetHeaders(object item)
    {
        if (item == null)
        {
            return [];
        }
        if (item is IDictionary<string, object?> dic)
        {
            return dic.Keys.ToArray();
        }
        return item.GetType().GetProperties().Select(p => p.Name).ToArray();
    }
    object ToAnonymousObject(IDictionary<string, object?> dictionary)
    {
        var expando = new ExpandoObject() as IDictionary<string, object?>;

        foreach (var kvp in dictionary)
        {
            expando[kvp.Key] = kvp.Value;
        }

        return expando;
    }
}