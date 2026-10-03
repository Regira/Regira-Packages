# Regira.IO.Storage.Azure

Azure Blob Storage backend for [Regira IO.Storage](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/), built on [Azure.Storage.Blobs](https://www.nuget.org/packages/Azure.Storage.Blobs). `BinaryBlobService` implements `IFileService` over one blob container, reached through an `AzureCommunicator` configured with `AzureOptions`.

## Installation

```xml
<PackageReference Include="Regira.IO.Storage.Azure" Version="6.*" />
```

Requires an Azure Storage account: `AzureOptions` takes its connection string and a container name. The container is created on first use unless `CreateContainerIfNotExists` is turned off.

## Documentation

- [Azure Blob Storage](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/#azure-blob-storage-binaryblobservice) — setup, the `AzureOptions` settings, and how `Save` sets a blob's content type
- [IO.Storage examples](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/docs/examples.html) — swapping backends by configuration and mirroring a GitHub folder to Azure
- [Regira IO.Storage](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/) — the shared `IFileService` contract and how files are identified

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
