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
dotnet test
```

## Versioning

Every shipped change bumps the affected project's `<Version>` and adds a bullet under `## Unreleased` in [CHANGELOG.md](CHANGELOG.md) (format: `` `PackageId` x.y.z — summary ``). Do not bump packages you did not change — dependents are re-versioned by the release tooling. See [AGENTS.md](AGENTS.md) for the full contributor guide.
