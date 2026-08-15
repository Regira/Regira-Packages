# Regira IO.Storage — Namespace Reference

> **AI Agent Rule**: You MUST use the exact namespaces listed in this file.
> You are NOT allowed to guess, invent, or assume any namespace.
> If a type is not listed here, look it up with `get_type` before using it.

Inject **`IFileService`** and register one backend. The file abstractions it moves around
(`IMemoryFile`, `IBinaryFile`) live in **`Regira.Common`**, not in a storage package — that split is the
most common source of a missing `using`.

---

## Abstractions and file types

| Namespace | Types |
|---|---|
| `Regira.IO.Storage.Abstractions` | `IFileService`, `ITextFileService`, `IFileProcessor`, `FileEntryTypes` |
| `Regira.IO.Storage` | `FileSearchObject`, `DefaultTextFileService` |
| `Regira.IO.Abstractions` | `IMemoryFile`, `IBinaryFile`, `IMemoryBytesFile`, `IMemoryStreamFile` — assembly **`Regira.Common`** |
| `Regira.IO.Extensions` | `MemoryFileExtensions`, `BinaryFileExtensions` — `GetBytes()`, `GetStream()`, `HasPath()`; assembly **`Regira.Common`** |
| `Regira.IO.Storage.Utilities` | `FileDataSource`, `BinaryFileCollectionExtensions` |
| `Regira.IO.Storage.Helpers` | `FileProcessor`, `FileNameHelper`, `ExportHelper`, `IExportHelper`, `OnExistsAction`, `Options` |

⚠️ `GetBytes()` / `GetStream()` are **extension methods in `Regira.IO.Extensions`**. Without that `using`,
an `IMemoryFile` appears to expose no way to read its content.

## Backends

| Namespace | Types |
|---|---|
| `Regira.IO.Storage.FileSystem` | `BinaryFileService`, `TextFileService`, `FileSystemOptions`, `FileServiceOptions`, `NetworkFileService`, `NetworkFileSystemOptions`, `NetworkShareCommunicator`, `FileSystemUtility` |
| `Regira.IO.Storage.Azure` | `BinaryBlobService`, `AzureOptions`, `AzureCommunicator` |
| `Regira.IO.Storage.SSH` | `SftpService`, `SftpConfig`, `SftpCommunicator` |
| `Regira.IO.Storage.GitHub` | `GitHubService`, `GitHubOptions`, `GitHubCommunicator`, `GitHubItem`, `GitHubItemType`, `GitHubExtensions` |
| `Regira.IO.Storage.SimpleTCP` | `TCPService`, `TCPConfig`, `TCPCommunicator` |

## Compression

| Namespace | Types |
|---|---|
| `Regira.IO.Storage.Compression` | `ZipFileService`, `ZipFileServiceFactory`, `ZipBuilder`, `ZipFileCommunicator`, `ZipUtility` |
| `Regira.IO.Compression.SharpZipLib` | `ZipManager` |

---

## Grouped by use case (quick lookup)

### Store and read a file on the local disk

```
Regira.IO.Storage.Abstractions   → IFileService
Regira.IO.Storage.FileSystem     → BinaryFileService, FileSystemOptions   (registration only)
Regira.IO.Abstractions           → IMemoryFile
Regira.IO.Extensions             → GetBytes(), GetStream()
```

### Entity attachments backed by storage

```
Regira.IO.Storage.Abstractions   → IFileService
Regira.IO.Storage.FileSystem     → BinaryFileService      // .WithAttachments(_ => new BinaryFileService(...))
Regira.IO.Storage.Azure          → BinaryBlobService      // the Azure Blob alternative
```

See `Regira.Entities` → `entities.instructions` → *Attachments* for the two registrations that wire this
to an owner entity.

### List or search stored files

```
Regira.IO.Storage.Abstractions   → IFileService, FileEntryTypes
Regira.IO.Storage               → FileSearchObject
```

### Zip a set of files

```
Regira.IO.Storage.Compression      → ZipBuilder, ZipFileService
Regira.IO.Compression.SharpZipLib  → ZipManager            (registration only)
```
