# IO.Storage — Example: Product Image Upload Service

> Context: An e-commerce API stores product images. Images are uploaded by staff, served publicly, and backed up nightly to Azure Blob Storage.

## DI Registration

```csharp
// Local disk for uploads
services.AddSingleton<IFileService>(_ =>
    new BinaryFileService(new FileSystemOptions { RootFolder = "/var/app/uploads" }));

// Azure Blob for backups — let IoC construct both the communicator and the service. Registered by its own type:
// a second IFileService registration would replace the local store wherever IFileService is injected
services.AddSingleton(new AzureOptions
{
    ConnectionString = configuration["Azure:Storage"],
    ContainerName    = "product-images"
});
services.AddSingleton<AzureCommunicator>();
services.AddSingleton<BinaryBlobService>();
```

## Upload an image

<!-- no-compile -->
```csharp
public async Task<string> UploadProductImage(int productId, IFormFile file)
{
    // the last segment of the client's name, whichever separator it used — never a path the client chose
    var fileName   = FileNameUtility.SanitizeFilename(file.FileName.Split('/', '\\')[^1]);
    var identifier = $"products/{productId}/{fileName}";

    // Ensure a unique name if the file already exists
    var helper = new FileNameHelper(_fileService);
    identifier = await helper.NextAvailableFileName(identifier);

    // no content type: the store types the file by its identifier's extension, never by the IFormFile.ContentType the client chose
    await using var stream = file.OpenReadStream();
    return await _fileService.Save(identifier, stream);
}
```

## List images for a product

<!-- no-compile -->
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

<!-- no-compile -->
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

<!-- no-compile -->
```csharp
public async Task BackupToAzure(IFileService local, IFileService azure)
    => await new ExportHelper(local, azure)
        .Export(new FileSearchObject { FolderUri = "products/", Recursive = true });
```

## ZIP download of all images for an order

<!-- no-compile -->
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
