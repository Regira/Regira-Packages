# Regira.Office.Excel.NpoiMapper

Excel backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [Npoi.Mapper](https://www.nuget.org/packages/Npoi.Mapper) over [NPOI](https://www.nuget.org/packages/NPOI). `ExcelManager` implements `IExcelService` and `ExcelManager<T>` implements the typed `IExcelService<T>`, binding columns to properties by name. Of the four Excel backends it is one of two with typed sheets.

## Installation

```xml
<PackageReference Include="Regira.Office.Excel.NpoiMapper" Version="6.*" />
```

NPOI is pinned to exactly 2.7.1: from 2.8.0 its binaries on nuget.org carry an Open Source Maintenance Fee EULA that requires a paid subscription above $10k annual revenue. The exact pin makes a later NPOI arriving through another package fail the restore rather than slip in.

## Documentation

- [Excel](https://regira.github.io/Regira-Packages/src/Common.Office/docs/excel/) — the shared reader and writer contracts, how the backends compare, and where they differ
- [Excel examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/excel/examples.html) — import, export and typed sheets

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
