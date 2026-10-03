# Regira.Office.Excel.MiniExcel

Excel backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [MiniExcel](https://www.nuget.org/packages/MiniExcel). `ExcelManager` implements `IExcelService` and `ExcelManager<T>` implements the typed `IExcelService<T>`. Of the four Excel backends it is one of two with typed sheets, and the only one that streams.

## Installation

```xml
<PackageReference Include="Regira.Office.Excel.MiniExcel" Version="6.*" />
```

## Documentation

- [Excel](https://regira.github.io/Regira-Packages/src/Common.Office/docs/excel/) — the shared reader and writer contracts, how the backends compare, and where they differ
- [Excel examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/excel/examples.html) — import, export and typed sheets

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
