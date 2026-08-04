using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Extensions;
using Regira.IO.Extensions;
using Regira.IO.Storage.Abstractions;
using Regira.IO.Storage.Helpers;
using Regira.Utilities;

namespace Regira.Entities.EFcore.Attachments;

public class AttachmentFileService<TAttachment, TKey>(IFileService fileService) : IAttachmentFileService<TAttachment, TKey>
    where TAttachment : class, IAttachment<TKey>, new()
{
    public async Task<byte[]?> GetBytes(TAttachment item, CancellationToken token = default)
    {
        if (item.HasContent())
        {
            return item.GetBytes();
        }

        var identifier = item.Identifier ??= fileService.GetIdentifier(item.Path!);
        if (!string.IsNullOrWhiteSpace(identifier))
        {
            return await fileService.GetBytes(identifier);
        }

        return null;
    }
    public async Task SaveFile(TAttachment item, CancellationToken token = default)
    {
        if (!item.HasContent())
        {
            throw new ArgumentNullException(nameof(item.Bytes));
        }

        // Identifier is the storage key. The attachment pipeline normally fills it via an
        // IFileIdentifierGenerator; when an Attachment is created directly (no generator in play),
        // derive a key from the file name so callers don't have to hand-craft one.
        if (string.IsNullOrWhiteSpace(item.Identifier))
        {
            if (string.IsNullOrWhiteSpace(item.FileName))
            {
                throw new ArgumentException(
                    $"Set {nameof(item.Identifier)} (the storage key) or {nameof(item.FileName)} before saving an attachment, or save it through the attachment service.",
                    nameof(item.Identifier));
            }

            var name = Path.GetFileNameWithoutExtension(item.FileName).ToKebabCase();
            item.Identifier = $"{name}-{Guid.NewGuid():N}{Path.GetExtension(item.FileName)}";
        }

        await using var fileStream = item.GetStream()!;
        if (item.IsNew())
        {
            var fileNameHelper = new FileNameHelper(fileService);
            // every filename should be unique!
            var identifier = await fileNameHelper.NextAvailableFileName(item.Identifier);
            item.Identifier = identifier;
        }

        var path = await fileService.Save(item.Identifier, fileStream, item.ContentType);
        // don't save full path (increases flexibility for multiple platforms)
        item.Path = fileService.GetIdentifier(path);
        item.Prefix = fileService.GetRelativeFolder(item.Identifier);
        item.FileName ??= Path.GetFileName(item.Identifier);
    }
    public async Task RemoveFile(TAttachment item, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            throw new ArgumentNullException(nameof(item.Path));
        }

        await fileService.Delete(item.Path);
    }

    public string GetIdentifier(string fileName)
        => fileService.GetIdentifier(fileName);
    public string GetRelativeFolder(TAttachment item)
        => fileService.GetRelativeFolder(item.Path!)!;
}