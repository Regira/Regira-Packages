# Regira.IO.Storage.SSH

SFTP backend for [Regira IO.Storage](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/), built on [SSH.NET](https://www.nuget.org/packages/SSH.NET). `SftpService` implements `IFileService` over a remote base directory, reached through an `SftpCommunicator` configured with `SftpConfig`.

## Installation

```xml
<PackageReference Include="Regira.IO.Storage.SSH" Version="6.*" />
```

Requires an SSH server with SFTP access; `SftpConfig` signs in with a username and password. Set its `HostKeyFingerprint` to the server's SHA-256 host key fingerprint (`ssh-keyscan <host> | ssh-keygen -lf -` lists one per key type: take the `SHA256:…` of the type the server negotiates) so a server presenting another key is refused: left empty, any host key is accepted.

## Documentation

- [SSH / SFTP](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/#ssh--sftp-sftpservice) — setup, the `SftpConfig` settings, and the connection's lifetime
- [File identification](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/#file-identification) — identifiers relative to the base directory, and the check that keeps them inside it
- [IO.Storage examples](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/docs/examples.html) — swapping backends by configuration

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
