using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Extensions;
using Regira.Entities.Attachments.Models;
using Regira.Utilities;

namespace Regira.Entities.EFcore.Attachments;

public class DefaultFileIdentifierGenerator<TAttachmentKey, TAttachment>(IAttachmentFileService<TAttachment, TAttachmentKey> fileService)
    : IFileIdentifierGenerator
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
{
    /// <summary>
    /// Builds the storage key: <c>{Owner}/Attachments/{ObjectId}/{kebab-name}-{guid}{ext}</c>.
    /// <para>
    /// The key is internal and independent of what the client typed — the display name only seeds a readable
    /// slug. Any virtual folder the client supplied lives in <c>Attachment.FileName</c> and deliberately does
    /// <b>not</b> appear here, so re-filing an attachment is a metadata edit that never moves bytes.
    /// </para>
    /// </summary>
    public virtual Task<string> Generate(IEntityAttachment entity, CancellationToken token = default)
    {
        var entityType = entity.GetType();
        var idProp = entityType.GetProperty(nameof(EntityAttachment.ObjectId))!;
        var objectName = entityType.Name.Replace("Attachment", string.Empty);
        var entityFolder = $"{objectName}/Attachments/{idProp.GetValue(entity)}";
        var displayName = entity.Attachment!.FileName.ToFileNameOnly();
        var extension = Path.GetExtension(displayName);
        var sanitizedFileName = Path.GetFileNameWithoutExtension(displayName).ToKebabCase();
        var fileName = $"{entityFolder}/{sanitizedFileName}-{Guid.NewGuid():N}{extension}";
        return Task.FromResult(fileService.GetIdentifier(fileName));
    }
}