using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using MiniExcelLibs.OpenXml;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Excel.Abstractions;
using Regira.Office.Excel.Internal;
using Regira.Office.Excel.Models;
using Regira.Office.MimeTypes;

namespace Regira.Office.Excel.MiniExcel;

public class ExcelManager(ExcelManager.Options? options = null) : IExcelService
{
    private readonly Options _options = options ?? new Options();
    public class Options
    {
        /// <summary>
        /// Excel number format for the columns whose values are all <see cref="DateTime"/>s, in every sheet.
        /// <c>null</c> (default) keeps Excel's built-in date format.
        /// </summary>
        public string? DateFormat { get; set; }
    }


    /// <summary>
    /// Reads every sheet, using its first row as the keys of the rows below it.
    /// </summary>
    /// <param name="input">The workbook</param>
    /// <param name="headers">When supplied, only the columns with these headers are returned (case-insensitive)</param>
    /// <param name="cancellationToken"></param>
    public Task<IEnumerable<ExcelSheet>> Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default)
    {
        using var stream = input.GetStream()
            ?? throw new ArgumentException("The input file has no content.", nameof(input));
        var sheets = stream.GetSheetNames()
            .Select(sheetName => new ExcelSheet
            {
                Name = sheetName,
                Data = ReadSheet(stream, sheetName, headers, cancellationToken)
            })
            .ToList();
        return Task.FromResult<IEnumerable<ExcelSheet>>(sheets);
    }
    private static List<object> ReadSheet(Stream stream, string sheetName, string[]? headers, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var data = new List<object>();
        IReadOnlyList<(string Column, string Key)>? columns = null;
        foreach (IDictionary<string, object?> row in stream.Query(useHeaderRow: false, sheetName: sheetName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (columns == null)
            {
                columns = row.Keys
                    .Zip(SheetHeaders.Keys(row.Values.ToList()), (column, key) => (column, key))
                    .Where(c => headers?.Contains(c.key, StringComparer.InvariantCultureIgnoreCase) ?? true)
                    .ToList();
                continue;
            }

            var item = new Dictionary<string, object?>(columns.Count);
            foreach (var (column, key) in columns)
            {
                item[key] = row.TryGetValue(column, out var value) ? value : null;
            }
            data.Add(item);
        }
        return data;
    }

    /// <summary>
    /// Writes each sheet with a header row holding every key its rows use, in the order they first appear.
    /// </summary>
    /// <exception cref="ArgumentException">A sheet name breaks Excel's rules or is used more than once</exception>
    public Task<IMemoryFile> Create(IEnumerable<ExcelSheet> sheets, CancellationToken cancellationToken = default)
    {
        var sheetList = sheets.ToList();
        var sheetNames = SheetNames.Resolve(sheetList.Select(s => s.Name).ToList());
        // MiniExcel formats a column by its exact key, in every sheet, so a key gets DateFormat
        // only when all its values are DateTimes (true while only DateTimes were seen)
        var dateKeys = new Dictionary<string, bool>(StringComparer.Ordinal);

        var miniSheets = new Dictionary<string, object>();
        for (var i = 0; i < sheetList.Count; i++)
        {
            var rows = SheetRows.ToDictionaries(sheetList[i].Data);
            // every key, written as it is first spelled
            var keys = new List<string>();
            var spellings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var (key, value) in row)
                {
                    if (!spellings.TryGetValue(key, out var column))
                    {
                        column = key;
                        spellings.Add(key, column);
                        keys.Add(column);
                    }
                    if (value != null)
                    {
                        dateKeys[column] = value is DateTime && dateKeys.GetValueOrDefault(column, true);
                    }
                }
            }
            // every row gets every key, or MiniExcel misaligns or drops the columns a row lacks;
            // projected while MiniExcel writes, so the rows are not held twice
            miniSheets.Add(sheetNames[i], rows
                .Select(row => keys.ToDictionary(key => key, key => row.TryGetValue(key, out var value) ? value : null)));
        }

        var config = new OpenXmlConfiguration
        {
            EnableWriteNullValueCell = true,
            DynamicColumns = _options.DateFormat == null
                ? null
                : dateKeys
                    .Where(x => x.Value)
                    .Select(x => new DynamicExcelColumn(x.Key) { Format = _options.DateFormat })
                    .ToArray()
        };
        var ms = new MemoryStream();
        ms.SaveAs(miniSheets, configuration: config);
        ms.Position = 0;

        return Task.FromResult<IMemoryFile>(ms.ToMemoryFile(ContentTypes.XLSX));
    }
}

public class ExcelManager<T> : IExcelService<T>
    where T : class, new()
{
    /// <summary>
    /// Reads every sheet into <typeparamref name="T"/>, matching the first row's headers to its properties.
    /// </summary>
    /// <param name="input">The workbook</param>
    /// <param name="headers">Ignored: the properties of <typeparamref name="T"/> select the columns</param>
    /// <param name="cancellationToken"></param>
    public Task<IEnumerable<ExcelSheet<T>>> Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default)
    {
        using var stream = input.GetStream()
            ?? throw new ArgumentException("The input file has no content.", nameof(input));
        var sheets = stream.GetSheetNames()
            .Select(sheetName =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ExcelSheet<T>
                {
                    Name = sheetName,
                    Data = stream.Query<T>(sheetName: sheetName).ToList()
                };
            })
            .ToList();
        return Task.FromResult<IEnumerable<ExcelSheet<T>>>(sheets);
    }

    /// <summary>
    /// Writes each sheet with a header row from the properties of <typeparamref name="T"/>, also when it has no rows.
    /// </summary>
    /// <exception cref="ArgumentException">A sheet name breaks Excel's rules or is used more than once</exception>
    public Task<IMemoryFile> Create(IEnumerable<ExcelSheet<T>> sheets, CancellationToken cancellationToken = default)
    {
        var sheetList = sheets.ToList();
        var sheetNames = SheetNames.Resolve(sheetList.Select(s => s.Name).ToList());
        var miniSheets = new Dictionary<string, object>();
        for (var i = 0; i < sheetList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            miniSheets.Add(sheetNames[i], sheetList[i].Data ?? new List<T>());
        }

        var ms = new MemoryStream();
        ms.SaveAs(miniSheets);
        ms.Position = 0;

        return Task.FromResult<IMemoryFile>(ms.ToMemoryFile(ContentTypes.XLSX));
    }
}
