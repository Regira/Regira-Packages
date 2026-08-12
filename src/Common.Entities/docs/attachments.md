# Entity Attachments

The Attachments module contains 2 main components:
- Attachment: *represents a file stored in the system*
- EntityAttachment: *links an Attachment to an entity (e.g. Product, Article, ...)*

All `EntityAttachments` are linked to the same `Attachment`.

## Attachment

All attachments for all entities are stored in one table.

### Models

```csharp
public interface IAttachment : IBinaryFile, IHasTimestamps;
public interface IAttachment<TKey> : IAttachment, IEntity<TKey>;
```

The Attachment is based on `IBinaryFile` (Part of [Regira.IO](../../../Common.IO.Storage/README.md) module):
- `string? FileName` - The name of a file (not full path)
- `string? Identifier` - Identifier in a specific context (Prefix + Filename)
- `string? Prefix` - The folder structure, except the root folder
- `string? Path` - The full path/Uri for this file
- `string? ContentType` - MIME type of the file
- `long Length` - Size of the file in bytes
- `byte[]? Bytes` - Content as a byte array
- `Stream? Stream` - Content as a stream

### Services

The `AttachmentFileService` handles the physical file storage and retrieval for attachments.

```csharp
public class AttachmentFileService<TAttachment, TKey>(IFileService fileService) : IAttachmentFileService<TAttachment, TKey>
{
    public async Task<byte[]?> GetBytes(TAttachment item, CancellationToken token = default)
    public async Task SaveFile(TAttachment item, CancellationToken token = default)
    public async Task RemoveFile(TAttachment item, CancellationToken token = default)

    public string GetIdentifier(string fileName)
    public string GetRelativeFolder(TAttachment item)
}
```
It uses an underlying `IFileService` to perform the actual file operations.
Useful IFileService implementations:
- `BinaryFileService`: *Local File System*
- `BinaryBlobService`: *Azure Blob Storage*
- `SftpService`: *SFTP/SSH*


## EntityAttachment

```csharp
// (simplified)
public interface IEntityAttachment<TKey, TObjectKey> : IEntityAttachment<TKey, TObjectKey, int, Attachment>;
public interface IEntityAttachment<TKey, TObjectKey, TAttachmentKey> : IEntityAttachment<TKey, TObjectKey, TAttachmentKey, Attachment<TAttachmentKey>>;
public interface IEntityAttachment<TKey, TObjectKey, TAttachmentKey, TAttachment> : IEntity<TKey>, IHasObjectId<TObjectKey>, IEntityAttachment, ISortable
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
{
    string? ObjectType { get; } // Name of owning entity type (e.g. Product, Article, ...)

    // properties used to update existing attachment values
    string? NewFileName { get; set; }
    string? NewContentType { get; set; }
    byte[]? NewBytes { get; set; }

    TAttachmentKey AttachmentId { get; set; }
    new TAttachment? Attachment { get; set; }
}
```

## Implementation

### EntityAttachment model

Inherit the **`EntityAttachment`** base (which maps to `EntityAttachment<int, int, int, Attachment>`) and
set `ObjectType` in the constructor.

```csharp
public class ProductAttachment : EntityAttachment
{
    public ProductAttachment() => ObjectType = nameof(Product);
}
```

### Owning Entity

After defining the model of the EntityAttachment, 2 interfaces have to be implemented on the Owning Entity:
- `IHasAttachments`
- `IHasAttachments<TEntityAttachment>`

```csharp
// other properties and interfaces are omitted
public class OwningEntity: IHasAttachments, IHasAttachments<MyEntityAttachment>
{
    // ...

    // Add these 3 properties
    public bool? HasAttachment { get; set; }
    public ICollection<MyEntityAttachment>? Attachments { get; set; }
    // implicit interface implementation
    ICollection<IEntityAttachment>? IHasAttachments.Attachments
    {
        get => Attachments?.Cast<IEntityAttachment>().ToArray();
        set => Attachments = value?.Cast<MyEntityAttachment>().ToArray();
    }
}
```

### DbContext

```csharp   
    // Add a DbSet for each EntityAttachment type
    public DbSet<MyEntityAttachment> MyEntityAttachments { get; set; } = null!;

    // Update OnModelCreating
    modelBuilder.Entity<OwningEntity>(entity =>
    {
        entity.HasMany(e => e.Attachments)
            .WithOne()
            .HasForeignKey(e => e.ObjectId)
            .HasPrincipalKey(e => e.Id);
    });
```

### Controllers

The custom EntityAttachmentController must derive from `EntityAttachmentControllerBase`. Set the class
`[Route]` to the **owner base path** — the base actions append the sub-routes
(`{objectId}/attachments`, `attachments/{id}`, `{objectId}/files`, `files/{id}`, …).

```csharp
// using default DTOs (EntityAttachmentDto & EntityAttachmentInputDto))
[ApiController, Route("products")]
public class ProductAttachmentsController : EntityAttachmentControllerBase<ProductAttachment>;
// or using custom DTOs
[ApiController, Route("products")]
public class ProductAttachmentsController : EntityAttachmentControllerBase<ProductAttachment, MyAttachmentDto, MyAttachmentInputDto>;
```

Endpoints exposed (with `[Route("products")]`):

| Method | Route | Purpose |
|--------|-------|---------|
| `POST` | `{objectId}/files` | Upload a file (multipart `IFormFile` + input model) |
| `PUT` | `{objectId}/files/{id}` | Replace an existing file |
| `GET` | `{objectId}/attachments` | List attachments for an owner |
| `GET` | `attachments/{id}` | Attachment metadata |
| `PUT` | `{objectId}/attachments/{id}` | Update attachment metadata |
| `DELETE` | `attachments/{id}` | Delete (also removes the file) |
| `GET` | `files/{id}` · `{objectId}/files/{fileName}` | Download the file |

### Dependency Injection

Attachments need **two** registrations:

1. **`WithAttachments(factory)`** registers the shared `Attachment` entity, the file store and the
   bytes→file primer.
2. **`HasAttachments<…>(x => x.Attachments)`** — chained on the owner's `For<>()` builder — registers the
   typed per-owner read/write services, the link prepper and DTO mapping.

```csharp
builder.Services
    .AddHttpContextAccessor()                       // required for attachment Uri resolution
    .UseEntities<MyDbContext>(o =>
    {
        o.UseDefaults();
        o.UseAttachmentUris();                      // web apps: resolve attachment DTO Uri's (ASP.NET Core)
        /* ... */
    })
    // 1. shared Attachment entity + file store + bytes→file primer
    .WithAttachments(_ => new BinaryFileService(
        new FileSystemOptions
        {
            RootFolder = ApiConfiguration.AttachmentsDirectory
        }
    ))
    // 2. typed per-owner services + link prepper + DTO mapping
    .For<Product>(e => e.HasAttachments<MyDbContext, Product, ProductAttachment>(x => x.Attachments));
```

> **Mapped owner (`UseMapping`)?** Declare the collection on the owner's input DTO —
> `public ICollection<EntityAttachmentInputDto>? Attachments { get; set; }` — and mirror it on the read DTO
> with `ICollection<EntityAttachmentDto>?`. Without the input property, the convention map yields a `null`
> collection on every parent save, which the sync reads as "attachments not sent": adds, removes and
> reorders through the parent are silently ignored while the `/{objectId}/attachments` sub-routes keep
> working. Startup validation warns about this shape.

> **File-service factory.** `WithAttachments` takes an `IFileService` factory
> (`Func<IServiceProvider, IFileService>`), not a registered `IFileService` — so your app can still register
> its own store(s) elsewhere. Build one inline (`WithAttachments(_ => new BinaryFileService(...))`) or reuse
> an app-registered one (`WithAttachments(p => p.GetRequiredService<IFileService>())`). It's wrapped into the
> registered `IAttachmentFileService<Attachment, int>` — one per attachment base type, so each can use a
> different store.

> **Reading file bytes.** Use the built-in download endpoints, or inject
> `IAttachmentFileService<Attachment, int>` and call `GetBytes(item)`. Consuming code references files by
> `Identifier` (the public storage key, populated when you load through the entity service); `Path` is
> internal and isn't mapped to DTOs — clients get a download `Uri` instead.

> **Ordering.** Attachment order travels by **array position**: `HasAttachments` wires `SetSortOrder()`
> over the incoming collection, so every parent save assigns `SortOrder = index` — the input DTO carries
> no sort field on purpose, and any client-sent value is overwritten. The read DTO exposes `SortOrder`;
> order the eager-load (`x.Attachments!.OrderBy(a => a.SortOrder)`) so a round-trip is stable.

> **`o.UseAttachmentUris()` (web apps).** Populates the attachment DTO `Uri`.
> `Entities.DependencyInjection` doesn't reference `Entities.Web`, so the ASP.NET Core resolver
> (`LinkGenerator` + `IHttpContextAccessor`) is opt-in (namespace
> `Regira.Entities.Web.Attachments.DependencyInjection`). Call it in the `UseEntities` options block, before
> entities are registered; without it, `Uri` is `null`. The `Uri` is generated as a link to the `GetFile`
> action on the attachment entity's controller (`{EntityAttachment}Controller : EntityAttachmentControllerBase<…>`),
> so that controller must be mapped. If you replace the generated attachment endpoints with a custom download
> route, the link generator finds no matching action and `Uri` stays `null` — use the download endpoint
> directly. It is also `null` outside an active request (e.g. during seeding).

## Overview

1. [Index](../README.md) — Overview of Regira Entities
1. [Entity Models](models.md) — Creating and structuring entity models
1. [Services](services.md) — Implementing entity services and repositories
1. [Mapping](mapping.md) — Mapping Entities to and from DTOs
1. [Web Endpoints](web-endpoints.md) — Exposing entity operations as HTTP endpoints
1. [Normalizing](normalizing.md) — Data normalization techniques
1. **[Attachments](attachments.md)** — Managing file attachments
1. [Built-in Features](built-in-features.md) — Ready to use components
1. [Checklist](checklist.md) — Step-by-step guide for common tasks
1. [Practical Examples](examples.md) — Complete implementation examples
