# Common.Testing

NUnit tests for [Common](../../src/Common/README.md) (`Regira.Common`): the globalization utilities (countries,
cultures, languages) and the general-purpose utilities — collections, content types, dates and `DateTimeDefaults`,
dictionaries, dimensions, enums, numbers, objects, regular expressions, strings and types. `CacheProviderTests` runs
the same cases on `DictionaryCacheProvider` and on `MemoryCacheProvider` from
[Caching.Runtime](../../src/Caching.Runtime/README.md).

## Running

```bash
dotnet test tests/Common.Testing
```

No external requirements. `DictionaryUtilityTests` reads `Assets/countries.json` from the project folder.
