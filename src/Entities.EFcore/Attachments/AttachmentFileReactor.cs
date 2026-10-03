using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Reactors.Abstractions;

namespace Regira.Entities.EFcore.Attachments;

public class AttachmentFileReactor(IAttachmentFileService<Attachment, int> fileService) : AttachmentFileReactor<Attachment, int>(fileService);
/// <summary>
/// Removes an attachment's file once the save that replaced or deleted it is committed: the file new bytes replaced,
/// and the file of a deleted attachment. A save the database refuses, or a transaction rolled back, leaves the stored
/// file as it was, as it leaves the row. Without the reactor wiring (<c>DbContextWiring.Reactors</c>, part of
/// <c>UseDefaults()</c>), or without this reactor registered for the attachment, <see cref="AttachmentPrimer{TAttachment, TKey}"/>
/// removes a replaced file once the save succeeds, and a deleted attachment's file during the save.
/// </summary>
public class AttachmentFileReactor<TAttachment, TKey>(IAttachmentFileService<TAttachment, TKey> fileService) : EntityReactorBase<TAttachment>
    where TAttachment : class, IAttachment<TKey>, new()
{
    public override bool CanReact(IEntityChange<TAttachment> change) => change.Kind switch
    {
        EntityChangeKind.Modified => !string.IsNullOrWhiteSpace(change.Original?.Path) && change.Original.Path != change.Entity.Path,
        EntityChangeKind.Deleted => !string.IsNullOrWhiteSpace(change.Entity.Path),
        _ => false
    };

    public override Task React(IEntityChange<TAttachment> change, CancellationToken token = default)
        => fileService.RemoveFile(change.Kind == EntityChangeKind.Deleted ? change.Entity : change.Original!, token);
}
