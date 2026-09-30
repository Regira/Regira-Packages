# Serializing.Testing

Tests for [Serializing.Newtonsoft](../../src/Serializing.Newtonsoft/README.md): round-tripping nullable `Guid`
values, reading and writing booleans as numbers, and enums as strings through its `JsonSerializer`. NUnit.

## Running

```bash
dotnet test tests/Serializing.Testing
```

No external requirements.
