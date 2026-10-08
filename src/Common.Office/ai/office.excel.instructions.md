# Regira Office.Excel AI Agent Instructions

---

## Module Context

Part of **Regira Office**. For routing and full module overview, see [`office.instructions.md`](./office.instructions.md).

| Namespace | Covers |
|---|---|
| `Regira.Office.Excel` | Excel workbook reading and writing |

**Related:**
- **IO.Storage** — `IBinaryFile` / `IMemoryFile` used as input/output. `get_package(id: "Regira.IO.Storage", section: "io.storage.instructions")`, or `io.storage.instructions.md` locally.

---

## Installation

```xml
<!-- MiniExcel — recommended (generic, low memory) -->
<PackageReference Include="Regira.Office.Excel.MiniExcel" Version="6.*" />

<!-- ClosedXML -->
<PackageReference Include="Regira.Office.Excel.ClosedXML" Version="6.*" />

<!-- EPPlus v4 — deprecated (legacy EPPlus, Windows) -->
<PackageReference Include="Regira.Office.Excel.EPPlus" Version="6.*" />

<!-- NpoiMapper (type-mapped, generic; NPOI pinned to 2.7.1) -->
<PackageReference Include="Regira.Office.Excel.NpoiMapper" Version="6.*" />
```

---

## Backend Comparison

| Package | Backend | Generic `<T>` | Low memory | Notes |
|---|---|---|---|---|
| `Regira.Office.Excel.MiniExcel` | MiniExcel | ✓ | ✓ | Recommended — fast, low memory |
| `Regira.Office.Excel.ClosedXML` | ClosedXML | — | — | Rich formatting support |
| `Regira.Office.Excel.EPPlus` | EPPlus v4 | — | — | Deprecated — see below |
| `Regira.Office.Excel.NpoiMapper` | NPOI + Npoi.Mapper | ✓ | — | Type-mapped via attributes; NPOI pinned to 2.7.1 |

**Default recommendation:** Use `MiniExcel` for reading/writing data — supports generics and reads and writes the sheet XML without a workbook object model. Rows are still collected into `ExcelSheet.Data`; no backend streams rows to the caller.

**EPPlus is deprecated.** It stays on EPPlus 4.5.3.3, the last release under a free licence (EPPlus 5 and later need a commercial licence), which nuget.org marks as legacy and which receives no fixes. It depends on `System.Drawing.Common`, which throws on non-Windows from .NET 6 on — treat it as **Windows**. Use it only for what no other backend does — writing a `DataSet`, or the per-cell `TransformData` callback; otherwise pick MiniExcel or ClosedXML.

**NpoiMapper pins NPOI to exactly 2.7.1.** From 2.8.0 the NPOI binaries on nuget.org carry an Open Source Maintenance Fee agreement that requires a paid subscription from organisations with annual revenue of US$10,000 or more. Do not raise NPOI in the application: a direct reference to 2.8 or later overrides the pin with only a restore warning (NU1608), and puts the application under that agreement.

---

## Interfaces

### `IExcelReader` / `IExcelReader<T>`

<!-- no-compile -->
```csharp
Task<IEnumerable<ExcelSheet>>    Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default);
Task<IEnumerable<ExcelSheet<T>>> Read(IBinaryFile input, string[]? headers = null, CancellationToken cancellationToken = default);  // generic
```

`headers` — on MiniExcel, ClosedXML and NpoiMapper, only the named columns are returned (matched case-insensitively against row 1). EPPlus reads row 1 as data and uses the array as the keys, in column order; the typed readers ignore it.

### `IExcelWriter` / `IExcelWriter<T>`

<!-- no-compile -->
```csharp
Task<IMemoryFile> Create(IEnumerable<ExcelSheet> sheets, CancellationToken cancellationToken = default);
Task<IMemoryFile> Create(IEnumerable<ExcelSheet<T>> sheets, CancellationToken cancellationToken = default);  // generic
```

### `IExcelService` / `IExcelService<T>`

Composite: `IExcelReader + IExcelWriter` (and their generic variants).

### Behaviour shared by the backends

- **Read** — row 1 holds the keys: a blank header becomes `Column{n}` (its column number), a repeated one (case-insensitive) `{header}_2`, `{header}_3`, … (skipping a name another header already has). A sheet without rows reads as an empty `Data`.
- **Create** — the header row holds every key the rows use (case-insensitive, first spelling, first-seen order; within a row, the first of `Id`/`ID` keeps its value); a row without a key gets an empty cell, and each cell keeps its own value's type; a property holding an object is written as its `ToString()` (NpoiMapper's Npoi.Mapper leaves it out). An empty or `null` `Data` writes an empty sheet (a typed writer keeps its header row).
- **Sheet names** — a missing `Name` becomes `Sheet-{n}` (`Sheet {n}` on EPPlus). A name Excel refuses throws `ArgumentException`: empty, over 31 characters, holding `: \ / ? * [ ]`, starting or ending with `'`, `History`, or used twice (case-insensitive).
- **`Options.DateFormat`** — the number format of the `DateTime` cells written; they stay dates. Defaults: `null` on ClosedXML and MiniExcel (the library's own date format), `"yyyy/MM/dd"` on EPPlus, `"yyyy-MM-dd hh:mm:ss"` on NpoiMapper. MiniExcel applies it per key across the workbook, only to keys whose values are all `DateTime`s.

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

<!-- no-compile -->
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
// ⚠️ Read the result with GetBytes() (Regira.IO.Extensions), never .Bytes — a producer fills EITHER
// .Bytes or .Stream, and reading the empty half yields a 0-byte download with no error.
byte[] outputBytes = output.GetBytes()!;
```

---
