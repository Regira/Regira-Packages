using Npoi.Mapper;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Excel.Abstractions;
using Regira.Office.Excel.Internal;
using Regira.Office.Excel.Models;
using Regira.Office.MimeTypes;

namespace Regira.Office.Excel.NpoiMapper;

public class ExcelManager(ExcelManager.Options? options = null) : IExcelService
{
    private readonly Options _options = options ?? new Options();
    public class Options
    {
        public string DateFormat { get; set; } = "yyyy-MM-dd hh:mm:ss";
    }


    /// <summary>
    /// Reads every sheet, using its first row as the keys of the rows below it. Rows that were never written are skipped.
    /// </summary>
    /// <param name="input">The workbook</param>
    /// <param name="headers">When supplied, only the columns with these headers are returned (case-insensitive)</param>
    /// <param name="cancellationToken"></param>
    public Task<IEnumerable<ExcelSheet>> Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default)
    {
        using var ms = input.GetStream()
            ?? throw new ArgumentException("The input file has no content.", nameof(input));
        var workbook = WorkbookFactory.Create(ms);
        var sheets = Enumerable.Range(0, workbook.NumberOfSheets)
            .Select(i => workbook.GetSheetAt(i))
            .Select(sheet => new ExcelSheet
            {
                Name = sheet.SheetName,
                Data = ReadSheet(sheet, headers, cancellationToken)
            })
            .ToList();
        return Task.FromResult<IEnumerable<ExcelSheet>>(sheets);
    }
    private static List<object> ReadSheet(ISheet sheet, string[]? headers, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var data = new List<object>();
        var rows = Enumerable.Range(1, Math.Max(sheet.LastRowNum, 0))
            .Select(sheet.GetRow)
            .Where(row => row != null)
            .ToList();
        var headerRow = sheet.GetRow(0);
        var columnCount = rows.Append(headerRow)
            .Select(row => (int?)row?.LastCellNum ?? 0)
            .DefaultIfEmpty()
            .Max();
        if (columnCount <= 0)
        {
            return data;
        }

        var columns = SheetHeaders.Keys(Enumerable.Range(0, columnCount).Select(c => GetValue(headerRow?.GetCell(c))).ToList())
            .Select((key, index) => (Index: index, Key: key))
            .Where(c => headers?.Contains(c.Key, StringComparer.InvariantCultureIgnoreCase) ?? true)
            .ToList();
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new Dictionary<string, object?>(columns.Count);
            foreach (var (index, key) in columns)
            {
                item[key] = GetValue(row.GetCell(index));
            }
            data.Add(item);
        }
        return data;
    }
    private static object? GetValue(ICell? cell)
    {
        if (cell == null)
        {
            return null;
        }
        var type = cell.CellType == CellType.Formula ? cell.CachedFormulaResultType : cell.CellType;
        return type switch
        {
            CellType.Boolean => cell.BooleanCellValue,
            CellType.String => cell.StringCellValue,
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell) ? cell.DateCellValue : cell.NumericCellValue,
            CellType.Error => FormulaError.ForInt(cell.ErrorCellValue).String,
            _ => null
        };
    }

    /// <summary>
    /// Writes each sheet with a header row. Dictionaries get every key their rows use, in the order they first appear;
    /// objects are written by Npoi.Mapper.
    /// </summary>
    /// <exception cref="ArgumentException">A sheet name breaks Excel's rules or is used more than once</exception>
    public Task<IMemoryFile> Create(IEnumerable<ExcelSheet> sheets, CancellationToken cancellationToken = default)
    {
        var sheetList = sheets.ToList();
        var sheetNames = SheetNames.Resolve(sheetList.Select(s => s.Name).ToList());
        var workbook = new XSSFWorkbook();
        var mapper = new Mapper(workbook);
        for (var i = 0; i < sheetList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var data = sheetList[i].Data;
            if (data?.FirstOrDefault() is { } and not IDictionary<string, object>)
            {
                mapper.Put(data, sheetNames[i]);
            }
            else
            {
                var xlsSheet = workbook.CreateSheet(sheetNames[i]);
                FillSheet(xlsSheet, SheetRows.ToDictionaries(data));
            }
        }

        var ms = new MemoryStream();
        workbook.Write(ms, true);
        ms.Position = 0;
        return Task.FromResult<IMemoryFile>(ms.ToMemoryFile(ContentTypes.XLSX));
    }

    protected void FillSheet(ISheet sheet, IList<IDictionary<string, object?>> data)
    {
        if (!data.Any())
        {
            return;
        }

        var keys = SheetRows.Keys(data);
        ICellStyle? dateCellStyle = null;

        var headers = sheet.CreateRow(0);
        for (var c = 0; c < keys.Count; c++)
        {
            headers.CreateCell(c).SetCellValue(keys[c]);
        }
        for (var r = 0; r < data.Count; r++)
        {
            var row = sheet.CreateRow(r + 1);
            for (var c = 0; c < keys.Count; c++)
            {
                var key = keys[c];
                if (data[r].TryGetValue(key, out var value))
                {
                    var cell = row.CreateCell(c);

                    // each value by its own type: a key can hold a date in one row and text in the next
                    if (value is DateTime date)
                    {
                        if (dateCellStyle == null)
                        {
                            dateCellStyle = sheet.Workbook.CreateCellStyle();
                            dateCellStyle.DataFormat = sheet.Workbook.CreateDataFormat().GetFormat(_options.DateFormat);
                        }
                        cell.CellStyle = dateCellStyle;
                        cell.SetCellValue(date);
                    }
                    else if (value is bool boolean)
                    {
                        cell.SetCellValue(boolean);
                    }
                    else if (value != null && value.GetType().IsNumeric())
                    {
                        cell.SetCellValue(Convert.ToDouble(value));
                    }
                    else if (value != null)
                    {
                        cell.SetCellValue(value.ToString());
                    }
                }
            }
        }
    }
}

public class ExcelManager<T> : IExcelService<T>
    where T : class, new()
{
    public Task<IEnumerable<ExcelSheet<T>>> Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default)
    {
        var sheets = ReadCore(input, headers, cancellationToken).ToList();
        return Task.FromResult<IEnumerable<ExcelSheet<T>>>(sheets);
    }
    private IEnumerable<ExcelSheet<T>> ReadCore(IBinaryFile input, string[]? headers, CancellationToken cancellationToken)
    {
        using var ms = input.GetStream();
        var mapper = new Mapper(ms);
        var sheetCount = mapper.Workbook.NumberOfSheets;
        for (var i = 0; i < sheetCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sheetName = mapper.Workbook.GetSheetName(i);
            yield return new ExcelSheet<T>
            {
                Name = sheetName,
                Data = mapper.Take<T>(i).Select(r => r.Value).ToList()
            };
        }
    }
    /// <summary>
    /// Writes each sheet with a header row from the properties of <typeparamref name="T"/>, also when it has no rows.
    /// </summary>
    /// <exception cref="ArgumentException">A sheet name breaks Excel's rules or is used more than once</exception>
    public Task<IMemoryFile> Create(IEnumerable<ExcelSheet<T>> sheets, CancellationToken cancellationToken = default)
    {
        var sheetList = sheets.ToList();
        var sheetNames = SheetNames.Resolve(sheetList.Select(s => s.Name).ToList());
        var ms = new MemoryStream();
        var mapper = new Mapper();
        for (var i = 0; i < sheetList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mapper.Put(sheetList[i].Data ?? new List<T>(), sheetNames[i]);
        }
        mapper.Save(ms, true);
        ms.Position = 0;
        return Task.FromResult<IMemoryFile>(ms.ToMemoryFile(ContentTypes.XLSX));
    }
}
