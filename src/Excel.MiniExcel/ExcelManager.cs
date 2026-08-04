using MiniExcelLibs;
using MiniExcelLibs.OpenXml;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Excel.Abstractions;
using Regira.Office.Excel.Models;
using Regira.Office.MimeTypes;
using Regira.Utilities;

namespace Regira.Office.Excel.MiniExcel;

public class ExcelManager(ExcelManager.Options? options = null) : IExcelService
{
    private readonly Options _options = options ?? new Options();
    public class Options
    {
        public string DateFormat { get; set; } = "yyyy-MM-dd hh:mm:ss";
    }


    public Task<IEnumerable<ExcelSheet>> Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default)
    {
        var sheets = ReadCore(input, headers).ToList();
        return Task.FromResult<IEnumerable<ExcelSheet>>(sheets);
    }
    private IEnumerable<ExcelSheet> ReadCore(IBinaryFile input, string[]? headers = null)
    {
        using var ms = input.GetStream();
        var sheetNames = ms.GetSheetNames();

        foreach (var sheetName in sheetNames)
        {
            var rows = ms.Query(sheetName: sheetName)
                .Cast<IDictionary<string, object>>();
            // ReSharper disable once PossibleMultipleEnumeration
            var miniHeaders = rows.First();
            var duplicateHeaders = miniHeaders
                .GroupBy(v => v.Value)
                .Where(g => g.Count() > 1);
            foreach (var duplicateHeader in duplicateHeaders)
            {
                var duplicateHeaderItems = duplicateHeader.ToList();
                for (var i = 0; i < duplicateHeaderItems.Count; i++)
                {
                    if (i > 0)
                    {
                        miniHeaders[duplicateHeaderItems[i].Key] = $"{duplicateHeaderItems[i].Value}_{i + 1}";
                    }
                }
            }
            yield return new ExcelSheet
            {
                Name = sheetName,
                // ReSharper disable once PossibleMultipleEnumeration
                Data = rows
                    .Skip(1)
                    .Select(d =>
                    {
                        var keys = d.Keys.ToArray();
                        return d.Keys.ToDictionary(key => miniHeaders[key].ToString()!, key => d[key]) as object;
                    })
                    .ToList()
            };
        }
    }

    public Task<IMemoryFile> Create(IEnumerable<ExcelSheet> sheets, CancellationToken cancellationToken = default)
    {
        var ms = new MemoryStream();
        var sheetIndex = 0;
        var miniSheets = new Dictionary<string, object>();
        foreach (var sheet in sheets)
        {
            var sheetName = sheet.Name ?? $"Sheet-{++sheetIndex}";
            var dics = sheet.Data
                !.Select(x => DictionaryUtility.ToDictionary(x))
                .ToArray();
            var dicSize = dics.Max(d => d.Count);
            var dicHeaders = dics
                .First(d => d.Count == dicSize)
                .Keys;
            var rightDics = dics
                .Select(d =>
                {
                    if (d.Count < dicSize)
                    {
                        // make sure all dictionaries have the same keys (or MiniExcel.Save will throw an exception)
                        return dicHeaders.ToDictionary(h => h, h => d.TryGetValue(h, out var value) ? value : null);
                    }

                    return d;
                });
            miniSheets.Add(sheetName, rightDics);
        }

        var config = new OpenXmlConfiguration()
        {
            EnableWriteNullValueCell = true,
            IgnoreTemplateParameterMissing = true
        };
        ms.SaveAs(miniSheets, configuration: config);

        return Task.FromResult<IMemoryFile>(ms.ToMemoryFile(ContentTypes.XLSX));
    }
}

public class ExcelManager<T> : IExcelService<T>
    where T : class, new()
{
    public Task<IEnumerable<ExcelSheet<T>>> Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default)
    {
        var sheets = ReadCore(input, headers).ToList();
        return Task.FromResult<IEnumerable<ExcelSheet<T>>>(sheets);
    }
    private IEnumerable<ExcelSheet<T>> ReadCore(IBinaryFile input, string[]? headers = null)
    {
        using var ms = input.GetStream();
        var sheetNames = ms.GetSheetNames();

        foreach (var sheetName in sheetNames)
        {
            var rows = ms.Query<T>(sheetName: sheetName);
            yield return new ExcelSheet<T>
            {
                Name = sheetName,
                Data = rows.ToList()
            };
        }
    }
    public Task<IMemoryFile> Create(IEnumerable<ExcelSheet<T>> sheets, CancellationToken cancellationToken = default)
    {
        var ms = new MemoryStream();
        var sheetIndex = 0;
        var miniSheets = new Dictionary<string, object>();
        foreach (var sheet in sheets)
        {
            var sheetName = sheet.Name ?? $"Sheet-{++sheetIndex}";
            miniSheets.Add(sheetName, sheet.Data ?? []);
        }

        ms.SaveAs(miniSheets);

        return Task.FromResult<IMemoryFile>(ms.ToMemoryFile(ContentTypes.XLSX));
    }
}