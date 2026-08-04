# IO.Storage — Example: Product Image Upload Service

> Context: An e-commerce API stores product images. Images are uploaded by staff, served publicly, and backed up nightly to Azure Blob Storage.

## DI Registration

```csharp
// Program.cs
// Local disk for uploads
services.AddSingleton<IFileService>(_ =>
    new BinaryFileService(new FileSystemOptions { RootFolder = "/var/app/uploads" }));

// Azure Blob for backups — let IoC construct both the communicator and the service
services.AddSingleton(new AzureOptions
{
    ConnectionString = configuration["Azure:Storage"],
    ContainerName    = "product-images"
});
services.AddSingleton<AzureCommunicator>();
services.AddSingleton<IFileService, BinaryBlobService>();
```

## Upload an image

```csharp
public async Task<string> UploadProductImage(int productId, IFormFile file)
{
    var bytes      = await file.GetBytesAsync();
    var identifier = $"products/{productId}/{file.FileName}";

    // Ensure a unique name if the file already exists
    var helper = new FileNameHelper(_fileService);
    identifier = await helper.NextAvailableFileName(identifier);

    return await _fileService.Save(identifier, bytes, file.ContentType);
}
```

## List images for a product

```csharp
public async Task<IEnumerable<string>> GetProductImages(int productId)
    => await _fileService.List(new FileSearchObject
    {
        FolderUri  = $"products/{productId}/",
        Extensions = [".jpg", ".webp", ".png"],
        Recursive  = false,
        Type       = FileEntryTypes.Files
    });
```

## Stream images for a product (NET10+)

```csharp
public async IAsyncEnumerable<string> StreamProductImages(int productId)
{
    var so = new FileSearchObject
    {
        FolderUri  = $"products/{productId}/",
        Extensions = [".jpg", ".webp", ".png"],
        Recursive  = false,
        Type       = FileEntryTypes.Files
    };
    await foreach (var identifier in _fileService.ListAsync(so))
        yield return identifier;
}
```

## Nightly backup via ExportHelper

```csharp
public async Task BackupToAzure(IFileService local, IFileService azure)
    => await new ExportHelper(local, azure)
        .Export(new FileSearchObject { FolderUri = "products/", Recursive = true });
```

## ZIP download of all images for an order

```csharp
public async Task<IMemoryFile> ZipOrderImages(IEnumerable<string> identifiers)
{
    var files = new List<BinaryFileItem>();
    foreach (var id in identifiers)
    {
        var bytes = await _fileService.GetBytes(id);
        if (bytes != null)
            files.Add(new BinaryFileItem { FileName = FileNameUtility.GetCleanFileName(id), Bytes = bytes });
    }
    return files.Zip();
}
```
