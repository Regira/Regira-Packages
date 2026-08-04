using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.Attachments;

/// <summary>
/// Default <see cref="IAttachmentUriResolver{TEntity}"/> used when no web URI resolver is registered.
/// Always resolves to <c>null</c>, so attachment DTOs work without an ASP.NET Core dependency.
/// </summary>
public class NullAttachmentUriResolver<TEntityAttachment> : IAttachmentUriResolver<TEntityAttachment>
    where TEntityAttachment : IEntity, IEntityAttachment
{
    public string? Resolve(TEntityAttachment source) => null;
}
