# Regira Office.CSV AI Agent Instructions

---

## Module Context

Part of **Regira Office**. For routing and full module overview, see [`office.instructions.md`](./office.instructions.md).

| Namespace | Covers |
|---|---|
| `Regira.Office.Csv` | CSV reading and writing (typed + dictionary) |

**Related:**
- [IO.Storage](../../Common.IO.Storage/ai/io.storage.instructions.md) — `IBinaryFile` / `IMemoryFile` used as input/output

---

## Installation

```xml
<PackageReference Include="Regira.Office.Csv.CsvHelper" Version="6.*" />
```

---

## `ICsvService` / `ICsvService<T>`

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
| `IgnoreBadData` | `bool` | `false` | Skip malformed rows instead of throwing |
| `PreserveWhitespace` | `bool` | `false` | Keep leading/trailing whitespace in cell values |

---

## Notes

- First row is always treated as the header.
- Non-generic `CsvManager` reads each row into `Dictionary<string, object>`.
- For typed `CsvManager<T>`, column names must match property names (or use CsvHelper `[Name]` attributes on the POCO).
- `WriteFile` returns `IMemoryFile` with `ContentType = "text/csv"`.

---

## Usage

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

// Write non-generic from a list of dictionaries
string csv = await new CsvManager().Write(rows);
```

---
