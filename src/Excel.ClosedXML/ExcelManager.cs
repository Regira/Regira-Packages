using ClosedXML.Excel;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Excel.Abstractions;
using Regira.Office.Excel.Internal;
using Regira.Office.Excel.Models;
using Regira.Office.MimeTypes;

namespace Regira.Office.Excel.ClosedXML;

public class ExcelManager : IExcelService
{
    public class Options
    {
        /// <summary>
        /// Excel number format for <see cref="DateTime"/> cells.
        /// <c>null</c> (default) keeps ClosedXML's date format.
        /// </summary>
        public string? DateFormat { get; set; }
    }

    private readonly string? _dateFormat;
    public ExcelManager(Options? options = null)
    {
        options ??= new Options();
        _dateFormat = options.DateFormat;
    }


    /// <summary>
    /// Reads every sheet, using its first row as the keys of the rows below it. Blank rows are skipped.
    /// </summary>
    /// <param name="input">The workbook</param>
    /// <param name="headers">When supplied, only the columns with these headers are returned (case-insensitive)</param>
    /// <param name="cancellationToken"></param>
    public Task<IEnumerable<ExcelSheet>> Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default)
    {
        using var ms = input.GetStream()
            ?? throw new ArgumentException("The input file has no content.", nameof(input));
        using var wb = new XLWorkbook(ms);
        var sheets = wb.Worksheets
            .Select(sheet => new ExcelSheet
            {
                Name = sheet.Name,
                Data = ReadSheet(sheet, headers, cancellationToken)
            })
            .ToList();
        return Task.FromResult<IEnumerable<ExcelSheet>>(sheets);
    }
    private List<object> ReadSheet(IXLWorksheet sheet, string[]? headers, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var data = new List<object>();
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastColumn == 0)
        {
            return data;
        }

        var headerRow = sheet.Row(1);
        var columns = SheetHeaders.Keys(Enumerable.Range(1, lastColumn).Select(c => GetValue(headerRow.Cell(c))).ToList())
            .Select((key, i) => (Number: i + 1, Key: key))
            .Where(c => headers?.Contains(c.Key, StringComparer.InvariantCultureIgnoreCase) ?? true)
            .ToList();

        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new Dictionary<string, object?>(columns.Count);
            foreach (var (number, key) in columns)
            {
                item[key] = GetValue(row.Cell(number));
            }
            data.Add(item);
        }
        return data;
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

    /// <summary>
    /// Writes each sheet with a header row holding every key its rows use, in the order they first appear.
    /// </summary>
    /// <exception cref="ArgumentException">A sheet name breaks Excel's rules or is used more than once</exception>
    public Task<IMemoryFile> Create(IEnumerable<ExcelSheet> sheets, CancellationToken cancellationToken = default)
    {
        var sheetList = sheets.ToList();
        var sheetNames = SheetNames.Resolve(sheetList.Select(s => s.Name).ToList());

        using var wb = new XLWorkbook();
        for (var i = 0; i < sheetList.Count; i++)
        {
            var ws = wb.AddWorksheet(sheetNames[i]);
            var rows = SheetRows.ToDictionaries(sheetList[i].Data);
            var keys = SheetRows.Keys(rows);
            for (var c = 0; c < keys.Count; c++)
            {
                ws.Cell(1, c + 1).Value = keys[c];
            }
            for (var r = 0; r < rows.Count; r++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var c = 0; c < keys.Count; c++)
                {
                    if (rows[r].TryGetValue(keys[c], out var value) && value != null)
                    {
                        var cell = ws.Cell(r + 2, c + 1);
                        cell.Value = ToCellValue(value);
                        if (value is DateTime && _dateFormat != null)
                        {
                            cell.Style.NumberFormat.Format = _dateFormat;
                        }
                    }
                }
            }
        }

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return Task.FromResult<IMemoryFile>(ms.ToMemoryFile(ContentTypes.XLSX));
    }

    private static XLCellValue ToCellValue(object value)
        => value switch
        {
            string text => text,
            bool boolean => boolean,
            DateTime date => date,
            TimeSpan time => time,
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => Convert.ToDouble(value),
            _ => value.ToString()
        };
}
