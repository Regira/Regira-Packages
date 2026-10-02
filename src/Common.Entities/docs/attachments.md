# Entity Attachments

The Attachments module contains 2 main components:
- Attachment: *represents a file stored in the system*
- EntityAttachment: *links an Attachment to an entity (e.g. Product, Article, ...)*

All `EntityAttachments` are linked to the same `Attachment`.

## Attachment

All attachments for all entities are stored in one table.

### Models

<!-- no-compile -->
```csharp
public interface IAttachment : IBinaryFile, IHasTimestamps;
public interface IAttachment<TKey> : IAttachment, IEntity<TKey>;
```

The Attachment is based on `IBinaryFile` (Part of [Regira.IO](../../Common.IO.Storage/README.md) module):
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

<!-- no-compile -->
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
    [Obsolete] string? NewContentType { get; set; }   // ignored: the content type follows the file name
    byte[]? NewBytes { get; set; }

    TAttachmentKey AttachmentId { get; set; }
    new TAttachment? Attachment { get; set; }
}
```

## Implementation

### EntityAttachment model

Inherit the **`EntityAttachment`** base (which maps to `EntityAttachment<int, int, int, Attachment>`) and
set `ObjectType` in the constructor.

<!-- no-compile -->
```csharp
public class ProductAttachment : EntityAttachment
{
    public ProductAttachment() => ObjectType = nameof(Product);
}
```

Each owning entity gets its own subclass: the class is the join table and its constructor pins one
`ObjectType`, so attaching files to a second entity means a second subclass, `DbSet`, controller and
registration.

### Owning Entity

After defining the model of the EntityAttachment, 2 interfaces have to be implemented on the Owning Entity:
- `IHasAttachments`
- `IHasAttachments<TEntityAttachment>`

<!-- no-compile -->
```csharp
// other properties and interfaces are omitted
public class OwningEntity: IHasAttachments, IHasAttachments<MyEntityAttachment>
{
    // ...

    // Add these 3 properties
    // HasAttachment is yours to fill: nothing populates it, so it serializes as null even for a row that
    // has attachments. Filtering on it is also yours to wire — Regira.Entities.EFcore ships the
    // FilterHasAttachment(bool?) query extension, but no query builder calls it for you. It is a flag, not a
    // column, hence [NotMapped] (System.ComponentModel.DataAnnotations.Schema).
    [NotMapped]
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

<!-- no-compile -->
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

The owner-side mapping is required. `ObjectId` is not a conventional foreign-key name and the link has no
navigation back to its owner, so without it EF adds a shadow key of its own: link rows are saved without an
owner, the owner's `Attachments` loads empty and `?hasAttachment=true` matches nothing. Startup validation warns.

#### Marking one attachment as the primary one

Mark the link entity — a flag, or its `SortOrder`, which is assigned from the incoming array position. A foreign
key from the owner to one of its own attachments makes the two tables reference each other; see
[Entity Models: Referencing one of your own children](models.md#referencing-one-of-your-own-children).

### Controllers

The custom EntityAttachmentController must derive from `EntityAttachmentControllerBase`. Set the class
`[Route]` to the **owner base path** — the base actions append the sub-routes
(`{objectId}/attachments`, `attachments/{id}`, `{objectId}/files`, `files/{id}`, …).

<!-- no-compile -->
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
| `GET` | `attachments` | List links across owners (`EntityAttachmentSearchObject`) |
| `GET` | `attachments/{id}` | Attachment metadata |
| `PUT` | `{objectId}/attachments/{id}` | Update attachment metadata: `NewFileName` renames the file and retypes it, `NewBytes` replaces its content; the link keeps its attachment, whatever `AttachmentId` the body sends |
| `DELETE` | `attachments/{id}` | Delete (also removes the file) |
| `GET` | `files/{id}` · `{objectId}/files/{fileName}` | Download the file |

Every `{id}` is the id of the link row (`EntityAttachmentDto.Id`), not its `AttachmentId`; `{objectId}` is the
owner's id. The two `PUT` routes answer **400** for a link of another owner, a `ValidationProblemDetails` keyed
`objectId`.

An attachment's content type follows its file name — whatever the client declared, and whoever writes the row — and
a download is served with `X-Content-Type-Options: nosniff` and, for every file but a PDF,
`Content-Security-Policy: sandbox`, so a file renders but runs no script on the API's origin. A write a
[validator](services.md#entity-validators) refuses answers **400** with a `ValidationProblemDetails`
([Input Exceptions](built-in-features.md#input-exceptions)).

Validators scoped to the link entity run for these endpoints only. A `PUT` of the owner whose input carries
`Attachments` syncs the links itself — it adds one for each new entry with `NewBytes`, renames and replaces a kept
link's file from its `NewFileName` and `NewBytes`, and deletes the ones the array leaves out — and runs only the
owner's validators. A kept link keeps its attachment, whatever `AttachmentId` the entry sends, and a new one may point
only at an attachment the owner already links: one naming another owner's is cleared, and without `NewBytes` of its
own the save answers 409. The
upload route always creates a link, whatever `Id` its form sends.

**Scope an upload yourself.** An upload is a create: it takes the owner's id from the route and runs no query, so a
global filter (tenant, owner) never sees it, and any authenticated caller can attach a file to a row it cannot read.
`PUT` and `DELETE` load the link through the service first, and are filtered. Add a validator on the link entity that
re-runs the owner's scope on `Add` and refuses when it resolves nothing — a 400, where the read path answers 404 — or
override the controller's `Add` to answer 404. Read scope is not write scope either: a read scope widened on purpose,
a manager seeing their reports' rows, grants writes and deletes on those rows too, so put a narrower ownership check in
a validator, which runs on a delete as well. Repeat a link rule, such as the allowed file types or a file that
must not be deleted, in the owner's validator, or keep `Attachments` off the owner's input DTO.

### Dependency Injection

Attachments need **two** registrations:

1. **`WithAttachments(factory)`** registers the shared `Attachment` entity, the file store, the
   bytes→file primer, and `AttachmentFileReactor`, which removes a file that new bytes replaced, and a deleted
   attachment's file, once the save is committed — new bytes go under a key of their own, so a refused or rolled-back
   save leaves the stored files as they were; only a transaction rolled back after a successful save keeps the new file
   in storage. It runs through the reactor wiring `UseDefaults()` sets; without it, a replaced file is removed once
   the save succeeds, and a deleted attachment's file during the save.
2. **`HasAttachments<…>(x => x.Attachments)`** — chained on the owner's `For<>()` builder — registers the
   typed per-owner read/write services, the link prepper and DTO mapping.

The bytes `Details` loads are the attachment's stored file, not new content: saving a rename or another metadata edit
leaves the file where it is. Bytes or a stream set in their place replace it, stored under the file name's extension.

<!-- no-compile -->
```csharp
using Regira.Entities.DependencyInjection.Attachments;       // HasAttachments
using Regira.Entities.DependencyInjection.Extensions;        // UseEntities, UseDefaults
using Regira.Entities.Web.Attachments.DependencyInjection;   // UseAttachmentUris
using Regira.IO.Storage.FileSystem;                          // BinaryFileService, FileSystemOptions

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

> **Owner with its own input DTO?** Declare the collection on the owner's input DTO —
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
1. [Services](services.md) — Implementing entity services, repositories and the write pipeline
1. [Mapping](mapping.md) — Mapping Entities to and from DTOs
1. [Web Endpoints](web-endpoints.md) — Exposing entity operations as HTTP endpoints
1. [Normalizing](normalizing.md) — Data normalization techniques
1. **[Attachments](attachments.md)** — Managing file attachments
1. [Built-in Features](built-in-features.md) — Ready to use components
1. [Checklist](checklist.md) — Step-by-step guide for common tasks
1. [Practical Examples](examples.md) — Complete implementation examples
