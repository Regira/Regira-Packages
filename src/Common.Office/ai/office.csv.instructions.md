# Regira Office.CSV AI Agent Instructions

---

## Module Context

Part of **Regira Office**. For routing and full module overview, see [`office.instructions.md`](./office.instructions.md).

| Namespace | Covers |
|---|---|
| `Regira.Office.Csv` | CSV reading and writing (typed + dictionary) |

Exact usings: `office.namespaces.md`.

**Related:**
- **IO.Storage** — `IBinaryFile` / `IMemoryFile` used as input/output. `get_package(id: "Regira.IO.Storage", section: "io.storage.instructions")`, or `io.storage.instructions.md` locally.

---

## Installation

```xml
<PackageReference Include="Regira.Office.Csv.CsvHelper" Version="6.*" />
```

---

## `ICsvService` / `ICsvService<T>`

<!-- no-compile -->
```csharp
// Read
Task<List<T>>     Read(string input,      CsvOptions? options = null, CancellationToken cancellationToken = default);
Task<List<T>>     Read(IBinaryFile input, CsvOptions? options = null, CancellationToken cancellationToken = default);

// Write
Task<string>      Write(IEnumerable<T> items,      CsvOptions? options = null, CancellationToken cancellationToken = default);
Task<IMemoryFile> WriteFile(IEnumerable<T> items,  CsvOptions? options = null, CancellationToken cancellationToken = default);
```

`ICsvService` is shorthand for `ICsvService<IDictionary<string, object>>`.

---

## `CsvOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `Delimiter` | `string` | `","` | Column separator |
| `Culture` | `CultureInfo` | `en-US` | Number / date formatting |

### `CsvHelperOptions` (extends `CsvOptions`)

| Property | Type | Default | Description |
|---|---|---|---|
| `IgnoreBadData` | `bool` | `false` | Read a field with a stray quote as it stands instead of throwing `BadDataException` |
| `PreserveWhitespace` | `bool` | `false` | Keep leading/trailing whitespace in cell values |

Both are public fields, read only from the options passed to the `CsvManager` constructor
(`new CsvManager<Product>(new CsvHelperOptions { IgnoreBadData = true })`); per-call options apply only `Delimiter` and
`Culture`. A per-call object sets both, so its default `","` replaces the constructor's delimiter.

---

## Notes

- First row is always treated as the header.
- Non-generic `CsvManager` reads each row into `Dictionary<string, object>`.
- For typed `CsvManager<T>`, column names must match property names (or use CsvHelper `[Name]` attributes on the POCO).
- A typed read throws at the first field it cannot convert (CsvHelper's `TypeConverterException`) and returns no rows. To report bad lines one by one, read with the non-generic `CsvManager`, whose values are all strings, and convert and validate each row yourself.
- `WriteFile` returns `IMemoryFile` with `ContentType = "text/csv"`.

---

## Usage

There is no `Add…` extension: register the classes yourself. A `CsvManager` keeps nothing but the options it was
constructed with, so a singleton is safe:

```csharp
using Regira.Office.Csv.Abstractions;
using Regira.Office.Csv.CsvHelper;

services.AddSingleton<ICsvService, CsvManager>();                     // rows as dictionaries
services.AddSingleton(typeof(ICsvService<>), typeof(CsvManager<>));   // typed rows: ICsvService<Product>
```

<!-- no-compile -->
```csharp
// Non-generic — rows as Dictionary<string, object>
ICsvService csv = new CsvManager();
var rows = await csv.Read(csvString);

// Generic — rows mapped to a POCO
ICsvService<Product> csv = new CsvManager<Product>();
var products = await csv.Read(csvString);

// Read from uploaded file
var csvFile  = formFile.ToNamedFile().ToBinaryFile();
var rows     = await new CsvManager().Read(csvFile);

// Read typed with semicolon delimiter
var products = await new CsvManager<Product>()
    .Read(csvString, new CsvHelperOptions { Delimiter = ";" });

// Write typed
IMemoryFile file = await new CsvManager<Order>()
    .WriteFile(orders, new CsvOptions { Delimiter = "\t" });

// ⚠️ Read the result with GetBytes() (Regira.IO.Extensions), never .Bytes — see office.instructions → Reading a result
byte[] bytes = file.GetBytes()!;

// Write non-generic from a list of dictionaries
string csv = await new CsvManager().Write(rows);
```

---
