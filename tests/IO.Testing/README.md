# IO.Testing

NUnit tests for the storage and compression packages: the file-system, network-share and zip file services of
[Common.IO.Storage](../../src/Common.IO.Storage/README.md), plus [IO.Storage.Azure](../../src/IO.Storage.Azure/README.md),
[IO.Storage.GitHub](../../src/IO.Storage.GitHub/README.md), [IO.Storage.SSH](../../src/IO.Storage.SSH/README.md) and
[IO.Compression.SharpZipLib](../../src/IO.Compression.SharpZipLib/README.md).

## Running

```bash
dotnet test tests/IO.Testing
```

The local fixtures need nothing; a few path tests run on Windows only and are skipped elsewhere.

The SSH fixtures (category `Containers`) start an SFTP server in Docker (`atmoz/sftp`), one per fixture, and pin the
host key it presents, so every test connects through the fingerprint check. They run when `REGIRA_PROVIDER_TESTS` is
`containers`, which `Regira.runsettings` sets, and are skipped when Docker is unavailable.

The Azure and GitHub fixtures (category `Network`) read their settings from this project's user secrets —
`appsettings.json` shows the shape but is not read. A fixture whose required secret is missing or blank is skipped,
not failed.

| Fixtures | Required secrets | Optional |
|---|---|---|
| Azure | `Storage:Azure:ConnectionString` (`UseDevelopmentStorage=true` for Azurite) | |
| GitHub | `Storage:GitHub:Uri`, `Storage:GitHub:Key` (a token that may write the repository's contents) | `Storage:GitHub:Branch` (default `main`) |

```bash
dotnet user-secrets set "Storage:Azure:ConnectionString" "UseDevelopmentStorage=true" --project tests/IO.Testing
```

The Azure fixtures empty the `test-container` blob container after each test, so point them at a throwaway account or
Azurite. The GitHub fixtures write under `test-write/{guid}` in the repository and delete those files afterwards. Leave
them out with `--filter "TestCategory!=Network"`, and the SSH fixtures with `TestCategory!=Containers`.
