# Office.Excel.Testing

Tests for the Excel backends [Excel.ClosedXML](../../src/Excel.ClosedXML/README.md),
[Excel.EPPlus](../../src/Excel.EPPlus/README.md), [Excel.MiniExcel](../../src/Excel.MiniExcel/README.md) and
[Excel.NpoiMapper](../../src/Excel.NpoiMapper/README.md). One set of scenarios (`ExcelTestExtensions`) runs against
each backend: typed, untyped and dictionary round-trips, duplicate headers, sheet export and JSON input. NUnit.

## Running

```bash
dotnet test tests/Office.Excel.Testing
```

No external requirements. Inputs are in `Assets/Input`; each backend writes to `Assets/Output/{Backend}`.
