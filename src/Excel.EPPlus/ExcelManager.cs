using System.Data;
using OfficeOpenXml;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Excel.Abstractions;
using Regira.Office.Excel.Models;
using Regira.Office.MimeTypes;
using Regira.Utilities;

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

    private readonly string _dateFormat;
    private readonly Func<string, string, object, object>? _transformData;
    public ExcelManager(Options? options = null)
    {
        options ??= new Options();
        _dateFormat = options.DateFormat;
        _transformData = options.TransformData;
    }


    public Task<IEnumerable<ExcelSheet>> Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default)
    {
        var sheets = ReadCore(input, headers).ToList();
        return Task.FromResult<IEnumerable<ExcelSheet>>(sheets);
    }
    private IEnumerable<ExcelSheet> ReadCore(IBinaryFile input, string[]? headers = null)
    {
        var headersOnFirstRow = headers == null;
        using var stream = input.GetStream();
        using var package = new ExcelPackage(stream);
        var sheets = package.Workbook.Worksheets.ToList();
        foreach (var sheet in sheets)
        {
            var cells = sheet.Cells;
            var numberOfColumns = sheet.Cells.Max(cell => (int)cell.Address[0]) - 65 + 1;
            //var numberOfRows = sheet.Cells
            //    .Select(cell => cell.Start.Row)
            //    .Max();
            var numberOfRows = sheet.Dimension.Rows;

            var collectionDic = new List<object>(numberOfRows - 1);
            var firstRow = 1;
            if (headersOnFirstRow)
            {
                headers = cells.ReadHeaders(numberOfColumns);
                firstRow = 2;
            }
            for (var row = firstRow; row <= numberOfRows; row++)
            {
                var rowDic = cells.Read(headers!, row);
                collectionDic.Add(rowDic);
            }

            yield return new ExcelSheet { Name = sheet.Name, Data = collectionDic };
        }
    }

    public IMemoryFile Create(ExcelSheet sheet)
    {
        return Create([sheet], CancellationToken.None).GetAwaiter().GetResult();
    }
    public IMemoryFile Create(DataSet dataSet)
    {
        using var package = new ExcelPackage();
        foreach (DataTable table in dataSet.Tables)
        {
            var sheet = package.Workbook.Worksheets.Add(table.TableName);
            if (table.Columns.Count > 0)
            {
                sheet.Cells["A1"].LoadFromDataTable(table, true);
                for (var i = 0; i < table.Columns.Count; i++)
                {
                    var cell = (char)(65 + i);
                    var column = table.Columns[i];
                    var columnType = column.DataType;
                    if (columnType == typeof(DateTime))
                    {
                        sheet.Cells[$"{cell}:{cell}"].Style.Numberformat.Format = _dateFormat;
                    }
                }
            }

            sheet.Cells.AutoFitColumns();
        }

        package.Save();
        var ms = new MemoryStream();
        package.Stream.Position = 0;
        package.Stream.CopyTo(ms);
        ms.Position = 0;
        return ms.ToMemoryFile(ContentTypes.XLSX);
    }
    public Task<IMemoryFile> Create(IEnumerable<ExcelSheet> sheets, CancellationToken cancellationToken = default)
    {
        using var package = new ExcelPackage();
        var i = 1;
        foreach (var sheet in sheets)
        {
            var excelSheet = package.Workbook.Worksheets.Add(sheet.Name ?? $"Sheet {i++}");
            var firstItem = sheet.Data?.FirstOrDefault();
            if (firstItem is IDictionary<string, object>)
            {
                FillSheet(excelSheet, sheet.Data!.Cast<IDictionary<string, object?>>().AsList());
            }
            else
            {
                FillSheet(excelSheet, sheet.Data.AsList());
            }
        }

        package.Save();
        var ms = new MemoryStream();
        package.Stream.Position = 0;
        package.Stream.CopyTo(ms);
        ms.Position = 0;
        return Task.FromResult<IMemoryFile>(ms.ToMemoryFile(ContentTypes.XLSX));
    }

    protected void FillSheet(ExcelWorksheet sheet, IList<IDictionary<string, object?>> data)
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

        for (var row = 0; row < data.Count; row++)
        {
            for (var col = 0; col < keys.Length; col++)
            {
                var colName = (char)(col + 65);
                var key = keys[col];
                if (row == 0)
                {
                    // headers
                    sheet.Cells[$"{colName}{row + 1}"].Value = key;
                }

                if (data[row].ContainsKey(key))
                {
                    var cell = sheet.Cells[$"{colName}{row + 2}"];
                    var value = data[row][key];
                    value = _transformData?.Invoke($"{colName}{row + 2}", key, value!) ?? value;
                    cell.Value = value;
                    var propertyType = keysWithType[key];
                    if (TypeUtility.GetSimpleType(propertyType!) == typeof(DateTime))
                    {
                        cell.Style.Numberformat.Format = _dateFormat;
                    }
                }
            }
        }
    }
    protected void FillSheet(ExcelWorksheet sheet, IList<object> data)
    {
        if (!data.Any())
        {
            return;
        }

        var firstItem = data.First();
        var properties = firstItem.GetType().GetProperties();

        for (var row = 0; row < data.Count; row++)
        {
            for (var col = 0; col < properties.Length; col++)
            {
                var colName = (char)(col + 65);
                var prop = properties[col];
                if (row == 0)
                {
                    // headers
                    sheet.Cells[$"{colName}{row + 1}"].Value = prop.Name;
                }

                var cell = sheet.Cells[$"{colName}{row + 2}"];
                var value = prop.GetValue(data[row]);
                value = _transformData?.Invoke($"{colName}{row + 2}", prop.Name, value!) ?? value;
                cell.Value = value;
                if (TypeUtility.GetSimpleType(prop.PropertyType) == typeof(DateTime))
                {
                    cell.Style.Numberformat.Format = _dateFormat;
                }
            }
        }
    }
}