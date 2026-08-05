# Regira Office.Excel

Regira Office.Excel provides a **unified abstraction** for reading and writing Excel workbooks across multiple underlying libraries. All implementations share the same `IExcelService` interface, making backends interchangeable.

## Projects

| Project | Package | Backend | Generic `<T>` | Streaming |
|---------|---------|---------|--------------|-----------|
| `Common.Office` | *(transitive)* | Shared abstractions and models | — | — |
| `Excel.ClosedXML` | `Regira.Office.Excel.ClosedXML` | ClosedXML | — | — |
| `Excel.EPPlus` | `Regira.Office.Excel.EPPlus` | EPPlus v4 | — | — |
| `Excel.MiniExcel` | `Regira.Office.Excel.MiniExcel` | MiniExcel | ✓ | ✓ |
| `Excel.NpoiMapper` | `Regira.Office.Excel.NpoiMapper` | NPOI + Npoi.Mapper | ✓ | — |

## Installation

```xml
<!-- ClosedXML -->
<PackageReference Include="Regira.Office.Excel.ClosedXML" Version="6.*" />

<!-- EPPlus (v4 — free licence) -->
<PackageReference Include="Regira.Office.Excel.EPPlus" Version="6.*" />

<!-- MiniExcel (streaming, generic) -->
<PackageReference Include="Regira.Office.Excel.MiniExcel" Version="6.*" />

<!-- NpoiMapper (type-mapped, generic) -->
<PackageReference Include="Regira.Office.Excel.NpoiMapper" Version="6.*" />
```

## Quick Start

```csharp
// Construct directly (no DI extensions — pick any implementation)
IExcelService excel = new Regira.Office.Excel.MiniExcel.ExcelManager();

// Read all sheets from a file
byte[] bytes      = await File.ReadAllBytesAsync("workbook.xlsx");
IBinaryFile file  = bytes.ToBinaryFile();
var sheets        = await excel.Read(file);

foreach (var sheet in sheets)
    foreach (var row in sheet.Data!)
        Console.WriteLine(row);   // Dictionary<string, object> per row

// Write sheets to a new workbook
IMemoryFile output = await excel.Create(sheets);
```

## Interfaces

### IExcelReader / IExcelReader\<T\>

```csharp no-compile
Task<IEnumerable<ExcelSheet>>    Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default);
Task<IEnumerable<ExcelSheet<T>>> Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default);  // generic
```

`headers` — behavior differs per backend:

- **ClosedXML** — only the named columns are returned (matched case-insensitively against row 1).
- **EPPlus** — row 1 is **not** treated as a header row: it is read as data, and your array supplies the dictionary keys (positionally).
- **MiniExcel / NpoiMapper** — the parameter is ignored; row 1 is always used as headers.

### IExcelWriter / IExcelWriter\<T\>

```csharp no-compile
Task<IMemoryFile> Create(IEnumerable<ExcelSheet>    sheets, CancellationToken cancellationToken = default);
Task<IMemoryFile> Create(IEnumerable<ExcelSheet<T>> sheets, CancellationToken cancellationToken = default);  // generic
```

### IExcelService / IExcelService\<T\>

Composite interfaces: `IExcelService : IExcelServiceCore, IExcelReader, IExcelWriter` and `IExcelService<T> : IExcelServiceCore, IExcelReader<T>, IExcelWriter<T> where T : class, new()`. Use these as the injection target.

## ExcelSheet\<T\>

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string?` | Sheet tab name |
| `Data` | `ICollection<T>?` | Rows — `Dictionary<string,object>` for non-generic, `T` for typed |

Non-generic `ExcelSheet` is `ExcelSheet<object>`.

## Configuration

All four implementations accept an `Options` object:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DateFormat` | `string` | `"yyyy-MM-dd hh:mm:ss"` (`"yyyy/MM/dd"` for EPPlus) | Format applied to DateTime cells on write |

`DateFormat` is honored by **EPPlus** and **NpoiMapper** only; **ClosedXML** and **MiniExcel** declare the option but never use it.

EPPlus adds one extra option:

| Property | Type | Description |
|----------|------|-------------|
| `TransformData` | `Func<string, string, object, object>?` | Called per cell during write — receives `(cellAddress, columnKey, value)`, returns replacement value |

```csharp
var excel = new Regira.Office.Excel.EPPlus.ExcelManager(new()
{
    DateFormat    = "dd/MM/yyyy",
    TransformData = (cell, key, value) =>
        key == "Price" ? Math.Round((decimal)value, 2) : value
});
```

## Implementation notes

### ClosedXML

Simple and stable. Returns rows as `Dictionary<string, object?>`. No generic support.

### EPPlus

Locked at EPPlus **v4** (free licence; v5+ is commercial). Supports `DataSet` directly and a `TransformData` callback for per-cell value transformation. Best pick when you need raw dictionary access plus cell-level control.

```csharp
// Write from a DataSet
IExcelService excel = new Regira.Office.Excel.EPPlus.ExcelManager();
DataSet myDataSet   = new();
IMemoryFile file = ((Regira.Office.Excel.EPPlus.ExcelManager)excel).Create(myDataSet);
```

### MiniExcel

Lowest memory footprint — uses streaming under the hood. Has a **generic `ExcelManager<T>`** that maps rows directly to typed objects (as does NpoiMapper). Automatically renames duplicate column headers (`"Col"` → `"Col_2"`, `"Col_3"`, …).

```csharp no-compile
IExcelService<Product> excel = new Regira.Office.Excel.MiniExcel.ExcelManager<Product>();

var sheets   = await excel.Read(file);          // IEnumerable<ExcelSheet<Product>>
var products = sheets.First().Data!;            // ICollection<Product>
```

### NpoiMapper

Uses Npoi.Mapper for property-to-column binding. Also has a generic `ExcelManager<T>`. Good for scenarios where column names match property names (or are annotated).

```csharp no-compile
IExcelService<Order> excel = new Regira.Office.Excel.NpoiMapper.ExcelManager<Order>();
```

## Implementation comparison

| Feature | ClosedXML | EPPlus | MiniExcel | NpoiMapper |
|---------|-----------|--------|-----------|------------|
| **Recommended for** | Simple R/W | Callbacks & DataSet | Large files / typed | Type mapping |
| **Generic `<T>`** | — | — | ✓ | ✓ |
| **Streaming** | — | — | ✓ | — |
| **DataSet support** | — | ✓ | — | — |
| **TransformData callback** | — | ✓ | — | — |
| **Duplicate header fix** | — | — | ✓ (auto-rename) | — |


## Overview

1. **[Index](README.md)** — Overview, interfaces, models, and implementation notes
1. [Examples](examples.md) — Read, write, typed mapping, and DataSet export
