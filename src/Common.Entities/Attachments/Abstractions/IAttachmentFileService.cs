namespace Regira.Entities.Attachments.Abstractions;

public interface IAttachmentFileService<in TAttachment, TKey>
    where TAttachment : class, IAttachment<TKey>, new()
{
    Task<byte[]?> GetBytes(TAttachment item, CancellationToken token = default);
    Task SaveFile(TAttachment item, CancellationToken token = default);
    Task RemoveFile(TAttachment item, CancellationToken token = default);

    string GetIdentifier(string fileName);
    string GetRelativeFolder(TAttachment item);
}