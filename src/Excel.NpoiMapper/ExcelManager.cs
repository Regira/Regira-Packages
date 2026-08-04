using Npoi.Mapper;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Excel.Abstractions;
using Regira.Office.Excel.Models;
using Regira.Office.MimeTypes;
using Regira.Utilities;

namespace Regira.Office.Excel.NpoiMapper;

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
        var mapper = new Mapper(ms);
        var sheetCount = mapper.Workbook.NumberOfSheets;
        for (var i = 0; i < sheetCount; i++)
        {
            var sheetName = mapper.Workbook.GetSheetName(i);
            yield return new ExcelSheet
            {
                Name = sheetName,
                Data = mapper.Take<object>(i).Select(r => r.Value).ToList()
            };
        }
    }
    public Task<IMemoryFile> Create(IEnumerable<ExcelSheet> sheets, CancellationToken cancellationToken = default)
    {
        var workbook = new XSSFWorkbook();
        var mapper = new Mapper(workbook);
        var sheetIndex = 0;
        foreach (var sheet in sheets)
        {
            var sheetName = sheet.Name ?? $"Sheet-{++sheetIndex}";
            if (sheet.Data?.FirstOrDefault() is IDictionary<string, object>)
            {
                var xlsSheet = workbook.CreateSheet(sheetName);
                var dicData = sheet.Data.Select(d => DictionaryUtility.ToDictionary(d)).ToList();
                FillSheet(xlsSheet, dicData);
            }
            else
            {
                mapper.Put(sheet.Data, sheetName);
            }
        }

        var ms = new MemoryStream();
        workbook.Write(ms, true);
        return Task.FromResult<IMemoryFile>(ms.ToMemoryFile(ContentTypes.XLSX));
    }

    protected void FillSheet(ISheet sheet, IList<IDictionary<string, object?>> data)
    {
        if (!data.Any())
        {
            return;
        }

        var keys = data.SelectMany(dic => dic.Keys).Distinct().ToArray();
        var keysWithType = keys.ToDictionary(key => key, key =>
        {
            var firstValidItem = data.FirstOrDefault(d => d.ContainsKey(key) && d[key] != null);
            return firstValidItem?[key]?.GetType();
        });

        ICellStyle? dateCellStyle = null;

        var headers = sheet.CreateRow(0);
        for (var r = 0; r < data.Count; r++)
        {
            var row = sheet.CreateRow(r + 1);
            for (var c = 0; c < keys.Length; c++)
            {
                var key = keys[c];
                if (r == 0)
                {
                    // headers
                    var cell = headers.CreateCell(c);
                    cell.SetCellValue(key);
                }
                if (data[r].ContainsKey(key))
                {
                    var cell = row.CreateCell(c);
                    var value = data[r][key];

                    if (value != null)
                    {
                        var propertyType = keysWithType[key];
                        var simplePropertyType = TypeUtility.GetSimpleType(propertyType!);
                        if (simplePropertyType == typeof(DateTime))
                        {
                            if (dateCellStyle == null)
                            {
                                dateCellStyle = sheet.Workbook.CreateCellStyle();
                                dateCellStyle.DataFormat = sheet.Workbook.CreateDataFormat().GetFormat(_options.DateFormat);
                            }
                            cell.CellStyle = dateCellStyle;
                            cell.SetCellValue((DateTime)value);
                        }
                        else if (simplePropertyType.IsNumeric())
                        {
                            cell.SetCellValue(Convert.ToDouble(value));
                        }
                        else if (new[] { typeof(bool) }.Contains(simplePropertyType))
                        {
                            cell.SetCellValue((bool)value);
                        }
                        else //if (new[] { typeof(string), typeof(char) }.Contains(simplePropertyType))
                        {
                            cell.SetCellValue(value.ToString());
                        }
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
        var sheets = ReadCore(input, headers).ToList();
        return Task.FromResult<IEnumerable<ExcelSheet<T>>>(sheets);
    }
    private IEnumerable<ExcelSheet<T>> ReadCore(IBinaryFile input, string[]? headers = null)
    {
        using var ms = input.GetStream();
        var mapper = new Mapper(ms);
        var sheetCount = mapper.Workbook.NumberOfSheets;
        for (var i = 0; i < sheetCount; i++)
        {
            var sheetName = mapper.Workbook.GetSheetName(i);
            yield return new ExcelSheet<T>
            {
                Name = sheetName,
                Data = mapper.Take<T>(i).Select(r => r.Value).ToList()
            };
        }
    }
    public Task<IMemoryFile> Create(IEnumerable<ExcelSheet<T>> sheets, CancellationToken cancellationToken = default)
    {
        var ms = new MemoryStream();
        var mapper = new Mapper();
        var sheetIndex = 0;
        foreach (var sheet in sheets)
        {
            var sheetName = sheet.Name ?? $"Sheet-{++sheetIndex}";
            mapper.Put(sheet.Data, sheetName);
        }
        mapper.Save(ms, true);
        return Task.FromResult<IMemoryFile>(ms.ToMemoryFile(ContentTypes.XLSX));
    }
}