# GitHub File Storage

Based on REST API [Get repository content](https://docs.github.com/en/rest/repos/contents?apiVersion=2022-11-28).

Implements `IFileService`. Supported operations: `Exists`, `GetBytes`, `GetStream`, `List`, `ListAsync` (NET10+).
`Save`, `Move`, and `Delete` are not supported and throw `NotImplementedException`.

## Tokens

[Create a token for readonly permissions on a selected Repository](https://github.com/settings/tokens?type=beta).
- Only select repositories: select repository
- Repository permissions: enable Contents -> Access: Read-only

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
