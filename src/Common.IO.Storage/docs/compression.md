# Regira IO.Compression

Regira IO.Compression provides ZIP archive creation and extraction with optional password protection, as an alternative to the ZIP support built into [IO.Storage](../README.md#zip--compression).

## Projects

| Project | Package | Backend |
|---------|---------|---------|
| `IO.Compression.SharpZipLib` | `Regira.IO.Compression.SharpZipLib` | SharpZipLib |

## Installation

```xml
<PackageReference Include="Regira.IO.Compression.SharpZipLib" Version="6.*" />
```

## ZipManager

```csharp
using Regira.IO.Compression.SharpZipLib;

var zip = new ZipManager();
```

### Create a ZIP archive

<!-- no-compile -->
```csharp
// Returns a Stream containing the ZIP data
Stream archive = zip.Zip(files);

// Password-protected
Stream archive = zip.Zip(files, password: configuration["Exports:ZipPassword"]);
```

`files` is `IEnumerable<IBinaryFile>` — the `FileName` property is used as the entry name inside the archive, with
`/` as the separator and without a leading separator or drive. A name with a `..` segment throws
`UnauthorizedAccessException`, since `Unzip` would refuse it. The returned stream is rewound and ready to read or save.

A password encrypts every entry with AES-256. 7-Zip and WinZip open such an archive; the ZIP folders built into
Windows Explorer do not, since they read only the older ZipCrypto encryption.

### Extract a ZIP archive

<!-- no-compile -->
```csharp
BinaryFileCollection contents = await zip.Unzip(archiveStream);

// Password-protected
BinaryFileCollection contents = await zip.Unzip(archiveStream, password: configuration["Exports:ZipPassword"]);
```

Returns a `BinaryFileCollection` (a disposable `List<IBinaryFile>`) — each entry has `FileName` and `Bytes` populated.
The password opens AES and ZipCrypto entries alike, and a wrong one throws `ZipException`. The archive's central
directory sits at its end, so a stream that cannot seek is copied into memory first.

Entry names come back with `/` as the separator. An entry whose name would leave the folder it is extracted into — a
`..` segment, a leading separator or a drive — throws `UnauthorizedAccessException` before anything of it is read.

For an archive from an untrusted source, such as an upload, also cap what it may unpack to. `MaxUnzippedSize` counts
the bytes of all entries together while reading them, so an entry's declared size cannot hide a larger one; past it,
`Unzip` throws `InvalidDataException`. Left `null`, there is no limit.

```csharp
using Regira.IO.Compression.SharpZipLib;

var zip = new ZipManager { MaxUnzippedSize = 100 * 1024 * 1024 };   // 100 MB
```

## When to use SharpZipLib vs ZipFileService

| Scenario | Recommendation |
|----------|---------------|
| Password protection | `IO.Compression.SharpZipLib` |
| Browse / modify archive entries via `IFileService` | `ZipFileService` from `IO.Storage` |
| Build archive from a list of files | Either — both work |
| Unzip an archive from an untrusted source, such as an upload | `IO.Compression.SharpZipLib`, with `MaxUnzippedSize` — `ZipFileService` and `ZipUtility` set no limit |

See [IO.Storage ZIP section](../README.md#zip--compression) for `ZipBuilder`, `ZipFileService`, and `ZipUtility`.
