# Regira.IO.Storage.SimpleTCP

Sends files and text to a TCP server through the [Regira IO.Storage](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/) contracts, built on [SimpleTCP.Core](https://www.nuget.org/packages/SimpleTCP.Core). `TCPService` implements `ITextFileService`, connecting through a `TCPCommunicator` configured with a `TCPConfig` host and port. It is write-only: `Save` sends bytes, a stream or a text message — only the content, not the identifier — and the text overload returns the server's reply. Every other member throws.

## Installation

```xml
<PackageReference Include="Regira.IO.Storage.SimpleTCP" Version="6.*" />
```

Requires a TCP server listening on the configured host and port. The transport is plain TCP: nothing is encrypted or authenticated, so keep it to a trusted network.

## Documentation

- [Regira IO.Storage](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/) — the `IFileService` contract this package implements in part, and the storage backends that implement all of it

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
