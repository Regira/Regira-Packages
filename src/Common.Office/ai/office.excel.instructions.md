# Regira Office.Excel AI Agent Instructions

---

## Module Context

Part of **Regira Office**. For routing and full module overview, see [`office.instructions.md`](./office.instructions.md).

| Namespace | Covers |
|---|---|
| `Regira.Office.Excel` | Excel workbook reading and writing |

Exact usings: `office.namespaces.md`.

**Related:**
- **IO.Storage** — `IBinaryFile` / `IMemoryFile` used as input/output. `get_package(id: "Regira.IO.Storage", section: "io.storage.instructions")`, or `io.storage.instructions.md` locally.

---

## Installation

```bash
# MiniExcel — recommended (generic, low memory)
dotnet add package Regira.Office.Excel.MiniExcel

# ClosedXML
dotnet add package Regira.Office.Excel.ClosedXML

# EPPlus v4 — deprecated (legacy EPPlus, Windows)
dotnet add package Regira.Office.Excel.EPPlus

# NpoiMapper (type-mapped, generic; NPOI pinned to 2.7.2)
dotnet add package Regira.Office.Excel.NpoiMapper
```

---

## Backend Comparison

| Package | Backend | Generic `<T>` | Low memory | Reads `.xls` | Notes |
|---|---|---|---|---|---|
| `Regira.Office.Excel.MiniExcel` | MiniExcel | ✓ | ✓ | — | Recommended — fast, low memory |
| `Regira.Office.Excel.ClosedXML` | ClosedXML | — | — | — | Writes plain values (`Options.DateFormat` for dates). For fills, formulas, frozen panes or filters, use ClosedXML's `XLWorkbook` directly; the package brings it |
| `Regira.Office.Excel.EPPlus` | EPPlus v4 | — | — | — | Deprecated — see below |
| `Regira.Office.Excel.NpoiMapper` | NPOI + Npoi.Mapper | ✓ | — | ✓ | Type-mapped via attributes; NPOI pinned to 2.7.2 |

Only NpoiMapper reads the legacy binary `.xls` (Excel 97–2003); the other backends throw on it (`FileFormatException` on ClosedXML, `InvalidDataException` on MiniExcel and EPPlus). Every backend writes `.xlsx`.

**Default recommendation:** Use `MiniExcel` for reading/writing data — supports generics and reads and writes the sheet XML without a workbook object model. Rows are still collected into `ExcelSheet.Data`; no backend streams rows to the caller.

**EPPlus is deprecated.** It stays on EPPlus 4.5.3.3, the last release under a free licence (EPPlus 5 and later need a commercial licence), which nuget.org marks as legacy and which receives no fixes. It depends on `System.Drawing.Common`, which throws on non-Windows from .NET 6 on — treat it as **Windows**. Use it only for what no other backend does — writing a `DataSet`, or the per-cell `TransformData` callback; otherwise pick MiniExcel or ClosedXML. It takes fixes for crashes, data loss and security holes only.

**NpoiMapper pins NPOI to exactly 2.7.2.** From 2.8.0 the NPOI binaries on nuget.org carry an Open Source Maintenance Fee agreement that requires a paid subscription from organisations with annual revenue of US$10,000 or more. Do not raise NPOI in the application: a direct reference overrides the pin with only a restore warning (NU1608). From 2.7.3 Npoi.Mapper throws `MissingMethodException` when it writes a cell, and 2.8 or later puts the application under that agreement.

**NpoiMapper pins SixLabors.ImageSharp to 3.1.12**, because from 3.2.0 and 4.0.0 a project that references ImageSharp directly needs a Six Labors licence key to build in Release (a Debug build only warns; `-c Release` and `dotnet publish` fail). 3.1.12 restores with advisories GHSA-j3p4-wp97-rph4, GHSA-j9gm-c75j-xc9q and GHSA-jjfr-hcj7-qf5w (NU1903) and GHSA-gwg2-r3hj-4w44 and GHSA-wmxv-xphr-5c9g (NU1902), which 3.2.0 and 4.1.2 fix. The backend decodes no images: NPOI loads a picture through ImageSharp only when the application calls `IPicture.Resize()`, `GetPreferredSize()` or `GetImageDimension()`. Taking the fixes is the user's licence decision, not a routine bump: the application references `SixLabors.ImageSharp` 3.2.0 and then needs a key to build (`sixlabors.lic` in the project directory, or the `SixLaborsLicenseKey` property), which Six Labors issues on application to open-source projects, non-profits and companies under US$1M annual revenue, and sells otherwise. NuGet audit can keep flagging 3.2.0: it reads the GitHub Advisory Database, whose entries name only 4.1.2, while Six Labors' own advisories name 3.2.0 as well.

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

- **Read** — row 1 holds the keys: a blank header becomes `Column{n}` (its column number), a key taken already, from the left and case-insensitively, the first free `{key}_2`, `{key}_3`, … (`Value`, `Value`, `Value_2` reads as `Value`, `Value_2`, `Value_2_2`). A sheet without rows reads as an empty `Data`. Each row is a `Dictionary<string, object?>`: an empty cell is `null`, text a `string`, a number a `double`, a boolean a `bool`, a date cell a `DateTime`, except on EPPlus, which reads a date as `double` unless its number format holds `yyyy`. The row's keys compare case-sensitively (`Name` is not `name`). Blank rows differ per backend: MiniExcel and EPPlus return them as rows of `null`s, ClosedXML skips them, and NpoiMapper skips only rows that were never written, so on those two a row's index is not its sheet row number.
- **Create** — the header row holds every key the rows use (case-insensitive, first spelling, first-seen order); a row holding two keys that differ only in case (`Id` beside `ID`) throws `ArgumentException`; a row without a key gets an empty cell, and each cell keeps its own value's type; a property holding an object is written as its `ToString()`. NpoiMapper's untyped `ExcelManager` writes object rows this way too, without Npoi.Mapper's attributes; its typed `ExcelManager<T>` maps through Npoi.Mapper. An empty or `null` `Data` writes an empty sheet (a typed writer keeps its header row), and no sheets at all write one empty sheet with the default name.
- **Sheet names** — a missing `Name` becomes `Sheet-{n}` (`Sheet {n}` on EPPlus). A name Excel refuses throws `ArgumentException`: empty, over 31 characters, holding `: \ / ? * [ ]`, starting or ending with `'`, `History`, or used twice (case-insensitive).
- **`Options.DateFormat`** — the number format of the `DateTime` cells written; they stay dates. Defaults: `null` on ClosedXML (the library's own date format), `"yyyy/MM/dd"` on EPPlus, `"yyyy-MM-dd hh:mm:ss"` on NpoiMapper. MiniExcel's is `[Obsolete]` and ignored: it writes Excel's built-in date format; use ClosedXML to format dates.

---

## Models

### `ExcelSheet` / `ExcelSheet<T>`

| Property | Type | Description |
|---|---|---|
| `Name` | `string?` | Sheet tab name |
| `Data` | `ICollection<T>?` | Rows — `Dictionary<string, object?>` per row for the non-generic sheet, `T` for typed |

The non-generic `ExcelSheet` is `ExcelSheet<object>`.

No backend writes charts, cell styles beyond `DateFormat`, or formulas (a string starting with `=` is written as text). Post-process the created bytes with the Open XML SDK (`DocumentFormat.OpenXml`, which ClosedXML brings), or, for styles and formulas, build the workbook with ClosedXML's `XLWorkbook` directly. ClosedXML does not create charts: a chart takes the Open XML SDK (a `DrawingsPart` holding a `ChartPart`).

---

## Usage

There is no `Add…` extension: register the backend yourself. An `ExcelManager` keeps nothing but the options it was
constructed with, so a singleton is safe:

<!-- no-compile -->
```csharp
services.AddSingleton<IExcelService, Regira.Office.Excel.MiniExcel.ExcelManager>();
```

<!-- no-compile -->
```csharp
// Or construct one directly — pick any implementation
IExcelService excel = new Regira.Office.Excel.MiniExcel.ExcelManager();

// Read all sheets — from an IBinaryFile: bytes.ToBinaryFile(contentType), not ToMemoryFile() (an IMemoryFile is no IBinaryFile, CS1503)
IBinaryFile file = bytes.ToBinaryFile();
var sheets       = await excel.Read(file);
foreach (var sheet in sheets)
    foreach (var row in sheet.Data!)
        Console.WriteLine(row);   // Dictionary<string, object?>

// Read typed
IExcelService<Product> typed = new Regira.Office.Excel.MiniExcel.ExcelManager<Product>();
var products = await typed.Read(file);
foreach (var product in products.First().Data!)
    Console.WriteLine(product.Name);

// Write sheets to a new workbook
IMemoryFile output = await excel.Create(sheets);
// ⚠️ Read the result with GetBytes() (Regira.IO.Extensions), never .Bytes — a producer fills EITHER
// .Bytes or .Stream, and reading the empty half yields a 0-byte download with no error.
byte[] outputBytes = output.GetBytes()!;
```

---
