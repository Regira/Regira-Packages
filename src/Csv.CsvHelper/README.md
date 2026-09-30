# Regira.Office.Csv.CsvHelper

CSV backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [CsvHelper](https://www.nuget.org/packages/CsvHelper). `CsvManager` implements `ICsvService`, reading and writing rows as dictionaries, and `CsvManager<T>` implements the typed `ICsvService<T>`. `CsvHelperOptions` extends the shared `CsvOptions` with flags for skipping malformed rows and keeping whitespace.

## Installation

```xml
<PackageReference Include="Regira.Office.Csv.CsvHelper" Version="6.*" />
```

## Documentation

- [CSV](https://regira.github.io/Regira-Packages/src/Common.Office/docs/csv/) — the read and write contract, the options and which of them only take effect through the constructor, and examples

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
