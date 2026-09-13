# Contributing

Thanks for your interest in the Regira packages. This repository is **source-available**: the code is public and issues are very welcome; external pull requests are accepted in a limited scope.

## Issues

Bug reports and feature requests go through [GitHub issues](https://github.com/Regira/Regira-Packages/issues). For a bug, include the package ID and version, a minimal repro, and the observed vs expected behavior.

## Pull requests

- **Welcome**: documentation fixes, small bug fixes with a test, broken-link/typo corrections.
- **By arrangement**: new features or API changes — open an issue first so we can align before you invest time.

By submitting a pull request you agree that your contribution is licensed under the repository's [Apache-2.0 license](LICENSE); contributions touching the commercially licensed packages (see [licensing.md](licensing.md)) are licensed to Regira bv for distribution under the [Regira Commercial License](legal/REGIRA-COMMERCIAL-LICENSE.md).

## Building

```sh
dotnet build Regira-Packages.slnx
dotnet test Regira-Packages.slnx --no-build
```

That is the full run, and it is what gates a release. `Regira.runsettings` points it at Docker and LocalDB,
so it covers the container-backed and SQL Server suites; leaving `--no-build` off just re-evaluates every
project before running.

### Faster inner loop

To skip the suites that need Docker, LocalDB, a local MongoDB or the network:

```sh
dotnet test Regira-Packages.slnx --no-build --filter "TestCategory!=Containers&TestCategory!=LocalDb&TestCategory!=MongoDb&TestCategory!=Network"
```

These categories are on the gated fixtures only. Nothing about the default run changes — run the full
one before you push.

### Test parallelism

`dotnet test` already runs the test assemblies concurrently (the CLI hands MSBuild `-maxcpucount`), so the
total is roughly the slowest single assembly rather than the sum. Within an assembly, parallelism is declared
in one place per project: `tests/{Project}/Properties/AssemblyInfo.cs`. A project that runs serially on
purpose has the file too, carrying the reason instead of the attribute. That convention covers the NUnit
projects; the two xUnit ones (`Entities.Web.Testing`, `Web.Security.Testing`) take xUnit's own default of
running test classes in parallel. A fixture that touches process-wide
state or a shared external resource is marked `[NonParallelizable]` where it is declared, with a note saying
what it shares — NUnit never overlaps the non-parallel shift with the parallel one, which is what keeps such
a fixture away from the rest.

Fixtures that drive a rate-limited remote API (GitHub, Azure) keep their own tests serial. Do not widen one
to `ParallelScope.All`.

Container-backed suites start a fresh container per run. To keep containers alive between runs while
iterating, set `REGIRA_CONTAINER_REUSE=1` and add `testcontainers.reuse.enable=true` to
`~/.testcontainers.properties`. It is off by default because a reused container carries its previous state
into the next run.

## Versioning

Every shipped change bumps the affected project's `<Version>` and adds a bullet under `## Unreleased` in [CHANGELOG.md](CHANGELOG.md) (format: `` `PackageId` x.y.z — summary ``). Do not bump packages you did not change — dependents are re-versioned by the release tooling. See [AGENTS.md](AGENTS.md) for the full contributor guide.
