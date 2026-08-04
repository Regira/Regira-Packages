# Regira Office.Excel AI Agent Instructions

---

## Module Context

Part of **Regira Office**. For routing and full module overview, see [`office.instructions.md`](./office.instructions.md).

| Namespace | Covers |
|---|---|
| `Regira.Office.Excel` | Excel workbook reading and writing |

**Related:**
- [IO.Storage](../../Common.IO.Storage/ai/io.storage.instructions.md) — `IBinaryFile` / `IMemoryFile` used as input/output

---

## Installation

```xml
<!-- MiniExcel — recommended (streaming, generic, lightweight) -->
<PackageReference Include="Regira.Office.Excel.MiniExcel" Version="6.*" />

<!-- ClosedXML -->
<PackageReference Include="Regira.Office.Excel.ClosedXML" Version="6.*" />

<!-- EPPlus v4 (free licence) -->
<PackageReference Include="Regira.Office.Excel.EPPlus" Version="6.*" />

<!-- NpoiMapper (type-mapped, generic) -->
<PackageReference Include="Regira.Office.Excel.NpoiMapper" Version="6.*" />
```

---

## Backend Comparison

| Package | Backend | Generic `<T>` | Streaming | Notes |
|---|---|---|---|---|
| `Regira.Office.Excel.MiniExcel` | MiniExcel | ✓ | ✓ | Recommended — fast, low memory |
| `Regira.Office.Excel.ClosedXML` | ClosedXML | — | — | Rich formatting support |
| `Regira.Office.Excel.EPPlus` | EPPlus v4 | — | — | Free licence (v4) |
| `Regira.Office.Excel.NpoiMapper` | NPOI + Npoi.Mapper | ✓ | — | Type-mapped via attributes |

**Default recommendation:** Use `MiniExcel` for reading/writing data — supports generics, streaming, and has minimal memory overhead.

---

## Interfaces

### `IExcelReader` / `IExcelReader<T>`

```csharp
Task<IEnumerable<ExcelSheet>>    Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default);
Task<IEnumerable<ExcelSheet<T>>> Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default);  // generic
```

`headers` — when supplied, only those columns are returned (by name).

### `IExcelWriter` / `IExcelWriter<T>`

```csharp
Task<IMemoryFile> Create(IEnumerable<ExcelSheet> sheets, CancellationToken cancellationToken = default);
Task<IMemoryFile> Create(IEnumerable<ExcelSheet<T>> sheets, CancellationToken cancellationToken = default);  // generic
```

### `IExcelService` / `IExcelService<T>`

Composite: `IExcelReader + IExcelWriter` (and their generic variants).

---

## Models

### `ExcelSheet` / `ExcelSheet<T>`

| Property | Type | Description |
|---|---|---|
| `Name` | `string?` | Sheet tab name |
| `Data` | `ICollection<T>?` | Rows — `Dictionary<string, object>` per row for the non-generic sheet, `T` for typed |

The non-generic `ExcelSheet` is `ExcelSheet<object>`.

---

## Usage

```csharp
// Construct directly (no DI extensions — pick any implementation)
IExcelService excel = new Regira.Office.Excel.MiniExcel.ExcelManager();

// Read all sheets
IBinaryFile file = bytes.ToBinaryFile();
var sheets       = await excel.Read(file);
foreach (var sheet in sheets)
    foreach (var row in sheet.Data!)
        Console.WriteLine(row);   // Dictionary<string, object>

// Read typed
IExcelService<Product> typed = new Regira.Office.Excel.MiniExcel.ExcelManager<Product>();
var sheets = await typed.Read(file);
foreach (var product in sheets.First().Data!)
    Console.WriteLine(product.Name);

// Write sheets to a new workbook
IMemoryFile output = await excel.Create(sheets);
```

---
