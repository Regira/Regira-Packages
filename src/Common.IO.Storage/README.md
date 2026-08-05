# Regira IO.Storage

Regira IO.Storage provides a **unified abstraction** for file storage operations across multiple backends. All implementations share the same `IFileService` interface, making storage backends interchangeable in consuming code.

## Projects

| Project | Package | Backend | File services |
|---------|---------|---------|-------|
| `Common.IO.Storage` | `Regira.IO.Storage` | Local file system | `BinaryFileService` |
|  | | Windows network share (UNC) | `NetworkFileService` |
|  | | Zip file system | `ZipFileService` |
| `IO.Storage.Azure` | `Regira.IO.Storage.Azure` | Azure Blob Storage | `BinaryBlobService` |
| `IO.Storage.SSH` | `Regira.IO.Storage.SSH` | SFTP / SSH server | `SftpService` |
| `IO.Storage.GitHub` | `Regira.IO.Storage.GitHub` | GitHub repository | `GitHubService` (writes commit to a branch) |

## Installation

```xml
<!-- Local file system (also ships the shared abstractions) -->
<PackageReference Include="Regira.IO.Storage" Version="6.*" />

<!-- Azure Blob Storage -->
<PackageReference Include="Regira.IO.Storage.Azure" Version="6.*" />

<!-- SSH / SFTP -->
<PackageReference Include="Regira.IO.Storage.SSH" Version="6.*" />

<!-- GitHub -->
<PackageReference Include="Regira.IO.Storage.GitHub" Version="6.*" />
```

## Quick Start

```csharp no-compile
services.AddSingleton<IFileService>(_ =>
    new BinaryFileService(new FileSystemOptions { RootFolder = "/var/app/uploads" }));

// In a service
var bytes = await storage.GetBytes("invoices/2024/inv-001.pdf");
await storage.Save("exports/report.pdf", pdfBytes);
```

## IFileService

All backends implement this interface. **Identifiers** are relative paths within the storage root (e.g. `"folder/file.pdf"`). **URIs** are the absolute addresses returned by `GetAbsoluteUri`.

### Read

```csharp no-compile
Task<bool>                Exists(string identifier)
Task<byte[]?>             GetBytes(string identifier)
Task<Stream?>             GetStream(string identifier)
Task<IEnumerable<string>> List(FileSearchObject? so = null)
```

### Write

```csharp no-compile
Task<string> Save(string identifier, byte[] bytes,  string? contentType = null)
Task<string> Save(string identifier, Stream stream, string? contentType = null)
Task         Move(string sourceIdentifier, string targetIdentifier)
Task         Delete(string identifier)
```

> `Save` returns the final identifier used — it may differ if the backend renames on conflict.

### URI helpers

```csharp no-compile
string  Root { get; }                        // storage root URI / path
string  GetAbsoluteUri(string identifier)    // relative → absolute
string  GetIdentifier(string uri)            // absolute → relative
string? GetRelativeFolder(string identifier) // extract parent folder
```

## File Identification

Files are addressed uniformly across all backends using three coordinated concepts.

### `IFileService.Root`

Every `IFileService` implementation exposes a `Root` — the backend-specific base address for that storage scope:

| Implementation | `Root` example |
|----------------|----------------|
| `BinaryFileService` | `/var/app/storage` (local path) |
| `BinaryBlobService` | `https://account.blob.core.windows.net/my-container` |
| `SftpService` | `/home/deploy/files` (remote base directory) |
| `GitHubService` | `https://api.github.com/repos/owner/repo/contents/` |

All `IFileService` methods (`GetBytes`, `Save`, `List`, …) accept and return **identifiers** — paths relative to this root — keeping consuming code independent of the backend.

### `IBinaryFile.Identifier` and `IBinaryFile.Prefix`

`BinaryFileItem` (and anything implementing `IBinaryFile` / `IStorageFile`) carries two address parts:

| Property | Description | Example |
|----------|-------------|---------|
| `Prefix` | Sub-folder path below the root, excluding the filename | `"invoices/2024/"` |
| `Identifier` | `Prefix + FileName` — the relative key used in all `IFileService` calls | `"invoices/2024/inv-001.pdf"` |
| `Path` | Full absolute address — `Root + Identifier` | `/var/app/storage/invoices/2024/inv-001.pdf` |

```
Root        →  /var/app/storage/
Prefix      →                   invoices/2024/
FileName    →                                 inv-001.pdf
Identifier  →                   invoices/2024/inv-001.pdf
Path        →  /var/app/storage/invoices/2024/inv-001.pdf
```

### Converting between identifier and absolute URI

`IFileService` provides helpers to move between the two representations:

```csharp
IFileService service = new BinaryFileService(new FileSystemOptions { RootFolder = "/var/app/storage" });

string absolute = service.GetAbsoluteUri("invoices/2024/inv-001.pdf");
// → /var/app/storage/invoices/2024/inv-001.pdf  (or the equivalent Azure/SFTP URL)

string identifier = service.GetIdentifier("/var/app/storage/invoices/2024/inv-001.pdf");
// → invoices/2024/inv-001.pdf

string? folder = service.GetRelativeFolder("invoices/2024/inv-001.pdf");
// → invoices/2024
```

Use `Identifier` as the portable key that survives a backend swap; only resolve to `Path` when you need the actual physical/network address.

## FileSearchObject

Filter parameter for `List()`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `FolderUri` | `string?` | `null` | Restrict to this folder |
| `Extensions` | `ICollection<string>?` | `null` | Filter by extension — e.g. `[".jpg", ".png"]` |
| `Recursive` | `bool` | `false` | Include subdirectories |
| `Type` | `FileEntryTypes` | `All` | `Files`, `Directories`, or `All` |

```csharp
IFileService storage = new BinaryFileService(new FileSystemOptions { RootFolder = "/var/app/storage" });

var images = await storage.List(new FileSearchObject
{
    FolderUri  = "products/",
    Extensions = [".jpg", ".webp"],
    Recursive  = true,
    Type       = FileEntryTypes.Files
});
```

## Implementations

### File System (`BinaryFileService`)

Stores files on the local disk.

**Package:** `Regira.IO.Storage`

```csharp
var service = new BinaryFileService(new FileSystemOptions { RootFolder = "/var/app/storage" });
```

**Network shares** — for a UNC path protected by a username & password, use `NetworkFileService` with a `NetworkShareCommunicator`. The communicator authenticates against the share lazily on the first file operation (or eagerly via `await communicator.Open()`); dispose it on application shutdown to release the connection.

```csharp no-compile
services.AddSingleton(new NetworkFileSystemOptions
{
    RootFolder = @"\\fileserver\share\uploads",
    UserName   = configuration["Storage:Share:UserName"]!,
    Password   = configuration["Storage:Share:Password"],
    Domain     = configuration["Storage:Share:Domain"]   // optional — or "DOMAIN\user" as UserName
});
services.AddSingleton<NetworkShareCommunicator>();
services.AddSingleton<IFileService, NetworkFileService>();
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `RootFolder` | `string` | *(required)* | UNC path — `\\server\share[\folder]` |
| `UserName` | `string` | *(required)* | Login username |
| `Password` | `string?` | `null` | Login password |
| `Domain` | `string?` | `null` | Optional domain, prepended as `DOMAIN\user` |
| `Contained` | `bool` | `true` | Reject identifiers that escape `RootFolder` |

> Windows only (uses the WNet API). On Linux/macOS, mount the share at OS level (e.g. `mount.cifs`) and use plain `FileSystemOptions`. Connections are ref-counted per share + user across the process — the share connection is only released when the last communicator on it is disposed. If the connection is cancelled outside the process (e.g. `net use /delete`), call `await communicator.Reconnect()` to re-establish it; `Close()` only drops this communicator's reference.

**Text files** — use `TextFileService` directly, or wrap any `IFileService` with the `DefaultTextFileService` decorator:

```csharp no-compile
var text = new DefaultTextFileService(anyFileService, Encoding.UTF8);
string? content = await text.GetContents("config/app.json");
await text.Save("config/app.json", jsonString);
```

---

### Azure Blob Storage (`BinaryBlobService`)

**Package:** `Regira.IO.Storage.Azure` — **NuGet dependency:** `Azure.Storage.Blobs`

```csharp
var communicator = new AzureCommunicator(new AzureOptions
{
    ConnectionString = "DefaultEndpointsProtocol=https;AccountName=…",
    ContainerName    = "my-container"
});
await communicator.Open();   // idempotent — safe to call multiple times

var service = new BinaryBlobService(communicator);
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ConnectionString` | `string?` | `null` | Azure Storage connection string |
| `ContainerName` | `string?` | `null` | Blob container name |
| `CreateContainerIfNotExists` | `bool` | `true` | Create the container when missing — set `false` to fail fast on misconfigured names |

---

### SSH / SFTP (`SftpService`)

**Package:** `Regira.IO.Storage.SSH` — **NuGet dependency:** `SSH.NET`

```csharp
var communicator = new SftpCommunicator(new SftpConfig
{
    Host          = "sftp.example.com",
    Port          = 22,
    UserName      = "deploy",
    Password      = "s3cr3t",
    ContainerName = "/home/deploy/files"
});

var service = new SftpService(communicator);
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Host` | `string` | *(required)* | SSH server hostname |
| `Port` | `int` | `22` | SSH port |
| `UserName` | `string` | *(required)* | Login username |
| `Password` | `string?` | `null` | Login password |
| `ContainerName` | `string?` | `"/"` | Remote base directory |
| `Contained` | `bool` | `true` | Reject identifiers that escape `ContainerName` |

> `SftpCommunicator` holds a single persistent connection. Dispose it on application shutdown.

---

### GitHub (`GitHubService`)

**Package:** `Regira.IO.Storage.GitHub` — **NuGet dependency:** none (uses `HttpClient`)

```csharp
ISerializer jsonSerializer = new JsonSerializer();   // e.g. Regira.Serializing.Newtonsoft

var service = new GitHubService(
    new GitHubCommunicator(new GitHubOptions
    {
        Uri       = "https://api.github.com/repos/owner/repo",
        Key       = "ghp_xxxxxxxxxxxx",   // PAT — optional for public-repo reads
        UserAgent = "MyApp/1.0"
    }),
    jsonSerializer
);
```

| Option | Type | Description |
|--------|------|-------------|
| `Uri` | `string` | GitHub API repository endpoint (`https://api.github.com/repos/{owner}/{repo}`) |
| `Key` | `string?` | Personal Access Token (required for writes and private repos) |
| `UserAgent` | `string?` | `User-Agent` header — GitHub requires a non-empty value |
| `Branch` | `string` | Branch used for writes (default `"main"`) |
| `CommitMessage` | `string?` | Commit message for `Save`/`Delete` (default: auto-generated) |
| `ContentPath` | `string?` | Sub-path within the repository used as `Root` |

Fully implements `IFileService` — `Save`/`Move`/`Delete` create commits on `Branch`, so it's not suited for high-frequency writes.

## ZIP / Compression

### ZipFileService — browse an archive via IFileService

`ZipFileService` implements `IFileService` and `IDisposable`. Construct it with a `ZipFileCommunicator` that points to an existing archive or starts a fresh one:

```csharp no-compile
// Open an existing zip file
using var zipService = new ZipFileService(new ZipFileCommunicator { SourceFile = existingZip });
var entries = await zipService.List();
var bytes   = await zipService.GetBytes("report.pdf");

// Start a new empty archive
using var newZip = new ZipFileService(new ZipFileCommunicator());
await newZip.Save("data.csv", csvBytes);
```

| `ZipFileCommunicator` | Type | Description |
|-----------------------|------|-------------|
| `SourceFile` | `IMemoryFile?` | Existing zip to open — omit to start empty |
| `Password` | `string?` | Currently not consumed by `ZipFileService` — for password-protected ZIPs use `Regira.IO.Compression.SharpZipLib` (see [Compression](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/docs/compression.html)) |

> `ZipFileServiceFactory` is a convenience wrapper: `new ZipFileServiceFactory().Create(sourceFile, password)` is equivalent to constructing `ZipFileService` directly.

### ZipBuilder — create archives

```csharp
byte[] pdfBytes = [/* … */], csvBytes = [/* … */];

IMemoryFile zip = await new ZipBuilder()
    .For([new BinaryFileItem { FileName = "report.pdf", Bytes = pdfBytes },
          new BinaryFileItem { FileName = "data.csv",   Bytes = csvBytes }])
    .Build();
```

### ZipUtility — zip/unzip helpers

`Zip` is an extension method; `Unzip` is a static method on `ZipUtility`.

```csharp no-compile
IMemoryFile zipFromFiles    = files.Zip();                             // collection → zip
IMemoryFile zipFromPaths    = paths.Zip(baseFolder: "/var/exports");   // paths → zip
BinaryFileCollection items  = ZipUtility.Unzip(existingZip);           // zip → collection
string[] extracted          = ZipUtility.Unzip(existingZip, targetDirectory: "/tmp/out");
```

## Helpers

### FileProcessor — recursive processing

```csharp
IFileService fileService = new BinaryFileService(new FileSystemOptions { RootFolder = "/var/app/storage" });

await new FileProcessor(fileService).ProcessFiles(
    new FileSearchObject { FolderUri = "exports/", Recursive = true },
    async (identifier, svc) => { /* process each file */ }
);
```

### FileNameHelper — unique filenames

```csharp
IFileService fileService = new BinaryFileService(new FileSystemOptions { RootFolder = "/var/app/storage" });

var helper = new FileNameHelper(fileService);
string safe = await helper.NextAvailableFileName("invoices/report.pdf");
// → "invoices/report-(1).pdf" when "invoices/report.pdf" already exists
```

Customise the pattern: `new FileNameHelper.Options { NumberPattern = " ({0})" }`

### ExportHelper — copy between services

```csharp
IFileService source = new BinaryFileService(new FileSystemOptions { RootFolder = "/var/app/storage" });
IFileService target = new BinaryFileService(new FileSystemOptions { RootFolder = "/mnt/backup" });

await new ExportHelper(source, target)
    .Export(new FileSearchObject { FolderUri = "backups/", Recursive = true });
```

### FileNameUtility — path helpers

```csharp no-compile
FileNameUtility.GetAbsoluteUri("folder/file.txt", root)
FileNameUtility.GetRelativeUri(absolutePath, root)
FileNameUtility.GetCleanFileName("folder/sub/file.txt")  // → "file.txt"
FileNameUtility.Combine("folder", "sub", "file.txt")
FileNameUtility.SanitizeFilename(@"CON\report.txt")      // → @"_XXX_\report.txt" — replaces path segments that exactly match a Windows reserved name ("con.txt" is left as-is)
FileNameUtility.GetUncShareRoot(@"\\server\share\sub")   // → @"\\server\share" (null for non-UNC)
```

## Overview

1. **[Index](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/)** — Overview, interface, and implementation reference
1. [Examples](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/docs/examples.html) — Backend swap, transform & re-upload, GitHub→Azure mirror, ZIP export, safe upload
1. [Compression](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/docs/compression.html) — Password-protected ZIP via SharpZipLib
