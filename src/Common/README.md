# Regira Common

`Regira.Common` is the shared foundation library used by every other Regira project. It provides file abstractions, general-purpose utilities, normalising, caching, serialising, and lightweight DAL contracts — no external dependencies.

## Projects

| Project | Package | Description |
|---------|---------|-------------|
| `Common` | `Regira.Common` | Core abstractions and utilities |

## Installation

```xml
<PackageReference Include="Regira.Common" Version="6.*" />
```

Most Regira packages pull this in as a transitive dependency, so you rarely need to reference it explicitly.

---

## IO Abstractions

The IO abstraction hierarchy is the most widely referenced part of this library. It is used as the common file contract throughout IO.Storage, Drawing, and Office projects.

### Interface hierarchy

```
┌───────────────────┐    ┌───────────────────┐
│ IMemoryBytesFile  │    │ IMemoryStreamFile │
└─────────┬─────────┘    └─────────┬─────────┘
          └────────────┬───────────┘
                Input Interfaces
                       │
                ▼──────────────▼
                  IMemoryFile
                ┌──────────────┐
                │ INamedFile   │──▶ FileName
                └──────────────┘
                       │
                ┌──────────────┐
                │ IStorageFile │──▶ Identifier, Path, Prefix
                └──────────────┘
                       │
                ┌──────────────┐
                │ IBinaryFile  │
                └──────────────┘
                       │
                ┌──────────────┐
                │  ITextFile   │──▶ Contents
                └──────────────┘
```

### BinaryFileItem

The standard concrete implementation of `IBinaryFile`.

```csharp
var file = new BinaryFileItem
{
    FileName    = "invoice.pdf",
    Bytes       = pdfBytes,
    ContentType = "application/pdf"
};
```

Implicit conversions are available from `byte[]` and `Stream`:

```csharp
BinaryFileItem f1 = pdfBytes;
BinaryFileItem f2 = someStream;
```

### Extension methods

**`BinaryFileExtensions`** — work with any `IMemoryFile`:

```csharp
byte[]? bytes  = file.GetBytes();
Stream? stream = file.GetStream();
long    length = file.GetLength();
bool    hasIt  = file.HasContent();
```

**`BinaryFileExtensions`** — factory helpers:

```csharp
IBinaryFile f = bytes.ToBinaryFile("invoice.pdf");
IBinaryFile f = stream.ToBinaryFile("data.csv");
IBinaryFile f = memoryFile.ToBinaryFile("copy.pdf");
```

---

## ContentTypeUtility

Auto-detect MIME types from file extensions or byte sequences.

```csharp
string mime = ContentTypeUtility.GetContentType("report.pdf");  // "application/pdf"
string? ext = ContentTypeUtility.GetExtension("image/webp");    // "webp" (no leading dot)
```

Register additional mappings (extensions without leading dot, each mapped to one or more MIME types):

```csharp
ContentTypeUtility.Extend(new Dictionary<string, string[]>
{
    { "abc", ["application/x-abc"] }
});
```

---

## FileUtility

Conversions between bytes, streams, strings, and Base64.

```csharp
byte[]  bytes   = FileUtility.GetBytes(stream);
Stream  stream  = FileUtility.GetStream(bytes);
string  text    = FileUtility.GetString(bytes, Encoding.UTF8);
string  b64     = FileUtility.GetBase64String(bytes);
byte[]  back    = FileUtility.GetBytesFromString(b64);           // Base64 → bytes
```

---

## Utilities

### StringUtility / RegexUtility

```csharp
// Validation
bool ok = RegexUtility.IsValidEmail("alice@example.com");
bool ok = RegexUtility.IsValidUrl("https://example.com");
bool ok = RegexUtility.IsValidPhoneNumber("+32 123 456 789");
```

### CollectionUtility

```csharp
List<T>           list     = source.AsList<T>();
IEnumerable<T>    distinct = source.DistinctBy(x => x.Id);
```

### TypeUtility

```csharp
bool isSimple     = TypeUtility.IsSimpleType(typeof(int));
bool isNullable   = TypeUtility.IsNullableType(typeof(int?));
bool isCollection = TypeUtility.IsTypeACollection(typeof(List<string>));
Type underlying   = TypeUtility.GetSimpleType(typeof(int?));   // int
```

### ObjectUtility

```csharp
// Merge non-null properties from source onto target
ObjectUtility.Merge(source, target);

// Populate from an anonymous object or dictionary
ObjectUtility.Fill(target, new { Name = "Alice", Age = 30 });
```

### UriUtility

```csharp
string slug    = UriUtility.Slugify("Héllo Wörld!");           // "hello-world"
string dataUrl = UriUtility.ToBase64ImageUrl(bytes, "image/png");
string abs     = UriUtility.ToAbsoluteUri("../images/logo.png");
```

### DimensionsUtility

```csharp
float inches = DimensionsUtility.MmToIn(25.4f);   // 1.0f
float mm     = DimensionsUtility.InToMm(1.0f);    // 25.4f
```

---

## Dimensions

Shared geometric primitives used by Drawing and PDF projects.

### Size2D

```csharp
var size = new Size2D(800, 600);
var half = size / 2;          // (400, 300)
```

Implicit conversions: from `int` (square), `int[]`, `float[]`.

### Position2D

CSS-style distance from each edge (nullable floats).

```csharp
var pos = new Position2D { Top = 10, Left = 20 };
```

### LengthUnit

```csharp
public enum LengthUnit { Points, Inches, Millimeters, Percent }
```

Used by Drawing DTOs and PDF layout engines when specifying measurements. See [Drawing → DTOs & API Integration](https://regira.github.io/Regira-Packages/src/Common.Media#dtos--api-integration).

---

## Normalizing

Attribute-driven string and object normalisation.

### INormalizer

```csharp
public interface INormalizer
{
    string? Normalize(string? input);
}
```

`DefaultNormalizer` removes diacritics, normalises whitespace, and optionally transforms case:

```csharp
var normalizer = new DefaultNormalizer(new NormalizeOptions
{
    RemoveDiacritics = true,
    Transform        = TextTransform.ToLowerCase
});

string? result = normalizer.Normalize("Héllo Wörld");  // "hello world"
```

### NormalizedAttribute

Decorate properties so `ObjectNormalizer` knows which ones to normalise.

```csharp
[Normalized]
public string? Name { get; set; }

[Normalized(SourceProperty = nameof(Title))]
public string? NormalizedTitle { get; set; }
```

```csharp
var normalizer = new ObjectNormalizer();
normalizer.HandleNormalize(myEntity, recursive: true);
```

---

## Caching

### ICacheProvider

```csharp
public interface ICacheProvider
{
    IEnumerable<string> Keys { get; }
    object?             this[string key] { get; }
    T?    Get<T>(string key);
    void  Set<T>(string key, T value);
    void  Remove(string key);
    void  RemoveAll();
}
```

### DictionaryCacheProvider

Thread-safe in-memory cache backed by `ConcurrentDictionary`. Supports an optional key prefix to namespace entries.

```csharp
var cache = new DictionaryCacheProvider("products");
cache.Set("list", products);
var list = cache.Get<List<Product>>("list");
```

---

## Serializing

### ISerializer

```csharp
public interface ISerializer
{
    string  Serialize<T>(T value);
    T?      Deserialize<T>(string? input);
    object? Deserialize(string? input, Type type);
}
```

`XmlSerializer` ships in `Common`. JSON serialiser implementations live in separate packages. Inject `ISerializer` in consuming code to stay agnostic.

---

## Security

Thin contracts for pluggable encryption and hashing.

```csharp
public interface IEncrypter
{
    string Encrypt(string plainText, string? key);
    string Decrypt(string encryptedText, string? key);
}

public interface IHasher
{
    string Hash(string plainText);
    bool   Verify(string plainText, string hashedValue);
}
```

---

## DAL Abstractions

Lightweight database connectivity contracts. Implementations live in the `DAL.*` projects.

### IDbSettings

```csharp
string BuildConnectionString();
```

Extend `DbSettingsBase` to implement a provider-specific connection string builder.

### IDbCommunicator

```csharp
Task Open();
Task Close();
```

`DbCommunicator<TDbConnection>` is the generic implementation; provider-specific communicators (Postgres, MySQL, …) extend it.

### PagingInfo + QueryExtensions

```csharp
var paging = new PagingInfo { PageSize = 20, Page = 2 };
var page   = query.PageQuery(paging).ToList();
```

### IDbBackupService / IDbRestoreService

Shared backup/restore contracts implemented by the database-specific packages.

```csharp
public interface IDbBackupService
{
    Task<IMemoryFile> Backup();
}

public interface IDbRestoreService
{
    Task Restore(IMemoryFile file);
}
```

See the individual DAL project docs for implementations:
- [MongoDB](https://regira.github.io/Regira-Packages/src/DAL.MongoDB)
- [MySQL](https://regira.github.io/Regira-Packages/src/DAL.MySQL)
- [PostgreSQL](https://regira.github.io/Regira-Packages/src/DAL.PostgreSQL)

---

## Collections

### DisposableCollection\<T\>

A `List<T>` that disposes every `IDisposable` element when itself is disposed. Useful for holding image files, streams, or other resources that need coordinated cleanup.

```csharp
using var images = new DisposableCollection<IImageFile>();
images.Add((await imageService.Parse(bytes1))!);
images.Add((await imageService.Parse(bytes2))!);
// all entries disposed here
```

---

## Related documentation

| Library | Doc |
|---------|-----|
| Entities & EF Core | [Common.Entities](https://regira.github.io/Regira-Packages/src/Common.Entities) |
| Drawing (images) | [Common.Media](https://regira.github.io/Regira-Packages/src/Common.Media) — uses `IMemoryFile`, `BinaryFileItem`, `Size2D`, `LengthUnit` |
| IO.Storage | [Common.IO.Storage](https://regira.github.io/Regira-Packages/src/Common.IO.Storage) — uses `INamedFile`, `BinaryFileItem`, `ContentTypeUtility` |
| Office.Mail | [Mail docs](https://regira.github.io/Regira-Packages/src/Common.Office/docs/mail) — uses `INamedFile` for attachments |
| TreeList | [TreeList](https://regira.github.io/Regira-Packages/src/TreeList) |
