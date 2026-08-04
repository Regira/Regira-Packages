# Regira Office.Excel — Examples

## Example 1: Read a workbook into typed objects

Use `MiniExcel.ExcelManager<T>` to map rows directly to a POCO without manual column mapping.

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
}

public async Task<IEnumerable<Product>> ImportProducts(byte[] excelBytes)
{
    IExcelService<Product> excel = new Regira.Office.Excel.MiniExcel.ExcelManager<Product>();

    var file   = excelBytes.ToBinaryFile();
    var sheets = await excel.Read(file);

    return sheets.FirstOrDefault()?.Data ?? [];
}
```

---

## Example 2: Write a multi-sheet workbook

Build multiple `ExcelSheet` objects and pass them to `Create()`.

```csharp
public async Task<IMemoryFile> ExportReport(IEnumerable<Order> orders, IEnumerable<Product> products)
{
    IExcelService excel = new Regira.Office.Excel.EPPlus.ExcelManager();

    var orderSheet = new ExcelSheet
    {
        Name = "Orders",
        Data = orders.Select(o => (object)new Dictionary<string, object?>
        {
            ["Id"]       = o.Id,
            ["Customer"] = o.CustomerName,
            ["Total"]    = o.Total,
            ["Date"]     = o.OrderDate
        }).ToList()
    };

    var productSheet = new ExcelSheet
    {
        Name = "Products",
        Data = products.Select(p => (object)new Dictionary<string, object?>
        {
            ["Id"]    = p.Id,
            ["Name"]  = p.Name,
            ["Price"] = p.Price
        }).ToList()
    };

    return await excel.Create([orderSheet, productSheet]);
}
```

---

## Example 3: EPPlus — per-cell value transformation

Use `TransformData` to round decimals or format values during write.

```csharp
var excel = new Regira.Office.Excel.EPPlus.ExcelManager(new()
{
    DateFormat    = "dd/MM/yyyy",
    TransformData = (cellAddress, key, value) => key switch
    {
        "Price"    => Math.Round(Convert.ToDecimal(value), 2),
        "Discount" => $"{value}%",
        _          => value
    }
});

IMemoryFile file = await excel.Create([sheet]);
```

---

## Example 4: Read with header filtering

Supply a `headers` array to receive only the columns you need.

```csharp
IExcelService excel = new Regira.Office.Excel.ClosedXML.ExcelManager();

var file   = excelBytes.ToBinaryFile();
var sheets = await excel.Read(file, headers: ["Name", "Email", "Phone"]);

foreach (var row in sheets.First().Data!.Cast<IDictionary<string, object>>())
{
    Console.WriteLine($"{row["Name"]} — {row["Email"]}");
}
```

---

## Example 5: Round-trip — read, transform, write back

Read an existing workbook, modify the data, and produce a new file.

```csharp
public async Task<IMemoryFile> ApplyDiscount(byte[] sourceBytes, decimal discountPct)
{
    IExcelService excel = new Regira.Office.Excel.MiniExcel.ExcelManager();

    var sheets = (await excel.Read(sourceBytes.ToBinaryFile())).ToList();

    foreach (var sheet in sheets)
    {
        foreach (var row in sheet.Data!.Cast<IDictionary<string, object?>>())
        {
            if (row.TryGetValue("Price", out var price) && price is decimal d)
                row["Price"] = Math.Round(d * (1 - discountPct / 100), 2);
        }
    }

    return await excel.Create(sheets);
}
```

---

## Overview

1. [Index](README.md) — Overview, interfaces, models, and implementation notes
1. **[Examples](examples.md)** — Read, write, typed mapping, and DataSet export
