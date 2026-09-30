# Regira.Office.Excel.ClosedXML

Excel backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [ClosedXML](https://www.nuget.org/packages/ClosedXML). `ExcelManager` implements `IExcelService`, reading and writing rows as dictionaries; it has no typed sheets. Of the four Excel backends it is the only one whose `headers` argument narrows a read to the named columns.

## Installation

```xml
<PackageReference Include="Regira.Office.Excel.ClosedXML" Version="6.*" />
```

## Documentation

- [Excel](https://regira.github.io/Regira-Packages/src/Common.Office/docs/excel/) — the shared reader and writer contracts, how the backends compare, and where they differ
- [Excel examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/excel/examples.html) — import, export and header filtering

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
