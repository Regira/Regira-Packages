# Regira Office.Excel

Regira Office.Excel provides a **unified abstraction** for reading and writing Excel workbooks across multiple underlying libraries. All implementations share the same `IExcelService` interface, making backends interchangeable.

## Projects

| Project | Package | Backend | Generic `<T>` | Low memory |
|---------|---------|---------|--------------|-----------|
| `Common.Office` | *(transitive)* | Shared abstractions and models | — | — |
| `Excel.ClosedXML` | `Regira.Office.Excel.ClosedXML` | ClosedXML | — | — |
| `Excel.EPPlus` | `Regira.Office.Excel.EPPlus` | EPPlus v4 (deprecated) | — | — |
| `Excel.MiniExcel` | `Regira.Office.Excel.MiniExcel` | MiniExcel | ✓ | ✓ |
| `Excel.NpoiMapper` | `Regira.Office.Excel.NpoiMapper` | NPOI + Npoi.Mapper | ✓ | — |

## Installation

```xml
<!-- ClosedXML -->
<PackageReference Include="Regira.Office.Excel.ClosedXML" Version="6.*" />

<!-- EPPlus (v4 — deprecated) -->
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
        Console.WriteLine(row);   // Dictionary<string, object?> per row

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

- **Reading** — row 1 holds the keys. A blank header becomes `Column{n}`, its column number; a key taken already — compared case-insensitively, as keys are on write — becomes the first free `{key}_2`, `{key}_3`, …, from the left: `Value`, `Value`, `Value_2` reads as `Value`, `Value_2`, `Value_2_2`. A sheet without rows reads as an empty `Data`.
- **Writing** — the header row holds every key the sheet's rows use, compared case-insensitively, spelled as it first appears and in that order; a row holding two keys that differ only in case (`Id` beside `ID`) throws `ArgumentException`; a row without a key gets an empty cell, and each cell keeps its own value's type. A sheet whose `Data` is empty or `null` is written empty; the typed `ExcelManager<T>` still writes its header row. A `Create` given no sheets writes one empty sheet under the default name, since Excel opens no workbook without one. A property holding an object is written as the object's `ToString()`. NpoiMapper's untyped `ExcelManager` writes object rows this way too, without Npoi.Mapper's attributes; its typed `ExcelManager<T>` maps through Npoi.Mapper.
- **Sheet names** — a sheet without a `Name` becomes `Sheet-{n}` (`Sheet {n}` on EPPlus), skipping the names already taken. A name Excel refuses throws `ArgumentException`: empty, longer than 31 characters, holding `:` `\` `/` `?` `*` `[` `]`, starting or ending with an apostrophe, `History`, or used twice (compared case-insensitively). EPPlus checks the table names of a `DataSet` the same way.

## Configuration

All four implementations accept an `Options` object with a `DateFormat`: the Excel number format of the `DateTime` cells they write. The cells stay dates. MiniExcel's is `[Obsolete]` and ignored.

| Backend | Type | Default | Applied to |
|---------|------|---------|------------|
| ClosedXML | `string?` | `null` — ClosedXML's date format | every `DateTime` cell |
| EPPlus | `string` | `"yyyy/MM/dd"` | every `DateTime` cell |
| MiniExcel | `string?` | ignored — Excel's built-in date format | — |
| NpoiMapper | `string` | `"yyyy-MM-dd hh:mm:ss"` | every `DateTime` cell of the untyped `ExcelManager` |

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

**Deprecated.** Locked at EPPlus **4.5.3.3**, the last release under a free licence (v5+ is commercial). nuget.org marks that release as legacy and it receives no fixes; it depends on `System.Drawing.Common`, which throws on non-Windows from .NET 6 on, so treat the backend as Windows-only. It is the only backend that writes a `DataSet` directly and takes a `TransformData` callback for per-cell value transformation — use it for those alone, and MiniExcel or ClosedXML otherwise. It takes fixes for crashes, data loss and security holes only.

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

Its generic `ExcelManager<T>` uses Npoi.Mapper for property-to-column binding: good for scenarios where column names match property names (or are annotated). The untyped `ExcelManager` writes rows as the other backends do.

NPOI is pinned to exactly **2.7.2**. From 2.8.0 its binaries on nuget.org carry an Open Source Maintenance Fee agreement that requires a paid subscription from organisations with annual revenue of US$10,000 or more. Keep NPOI out of your own package references: a direct reference overrides the pin with only a restore warning (NU1608). From 2.7.3 Npoi.Mapper throws `MissingMethodException` when it writes a cell, and 2.8 or later brings the application under that agreement.

SixLabors.ImageSharp, which NPOI uses to read pictures, is pinned to **3.1.12**: from 3.2.0 and 4.0.0, a project that references ImageSharp directly needs a Six Labors licence key to build. 3.1.12 restores with advisories GHSA-j3p4-wp97-rph4, GHSA-j9gm-c75j-xc9q and GHSA-jjfr-hcj7-qf5w (high, NU1903) and GHSA-gwg2-r3hj-4w44 and GHSA-wmxv-xphr-5c9g (moderate, NU1902), which 3.2.0 and 4.1.2 fix. The backend decodes no images; NPOI loads a picture through ImageSharp only when your code sizes it (`IPicture.Resize()`, `GetPreferredSize()`, `GetImageDimension()`). For the fixes, reference `SixLabors.ImageSharp` 3.2.0 in your application, which then needs the key: Six Labors issues one on application to open-source projects, non-profits and companies under US$1M annual revenue, and sells it otherwise. NuGet audit can keep flagging 3.2.0: it reads the GitHub Advisory Database, whose entries name only 4.1.2, while Six Labors' own advisories name 3.2.0 as well.

<!-- no-compile -->
```csharp
IExcelService<Order> excel = new Regira.Office.Excel.NpoiMapper.ExcelManager<Order>();
```

## Implementation comparison

| Feature | ClosedXML | EPPlus | MiniExcel | NpoiMapper |
|---------|-----------|--------|-----------|------------|
| **Recommended for** | Simple R/W | Callbacks & DataSet (deprecated) | Large files / typed | Type mapping |
| **Generic `<T>`** | — | — | ✓ | ✓ |
| **Low memory** | — | — | ✓ | — |
| **DataSet support** | — | ✓ | — | — |
| **TransformData callback** | — | ✓ | — | — |
| **`headers` on read** | Filter | Column keys | Filter | Filter |


## Overview

1. **[Index](README.md)** — Overview, interfaces, models, and implementation notes
1. [Examples](examples.md) — Read, write, typed mapping, and DataSet export
