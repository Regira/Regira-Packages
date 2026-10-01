# IO.Testing

NUnit tests for the storage and compression packages: the file-system, network-share and zip file services of
[Common.IO.Storage](../../src/Common.IO.Storage/README.md), plus [IO.Storage.Azure](../../src/IO.Storage.Azure/README.md),
[IO.Storage.GitHub](../../src/IO.Storage.GitHub/README.md), [IO.Storage.SSH](../../src/IO.Storage.SSH/README.md) and
[IO.Compression.SharpZipLib](../../src/IO.Compression.SharpZipLib/README.md).

## Running

```bash
dotnet test tests/IO.Testing
```

The local fixtures need nothing; a few path tests run on Windows only and are skipped elsewhere. The Azure, GitHub
and SSH fixtures (category `Network`) read their settings from this project's user secrets — `appsettings.json`
shows the shape but is not read. A fixture whose required secret is missing or blank is skipped, not failed.

| Fixtures | Required secrets | Optional |
|---|---|---|
| Azure | `Storage:Azure:ConnectionString` (`UseDevelopmentStorage=true` for Azurite) | |
| GitHub | `Storage:GitHub:Uri`, `Storage:GitHub:Key` (a token that may write the repository's contents) | `Storage:GitHub:Branch` (default `main`) |
| SSH | `Storage:SSH:Host`, `Storage:SSH:Port`, `Storage:SSH:Username` | `Storage:SSH:Password`, `Storage:SSH:ContainerName`, `Storage:SSH:HostKeyFingerprint` |

```bash
dotnet user-secrets set "Storage:Azure:ConnectionString" "UseDevelopmentStorage=true" --project tests/IO.Testing
```

The Azure fixtures empty the `test-container` blob container after each test, so point them at a throwaway account or
Azurite. The GitHub fixtures write under `test-write/{guid}` in the repository and delete those files afterwards. The
SSH fixtures carry `[Ignore]` and are skipped even with secrets set. Leave them all out with
`--filter "TestCategory!=Network"`.
