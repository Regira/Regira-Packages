# Regira.Office.Excel.EPPlus

Excel backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [EPPlus](https://www.nuget.org/packages/EPPlus) 4. `ExcelManager` implements `IExcelService`, reading and writing rows as dictionaries. Of the four Excel backends it is the only one that writes a `DataSet` and takes a per-cell `TransformData` callback.

## Installation

```xml
<PackageReference Include="Regira.Office.Excel.EPPlus" Version="6.*" />
```

The package stays on EPPlus 4 (4.5.3.3), which is under a free licence; EPPlus 5 and later require a commercial licence.

## Documentation

- [Excel](https://regira.github.io/Regira-Packages/src/Common.Office/docs/excel/) — the shared reader and writer contracts, how the backends compare, and how EPPlus reads the `headers` argument
- [Excel examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/excel/examples.html) — import, export and per-cell value transformation

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).

The wrapped EPPlus library is licensed separately by its vendor; see its [package page](https://www.nuget.org/packages/EPPlus/4.5.3.3).
