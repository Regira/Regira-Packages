# Regira Office.Csv

Regira Office.Csv provides async CSV read and write via CsvHelper, with both generic (typed) and non-generic (dictionary) modes.

## Projects

| Project | Package | Backend |
|---------|---------|---------|
| `Common.Office` | *(transitive)* | Shared abstractions |
| `Csv.CsvHelper` | `Regira.Office.Csv.CsvHelper` | CsvHelper v33 |

## Installation

```xml
<PackageReference Include="Regira.Office.Csv.CsvHelper" Version="6.*" />
```

## Quick Start

```csharp no-compile
// Non-generic — rows as Dictionary<string, object>
ICsvService csv = new CsvManager();
var rows = await csv.Read(csvString);

// Generic — rows mapped to a POCO
ICsvService<Product> csv = new CsvManager<Product>();
var products = await csv.Read(csvString);
```

## ICsvService / ICsvService\<T\>

```csharp no-compile
// Read
Task<List<T>>    Read(string input,       CsvOptions? options = null, CancellationToken cancellationToken = default);
Task<List<T>>    Read(IBinaryFile input,  CsvOptions? options = null, CancellationToken cancellationToken = default);

// Write
Task<string>     Write(IEnumerable<T> items,     CsvOptions? options = null, CancellationToken cancellationToken = default);
Task<IMemoryFile> WriteFile(IEnumerable<T> items, CsvOptions? options = null, CancellationToken cancellationToken = default);
```

`ICsvService` is `ICsvService<IDictionary<string, object>>`.

## CsvOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Delimiter` | `string` | `","` | Column separator |
| `Culture` | `CultureInfo` | `en-US` | Number / date formatting |

### CsvHelperOptions (extends CsvOptions)

| Member | Type | Default | Description |
|--------|------|---------|-------------|
| `IgnoreBadData` | `bool` (public field) | `false` | Skip malformed rows instead of throwing |
| `PreserveWhitespace` | `bool` (public field) | `false` | Keep leading/trailing whitespace in cell values |

> `IgnoreBadData` and `PreserveWhitespace` are only honored when the `CsvHelperOptions` instance is
> passed to the **`CsvManager` constructor**. On the per-call `options` parameter only `Delimiter`
> and `Culture` are read — the two flags are silently ignored there.

```csharp no-compile
// Flags must go through the constructor:
var csv = new CsvManager<Product>(new CsvHelperOptions { IgnoreBadData = true });
```

## Notes

- First row is always treated as the header.
- The non-generic `CsvManager` reads each row into a `Dictionary<string, object>`.
- For typed `CsvManager<T>`, column names must match property names (or use CsvHelper `[Name]` attributes on the POCO).
- `WriteFile` returns an `IMemoryFile` with `ContentType = "text/csv"`.

## Examples

```csharp no-compile
// Read from uploaded file
var csvFile = formFile.ToNamedFile().ToBinaryFile();
var rows    = await new CsvManager().Read(csvFile);

// Read typed with semicolon delimiter
var products = await new CsvManager<Product>()
    .Read(csvString, new CsvHelperOptions { Delimiter = ";" });

// Write typed
IMemoryFile file = await new CsvManager<Order>()
    .WriteFile(orders, new CsvOptions { Delimiter = "\t" });

// Write non-generic from a list of dictionaries
string csv = await new CsvManager().Write(rows);
```
