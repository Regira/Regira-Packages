# Office.Excel.Testing

Tests for the Excel backends [Excel.ClosedXML](../../src/Excel.ClosedXML/README.md),
[Excel.EPPlus](../../src/Excel.EPPlus/README.md), [Excel.MiniExcel](../../src/Excel.MiniExcel/README.md) and
[Excel.NpoiMapper](../../src/Excel.NpoiMapper/README.md). One set of scenarios (`ExcelTestExtensions`) runs against
each backend: typed, untyped and dictionary round-trips, duplicate headers, sheet export and JSON input. NUnit.

`ExcelTestExtensions.EdgeCases.cs` holds the edge cases every backend is held to: writing empty and `null` sheets, rows
with different keys, a key holding values of different types and sheet names Excel refuses; reading an empty sheet,
blank and repeated headers, and the `headers` filter. Their input workbooks are built with ClosedXML directly. EPPlus
reads `headers` as column keys instead of a filter, so its fixture tests that in place of the filter scenario.

## Running

```bash
dotnet test tests/Office.Excel.Testing
```

No external requirements. Inputs are in `Assets/Input`; each backend writes to `Assets/Output/{Backend}`.
