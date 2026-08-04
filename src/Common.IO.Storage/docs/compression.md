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
var zip = new ZipManager();
```

### Create a ZIP archive

```csharp
// Returns a Stream containing the ZIP data
Stream archive = zip.Zip(files);

// Password-protected
Stream archive = zip.Zip(files, password: "s3cr3t");
```

`files` is `IEnumerable<IBinaryFile>` — the `FileName` property is used as the entry name inside the archive.

### Extract a ZIP archive

```csharp
BinaryFileCollection contents = await zip.Unzip(archiveStream);

// Password-protected
BinaryFileCollection contents = await zip.Unzip(archiveStream, password: "s3cr3t");
```

Returns a `BinaryFileCollection` (a disposable `List<IBinaryFile>`) — each entry has `FileName` and `Bytes` populated.

## When to use SharpZipLib vs ZipFileService

| Scenario | Recommendation |
|----------|---------------|
| Password protection | `IO.Compression.SharpZipLib` |
| Browse / modify archive entries via `IFileService` | `ZipFileService` from `IO.Storage` |
| Build archive from a list of files | Either — both work |

See [IO.Storage ZIP section](../README.md#zip--compression) for `ZipBuilder`, `ZipFileService`, and `ZipUtility`.
