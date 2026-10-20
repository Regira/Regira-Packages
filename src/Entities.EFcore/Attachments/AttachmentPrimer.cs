using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.EFcore.Utilities;
using Regira.IO.Extensions;
using Regira.IO.Utilities;

namespace Regira.Entities.EFcore.Attachments;

public class AttachmentPrimer(IAttachmentFileService<Attachment, int> fileService) : AttachmentPrimer<Attachment, int>(fileService);
/// <summary>
/// Stores the content given to an attachment as the save is primed, and types it by its file name; the bytes
/// <c>Details</c> loaded are its stored file, not new content. New content for a stored file goes under a key of its own,
/// with the extension of the file name, so a save the database refuses, or a transaction rolled back, leaves the stored
/// file as it was. The primer removes no file: <see cref="AttachmentFileReactor{TAttachment, TKey}"/> removes a replaced or
/// deleted attachment's file after the commit, once the attachment's own row no longer names it. A save that fails leaves the file it wrote,
/// which no row names; the attachment keeps naming it, so a retry of the same instance saves that file rather than
/// write another.
/// </summary>
public class AttachmentPrimer<TAttachment, TKey>(IAttachmentFileService<TAttachment, TKey> fileService) : EntityPrimerBase<TAttachment>
    where TAttachment : class, IAttachment<TKey>, new()
{
    // a key the default identifier generator made ends in a GUID, which a new key replaces rather than extends
    private static readonly Regex TrailingGuid = new("-[0-9a-f]{32}$", RegexOptions.IgnoreCase);
    // the new rows this primer stored content for, in a save that failed: a retry stores it again only when it changed
    private static readonly EntryMarks StoredForNewRow = new();

    public override async Task PrepareAsync(TAttachment entity, EntityEntry entry, CancellationToken token = default)
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified))
        {
            return;
        }

        // the type follows the file name, whoever set it: a client cannot choose the type its file is served as
        if (!string.IsNullOrWhiteSpace(entity.FileName))
        {
            entity.ContentType = ContentTypeUtility.GetContentType(entity.FileName);
        }

        // a new row gets a file of its own, also for bytes another row's file holds
        var isNewRow = entry.State == EntityState.Added && !StoredForNewRow.Contains(entry);
        if (isNewRow ? entity.HasContent() : StoredContent.HasNew(entity))
        {
            await Store(entity, entry, token);
        }
    }

    private async Task Store(TAttachment entity, EntityEntry entry, CancellationToken token)
    {
        var (identifier, length) = (entity.Identifier, entity.Length);
        var storedPath = entry.State == EntityState.Modified ? entity.Path : null;
        if (!string.IsNullOrWhiteSpace(storedPath)
            && (entity.Identifier == null || fileService.GetIdentifier(entity.Identifier) == fileService.GetIdentifier(storedPath)))
        {
            entity.Identifier = KeyBeside(fileService.GetIdentifier(storedPath), entity.FileName);
        }
        // the length of the content stored, new bytes or a stream for a stored file included
        entity.Length = entity.HasStream() ? entity.Stream!.Length : entity.Bytes?.LongLength ?? 0;
        try
        {
            await fileService.SaveFile(entity, token);
        }
        catch
        {
            (entity.Identifier, entity.Length) = (identifier, length);
            throw;
        }
        StoredContent.Mark(entity);
        if (entry.State == EntityState.Added)
        {
            StoredForNewRow.Add(entry);
        }
    }

    /// <summary>A new key in the stored key's folder, for the same name, with the extension of the file name.</summary>
    private static string KeyBeside(string storedKey, string? fileName)
    {
        var folder = Path.GetDirectoryName(storedKey)?.Replace('\\', '/');
        var name = TrailingGuid.Replace(Path.GetFileNameWithoutExtension(storedKey), string.Empty);
        var extension = Path.GetExtension(fileName);
        var key = $"{name}-{Guid.NewGuid():N}{(string.IsNullOrEmpty(extension) ? Path.GetExtension(storedKey) : extension)}";
        return string.IsNullOrEmpty(folder) ? key : $"{folder}/{key}";
    }
}
