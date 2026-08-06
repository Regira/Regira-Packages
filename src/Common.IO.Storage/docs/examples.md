# Regira IO.Storage — Examples

## Example 1: Swap storage backend without changing application code

Drive the backend choice from configuration so consuming code never changes.

```csharp
IFileService storage = config["Storage:Backend"] switch
{
    "azure" => new BinaryBlobService(new AzureCommunicator(new AzureOptions
               {
                   ConnectionString = config["Storage:Azure:ConnectionString"]!,
                   ContainerName    = config["Storage:Azure:Container"]!
               })),
    "sftp"  => new SftpService(new SftpCommunicator(new SftpConfig
               {
                   Host     = config["Storage:SSH:Host"]!,
                   UserName = config["Storage:SSH:Username"]!,
                   Password = config["Storage:SSH:Password"]
               })),
    "share" => new NetworkFileService(new NetworkShareCommunicator(new NetworkFileSystemOptions
               {
                   RootFolder = config["Storage:Share:Root"]!,   // \\server\share\folder — Windows only
                   UserName   = config["Storage:Share:Username"]!,
                   Password   = config["Storage:Share:Password"]
               })),
    _       => new BinaryFileService(new FileSystemOptions
               {
                   RootFolder = config["Storage:FileSystem:Root"]!
               })
};

services.AddSingleton<IFileService>(storage);
```

---

## Example 2: Download, transform, and re-upload

Combine `IFileService` with `IImageService` to process images in place.

```csharp
public async Task ResizeAndStore(IFileService storage, IImageService images, string path)
{
    var bytes = await storage.GetBytes(path);
    if (bytes == null) return;

    using var image   = images.Parse(bytes)!;
    using var resized = images.Resize(image, new ImageSize(800, 800));
    using var webp    = images.ChangeFormat(resized, ImageFormat.Webp);

    var newPath = Path.ChangeExtension(path, ".webp");
    await storage.Save(newPath, webp.GetBytes()!);
}
```

---

## Example 3: Mirror a GitHub folder to Azure

Use `ExportHelper` to copy files from `GitHubService` into `BinaryBlobService`.

```csharp
var github = new GitHubService(
    new GitHubOptions
    {
        Uri = "https://api.github.com/repos/acme/assets/contents/",
        Key = pat
    },
    jsonSerializer);

var communicator = new AzureCommunicator(new AzureOptions
{
    ConnectionString = connStr,
    ContainerName    = "assets"
});
await communicator.Open();
var azure = new BinaryBlobService(communicator);

await new ExportHelper(github, azure)
    .Export(new FileSearchObject { FolderUri = "images/", Recursive = true });
```

---

## Example 4: Bundle files into a ZIP for download

Collect all files in a folder and stream them to the caller as a single archive.

```csharp
public async Task<IMemoryFile> CreateZipExport(IFileService storage, string folder)
{
    var identifiers = await storage.List(new FileSearchObject
    {
        FolderUri = folder,
        Type      = FileEntryTypes.Files,
        Recursive = true
    });

    var files = await Task.WhenAll(identifiers.Select(async id =>
        new BinaryFileItem
        {
            Name  = id,
            Bytes = await storage.GetBytes(id)
        }));

    return await new ZipBuilder().For(files).Build();
}
```

---

## Example 5: Safe upload with a unique filename

Avoid overwriting existing files by finding the next available name before saving.

```csharp
public async Task<string> SafeUpload(
    IFileService storage, string folder, string filename, byte[] bytes)
{
    var helper = new FileNameHelper(storage);
    var path   = FileNameUtility.Combine(folder, filename);
    var safe   = await helper.NextAvailableFileName(path);
    return await storage.Save(safe, bytes);
}
```

Given `folder = "invoices"` and `filename = "report.pdf"`:
- First upload → `"invoices/report.pdf"`
- Second upload → `"invoices/report-(1).pdf"`
- Third upload → `"invoices/report-(2).pdf"`

---

## Example 6: Read and modify a ZIP archive in place

Open an existing archive, remove an outdated entry, add a replacement, and save the result.

```csharp
public async Task<IMemoryFile> ReplaceZipEntry(
    IMemoryFile sourceZip, string oldEntry, string newEntry, byte[] newBytes)
{
    using var zipService = new ZipFileService(new ZipFileCommunicator { SourceFile = sourceZip });

    if (await zipService.Exists(oldEntry))
        await zipService.Delete(oldEntry);

    await zipService.Save(newEntry, newBytes);

    // Retrieve the updated archive as a memory file
    return await new ZipBuilder()
        .For(await Task.WhenAll(
            (await zipService.List()).Select(async id =>
                new BinaryFileItem { FileName = id, Bytes = await zipService.GetBytes(id) })))
        .Build();
}
```

---

## Overview

1. [Index](../README.md) — Overview, interface, and implementation reference
1. **[Examples](examples.md)** — Backend swap, transform & re-upload, GitHub→Azure mirror, ZIP export, safe upload, modify archive
1. [Compression](compression.md) — Password-protected ZIP via SharpZipLib
