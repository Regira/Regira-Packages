using System.Data;
using OfficeOpenXml;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Excel.Abstractions;
using Regira.Office.Excel.Internal;
using Regira.Office.Excel.Models;
using Regira.Office.MimeTypes;

namespace Regira.Office.Excel.EPPlus;

public class ExcelManager : IExcelService
{
    public class Options
    {
        public string DateFormat { get; set; } = "yyyy/MM/dd";
        /// <summary>
        /// Function that accepts CellName (e.g. A1), key, value
        /// And returns value to be set
        /// </summary>
        public Func<string, string, object, object>? TransformData { get; set; }
    }

    private const string GeneratedSheetName = "Sheet {0}";
    private readonly string _dateFormat;
    private readonly Func<string, string, object, object>? _transformData;
    public ExcelManager(Options? options = null)
    {
        options ??= new Options();
        _dateFormat = options.DateFormat;
        _transformData = options.TransformData;
    }


    /// <summary>
    /// Reads every sheet. Without <paramref name="headers"/>, the first row holds the keys of the rows below it.
    /// </summary>
    /// <param name="input">The workbook</param>
    /// <param name="headers">When supplied, the keys of the columns, in order; the first row is then read as data</param>
    /// <param name="cancellationToken"></param>
    public Task<IEnumerable<ExcelSheet>> Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default)
    {
        using var stream = input.GetStream()
            ?? throw new ArgumentException("The input file has no content.", nameof(input));
        using var package = new ExcelPackage(stream);
        var sheets = package.Workbook.Worksheets
            .Select(sheet => new ExcelSheet { Name = sheet.Name, Data = ReadSheet(sheet, headers, cancellationToken) })
            .ToList();
        return Task.FromResult<IEnumerable<ExcelSheet>>(sheets);
    }
    private static List<object> ReadSheet(ExcelWorksheet sheet, string[]? headers, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var data = new List<object>();
        var dimension = sheet.Dimension;
        if (dimension == null)
        {
            return data;
        }

        var cells = sheet.Cells;
        var headersOnFirstRow = headers == null;
        var keys = SheetHeaders.Keys(headersOnFirstRow
            ? Enumerable.Range(1, dimension.End.Column).Select(col => (object?)cells[1, col].Text).ToList()
            : headers!.Cast<object?>().ToList());
        for (var row = headersOnFirstRow ? 2 : 1; row <= dimension.End.Row; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new Dictionary<string, object?>(keys.Length);
            for (var col = 0; col < keys.Length; col++)
            {
                item[keys[col]] = cells[row, col + 1].Read();
            }
            data.Add(item);
        }
        return data;
    }

    public IMemoryFile Create(ExcelSheet sheet)
    {
        return Create([sheet], CancellationToken.None).GetAwaiter().GetResult();
    }
    /// <exception cref="ArgumentException">A table name breaks Excel's rules for a sheet name or is used more than once</exception>
    public IMemoryFile Create(DataSet dataSet)
    {
        var tables = dataSet.Tables.Cast<DataTable>().ToList();
        var sheetNames = SheetNames.Resolve(tables.Select(t => (string?)t.TableName).ToList(), GeneratedSheetName);
        using var package = new ExcelPackage();
        for (var t = 0; t < tables.Count; t++)
        {
            var table = tables[t];
            var sheet = package.Workbook.Worksheets.Add(sheetNames[t]);
            if (table.Columns.Count > 0)
            {
                sheet.Cells["A1"].LoadFromDataTable(table, true);
                for (var i = 0; i < table.Columns.Count; i++)
                {
                    if (table.Columns[i].DataType == typeof(DateTime))
                    {
                        sheet.Column(i + 1).Style.Numberformat.Format = _dateFormat;
                    }
                }
            }

            sheet.Cells.AutoFitColumns();
        }

        return Save(package);
    }
    /// <summary>
    /// Writes each sheet with a header row holding every key its rows use, in the order they first appear.
    /// </summary>
    /// <exception cref="ArgumentException">A sheet name breaks Excel's rules or is used more than once</exception>
    public Task<IMemoryFile> Create(IEnumerable<ExcelSheet> sheets, CancellationToken cancellationToken = default)
    {
        var sheetList = sheets.ToList();
        var sheetNames = SheetNames.Resolve(sheetList.Select(s => s.Name).ToList(), GeneratedSheetName);
        using var package = new ExcelPackage();
        for (var i = 0; i < sheetList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var excelSheet = package.Workbook.Worksheets.Add(sheetNames[i]);
            FillSheet(excelSheet, SheetRows.ToDictionaries(sheetList[i].Data));
        }

        return Task.FromResult(Save(package));
    }
    private static IMemoryFile Save(ExcelPackage package)
    {
        package.Save();
        var ms = new MemoryStream();
        package.Stream.Position = 0;
        package.Stream.CopyTo(ms);
        ms.Position = 0;
        return ms.ToMemoryFile(ContentTypes.XLSX);
    }

    protected void FillSheet(ExcelWorksheet sheet, IList<IDictionary<string, object?>> data)
    {
        if (!data.Any())
        {
            return;
        }

        var keys = SheetRows.Keys(data);
        for (var col = 0; col < keys.Count; col++)
        {
            sheet.Cells[1, col + 1].Value = keys[col];
        }
        for (var row = 0; row < data.Count; row++)
        {
            for (var col = 0; col < keys.Count; col++)
            {
                var key = keys[col];
                if (data[row].TryGetValue(key, out var value))
                {
                    var cell = sheet.Cells[row + 2, col + 1];
                    value = _transformData?.Invoke(cell.Address, key, value!) ?? value;
                    cell.Value = value;
                    // each value by its own type: a key can hold a date in one row and a number in the next
                    if (value is DateTime)
                    {
                        cell.Style.Numberformat.Format = _dateFormat;
                    }
                }
            }
        }
    }
    protected void FillSheet(ExcelWorksheet sheet, IList<object> data)
        => FillSheet(sheet, SheetRows.ToDictionaries(data));
}
