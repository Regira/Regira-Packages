# GitHub File Storage

Based on REST API [Get repository content](https://docs.github.com/en/rest/repos/contents?apiVersion=2022-11-28).

Implements `IFileService`. Supported operations: `Exists`, `GetBytes`, `GetStream`, `List`, `ListAsync` (NET10+).
`Save`, `Move`, and `Delete` are not supported and throw `NotImplementedException`.

## Tokens

[Create a token for readonly permissions on a selected Repository](https://github.com/settings/tokens?type=beta).
- Only select repositories: select repository
- Repository permissions: enable Contents -> Access: Read-only