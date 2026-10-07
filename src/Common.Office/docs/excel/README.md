# Regira Office.Excel

Regira Office.Excel provides a **unified abstraction** for reading and writing Excel workbooks across multiple underlying libraries. All implementations share the same `IExcelService` interface, making backends interchangeable.

## Projects

| Project | Package | Backend | Generic `<T>` | Low memory |
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

<!-- MiniExcel (low memory, generic) -->
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

<!-- no-compile -->
```csharp
Task<IEnumerable<ExcelSheet>>    Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default);
Task<IEnumerable<ExcelSheet<T>>> Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default);  // generic
```

`headers` — behavior differs per backend:

- **ClosedXML / MiniExcel / NpoiMapper** — only the named columns are returned (matched case-insensitively against row 1). The typed `ExcelManager<T>` of MiniExcel and NpoiMapper ignores the parameter: the properties of `T` select the columns.
- **EPPlus** — row 1 is **not** treated as a header row: it is read as data, and your array supplies the dictionary keys, in column order. Repeated and blank keys are made unique as headers are (`Name`, `Name_2`, `Column3`).

### IExcelWriter / IExcelWriter\<T\>

<!-- no-compile -->
```csharp
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

## Reading and writing rows

All four backends read and write the untyped sheet the same way:

- **Reading** — row 1 holds the keys. A blank header becomes `Column{n}`, its column number; a repeated header — compared case-insensitively, as keys are on write — becomes `{header}_2`, `{header}_3`, …, skipping a name another header in the row already has. A sheet without rows reads as an empty `Data`.
- **Writing** — the header row holds every key the sheet's rows use, compared case-insensitively, spelled as it first appears and in that order — within a row too, where the first of two keys differing only in case keeps its value; a row without a key gets an empty cell, and each cell keeps its own value's type. A sheet whose `Data` is empty or `null` is written empty; the typed `ExcelManager<T>` still writes its header row. A property holding an object is written as the object's `ToString()`. NpoiMapper hands rows that are objects rather than dictionaries to Npoi.Mapper, which leaves such a property out.
- **Sheet names** — a sheet without a `Name` becomes `Sheet-{n}` (`Sheet {n}` on EPPlus), skipping the names already taken. A name Excel refuses throws `ArgumentException`: empty, longer than 31 characters, holding `:` `\` `/` `?` `*` `[` `]`, starting or ending with an apostrophe, `History`, or used twice (compared case-insensitively). EPPlus checks the table names of a `DataSet` the same way.

## Configuration

All four implementations accept an `Options` object with a `DateFormat`: the Excel number format of the `DateTime` cells they write. The cells stay dates.

| Backend | Type | Default | Applied to |
|---------|------|---------|------------|
| ClosedXML | `string?` | `null` — ClosedXML's date format | every `DateTime` cell |
| EPPlus | `string` | `"yyyy/MM/dd"` | every `DateTime` cell |
| MiniExcel | `string?` | `null` — Excel's built-in date format | every key whose values are all `DateTime`s, in every sheet |
| NpoiMapper | `string` | `"yyyy-MM-dd hh:mm:ss"` | every `DateTime` cell of a dictionary row |

MiniExcel formats a column by its key across the workbook, so a key that also holds a number, here or in another sheet, keeps the built-in format.

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

Lowest memory footprint — it reads and writes the sheet XML directly, without loading a workbook object model; the rows are still collected into `ExcelSheet.Data`. Has a **generic `ExcelManager<T>`** that maps rows directly to typed objects (as does NpoiMapper).

<!-- no-compile -->
```csharp
IExcelService<Product> excel = new Regira.Office.Excel.MiniExcel.ExcelManager<Product>();

var sheets   = await excel.Read(file);          // IEnumerable<ExcelSheet<Product>>
var products = sheets.First().Data!;            // ICollection<Product>
```

### NpoiMapper

Uses Npoi.Mapper for property-to-column binding. Also has a generic `ExcelManager<T>`. Good for scenarios where column names match property names (or are annotated).

<!-- no-compile -->
```csharp
IExcelService<Order> excel = new Regira.Office.Excel.NpoiMapper.ExcelManager<Order>();
```

## Implementation comparison

| Feature | ClosedXML | EPPlus | MiniExcel | NpoiMapper |
|---------|-----------|--------|-----------|------------|
| **Recommended for** | Simple R/W | Callbacks & DataSet | Large files / typed | Type mapping |
| **Generic `<T>`** | — | — | ✓ | ✓ |
| **Low memory** | — | — | ✓ | — |
| **DataSet support** | — | ✓ | — | — |
| **TransformData callback** | — | ✓ | — | — |
| **`headers` on read** | Filter | Column keys | Filter | Filter |


## Overview

1. **[Index](README.md)** — Overview, interfaces, models, and implementation notes
1. [Examples](examples.md) — Read, write, typed mapping, and DataSet export
