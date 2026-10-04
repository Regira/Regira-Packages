# Regira.IO.Compression.SharpZipLib

Password-protected ZIP archives for Regira, built on [SharpZipLib](https://www.nuget.org/packages/SharpZipLib). `ZipManager` zips a set of `IBinaryFile` items into a stream and unzips a stream into a `BinaryFileCollection`, each with an optional password. A password encrypts every entry with AES-256, which 7-Zip and WinZip open but the ZIP folders built into Windows Explorer do not. `Unzip` refuses an entry name that would leave the folder it is extracted into, and `MaxUnzippedSize` caps what an untrusted archive may unpack to. Choose it when an archive needs a password; otherwise the ZIP support built into [Regira IO.Storage](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/) covers building and browsing archives.

## Installation

```xml
<PackageReference Include="Regira.IO.Compression.SharpZipLib" Version="6.*" />
```

## Documentation

- [Compression](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/docs/compression.html) — zipping and unzipping with `ZipManager`, and when to use it rather than `ZipFileService`
- [ZIP / Compression in IO.Storage](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/#zip--compression) — the built-in `ZipFileService`, `ZipBuilder` and `ZipUtility`

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
