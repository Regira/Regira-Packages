# Regira.Caching.Runtime

In-memory cache provider for the caching abstractions in [Regira Common](https://regira.github.io/Regira-Packages/src/Common/), built on [System.Runtime.Caching](https://www.nuget.org/packages/System.Runtime.Caching). `MemoryCacheProvider` implements `ICacheProvider` on `MemoryCache.Default`, so entries are shared process-wide and the optional key prefix (`MemoryCacheProvider.Options.Prefix`) is the only isolation between instances. An entry expires after the duration given to `Set`, in seconds, or else after `Options.DefaultDuration` — one hour by default.

## Installation

```xml
<PackageReference Include="Regira.Caching.Runtime" Version="6.*" />
```

## Documentation

- [Caching](https://regira.github.io/Regira-Packages/src/Common/#caching) — the `ICacheProvider` contract, and the dictionary-backed provider in `Regira.Common`

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
