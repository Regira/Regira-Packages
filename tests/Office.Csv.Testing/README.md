# Office.Csv.Testing

Tests for [Csv.CsvHelper](../../src/Csv.CsvHelper/README.md): reading and writing CSV, typed and as dictionaries,
from fixture files and generated (Bogus) data. NUnit.

## Running

```bash
dotnet test tests/Office.Csv.Testing
```

No external requirements. Inputs are in `Assets/Input`; the tests write to `Assets/Output`.
