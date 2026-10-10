using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Web.Models;
using Regira.IO.Abstractions;

namespace Regira.Entities.Mediator.Requests;

// The operations of an owner's attachment routes — the link entity TEntity joins one owner row to one stored file.
// The route is authoritative throughout: its ids win over the input, and a link of another owner is refused with an
// EntityInputException keyed objectId.

/// <summary>
/// Creates a link of owner <see cref="ObjectId"/> to a new stored file, as <c>POST {objectId}/files</c> does. It is always
/// a new link, whatever id <see cref="Input"/> carries. The answer is the saved link mapped to <typeparamref name="TDto"/>.
/// <see cref="ValidateInput"/> checks the input's DataAnnotations first, as <c>SaveCommand</c>'s does; the attachment
/// controller turns it off.
/// </summary>
public sealed record UploadAttachmentCommand<TEntity, TDto, TInputDto>(int ObjectId, TInputDto Input, INamedFile File, bool ValidateInput = true)
    : IEntityRequest<SaveResult<TDto>>
    where TEntity : class, IEntityAttachment<int, int, int, Attachment>
{
    Type IEntityRequest.EntityType => typeof(TEntity);
    EntityOperation IEntityRequest.Operation => EntityOperation.Save;
}

/// <summary>
/// Updates link <see cref="Id"/> of owner <see cref="ObjectId"/> from <see cref="Input"/>, as <c>PUT {objectId}/attachments/{id}</c>
/// does — a new name or new bytes on the input change the stored file too. <c>null</c> when the link does not exist.
/// <see cref="ValidateInput"/> checks the input's DataAnnotations first, as <c>SaveCommand</c>'s does; the attachment
/// controller turns it off.
/// </summary>
public sealed record UpdateAttachmentCommand<TEntity, TDto, TInputDto>(int ObjectId, int Id, TInputDto Input, bool ValidateInput = true)
    : IEntityRequest<SaveResult<TDto>>
    where TEntity : class, IEntityAttachment<int, int, int, Attachment>
{
    Type IEntityRequest.EntityType => typeof(TEntity);
    EntityOperation IEntityRequest.Operation => EntityOperation.Save;
}

/// <summary>
/// Replaces the file of link <see cref="Id"/> of owner <see cref="ObjectId"/>, as <c>PUT {objectId}/files/{id}</c> does.
/// <c>null</c> when the link does not exist.
/// </summary>
public sealed record ReplaceAttachmentFileCommand<TEntity, TDto>(int ObjectId, int Id, INamedFile File)
    : IEntityRequest<SaveResult<TDto>>
    where TEntity : class, IEntityAttachment<int, int, int, Attachment>
{
    Type IEntityRequest.EntityType => typeof(TEntity);
    EntityOperation IEntityRequest.Operation => EntityOperation.Save;
}

/// <summary>The stored file of link <see cref="Id"/>, as <c>GET files/{id}</c> serves it; <c>null</c> when the link does not exist.</summary>
public sealed record AttachmentFileQuery<TEntity>(int Id) : IEntityRequest<Attachment>
    where TEntity : class, IEntityAttachment<int, int, int, Attachment>
{
    Type IEntityRequest.EntityType => typeof(TEntity);
    EntityOperation IEntityRequest.Operation => EntityOperation.Details;
}

/// <summary>
/// The stored file of owner <see cref="ObjectId"/> named <see cref="FileName"/> — a virtual path, folders included —
/// as the mapped <c>GET {objectId}/files/{*fileName}</c> serves it; <c>null</c> when the owner has no file of that name.
/// The attachment controller's download by name goes through its own <c>GetFile(id)</c>, and so sends
/// <see cref="AttachmentFileQuery{TEntity}"/>.
/// </summary>
public sealed record AttachmentFileByNameQuery<TEntity>(int ObjectId, string FileName) : IEntityRequest<Attachment>
    where TEntity : class, IEntityAttachment<int, int, int, Attachment>
{
    Type IEntityRequest.EntityType => typeof(TEntity);
    EntityOperation IEntityRequest.Operation => EntityOperation.Details;
}
