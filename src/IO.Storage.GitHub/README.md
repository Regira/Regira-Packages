# Regira.IO.Storage.GitHub

Stores files in a GitHub repository through the [Regira IO.Storage](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/) contracts, over the REST API's [repository contents](https://docs.github.com/en/rest/repos/contents) endpoints. `GitHubService` implements `IFileService` with a `GitHubCommunicator` and an `ISerializer`: reads come from the repository, and `Save`, `Move` and `Delete` each create a commit on the configured branch, so it is not suited to high-frequency writes.

## Installation

```xml
<PackageReference Include="Regira.IO.Storage.GitHub" Version="6.*" />
```

Public repositories can be read without a token. Writes and private repositories need a [fine-grained personal access token](https://github.com/settings/tokens?type=beta) limited to the repository, with the *Contents* permission — *Read-only* for reading, *Read and write* to save, move or delete.

## Documentation

- [GitHub storage](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/#github-githubservice) — construction, options and branch handling
- [Regira IO.Storage](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/) — the `IFileService` contract and the other storage backends

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
