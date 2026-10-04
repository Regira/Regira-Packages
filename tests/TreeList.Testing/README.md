# TreeList.Testing

Tests for [TreeList](../../src/TreeList/README.md): building trees from flat data, one-to-many (family tree) and
many-to-many (cookbook) relations, failure cases such as self-referencing parents, and timings on generated (Bogus)
data. It also builds trees from a directory structure and from a project reference graph read by
[System.Projects](../../src/System.Projects/README.md). NUnit.

## Running

```bash
dotnet test tests/TreeList.Testing
```

No external requirements. The directory and project fixtures are written under the system temp directory and
removed afterwards.
