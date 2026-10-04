# System.Testing

Tests for `ProcessHelper` in [Common.System](../../src/Common.System/README.md): capturing stdout and stderr past
the pipe buffer, keeping the streams apart, exit codes, and concurrent commands on one instance. NUnit.

## Running

```bash
dotnet test tests/System.Testing
```

Windows only: `ExecuteCommand` runs a `.bat` file, so on other platforms every test is skipped.
