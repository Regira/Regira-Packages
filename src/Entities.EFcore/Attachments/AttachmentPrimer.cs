using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.EFcore.Extensions;
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
/// file as it was. A save that fails hands the row its stored path back and removes the file it wrote — unless EF did not
/// roll the failed save back (a caller's transaction without a savepoint, an ambient transaction), where the row naming
/// that file may still be committed. Where an <see cref="AttachmentFileReactor{TAttachment, TKey}"/> runs for the attachment,
/// the replaced file and the file of a deleted attachment are removed once the save is committed; where none does, the
/// primer removes them once the save succeeds.
/// </summary>
public class AttachmentPrimer<TAttachment, TKey>(IAttachmentFileService<TAttachment, TKey> fileService) : EntityPrimerBase<TAttachment>
    where TAttachment : class, IAttachment<TKey>, new()
{
    // a key the default identifier generator made ends in a GUID, which a new key replaces rather than extends
    private static readonly Regex TrailingGuid = new("-[0-9a-f]{32}$", RegexOptions.IgnoreCase);

    public override async Task PrepareAsync(TAttachment entity, EntityEntry entry, CancellationToken token = default)
    {
        if (entry.State is EntityState.Added or EntityState.Modified)
        {
            // the type follows the file name, whoever set it: a client cannot choose the type its file is served as
            if (!string.IsNullOrWhiteSpace(entity.FileName))
            {
                entity.ContentType = ContentTypeUtility.GetContentType(entity.FileName);
            }

            // a new row gets a file of its own, also for bytes another row's file holds
            if (entry.State == EntityState.Added ? entity.HasContent() : StoredContent.HasNew(entity))
            {
                await Store(entity, entry, token);
            }
        }

        // the file of a deleted attachment; after the commit instead, where an AttachmentFileReactor removes it
        if (entry.State == EntityState.Deleted && !FileReactorRuns(entry.Context))
        {
            await RemoveOnceDeleted(entity, entry, token);
        }
    }

    private async Task RemoveOnceDeleted(TAttachment entity, EntityEntry entry, CancellationToken token)
    {
        var path = entity.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            // a delete by key: the stored row names the file. Read past identity resolution, which hands back this entity.
            var stored = await entry.GetDatabaseValuesAsync(token);
            path = stored?.GetValue<string?>(nameof(IAttachment.Path));
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }
        }
        var file = new TAttachment { Id = entity.Id, Path = path };

        SaveOutcomes.Register(entry.Context,
            onSaved: async () =>
            {
                // only a save that wrote the delete, which detaches the entry: a delete the database refused keeps its file
                if (entry.State == EntityState.Detached)
                {
                    await fileService.RemoveFile(file);
                }
            });
    }

    private async Task Store(TAttachment entity, EntityEntry entry, CancellationToken token)
    {
        var stored = new TAttachment { Id = entity.Id, FileName = entity.FileName, Identifier = entity.Identifier, Prefix = entity.Prefix, Path = entity.Path, Length = entity.Length };
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
            (entity.Identifier, entity.Length) = (stored.Identifier, stored.Length);
            throw;
        }
        StoredContent.Mark(entity);
        var written = entity.Path;

        var context = entry.Context;
        SaveOutcomes.Register(context,
            onFailed: async mayStand =>
            {
                // the row holds its stored file again for a retry; the file just written goes, unless the row naming it
                // may still be committed
                StoredContent.Unmark(entity);
                try
                {
                    if (!mayStand)
                    {
                        await fileService.RemoveFile(entity);
                    }
                }
                finally
                {
                    (entity.Identifier, entity.Prefix, entity.Path, entity.Length) = (stored.Identifier, stored.Prefix, stored.Path, stored.Length);
                }
            },
            onSaved: async () =>
            {
                // only a save that wrote this row replaced its file: not one an interceptor suppressed, nor a later save
                // of other rows after this one's primed change was dropped
                if (!string.IsNullOrWhiteSpace(storedPath) && written != storedPath
                    && entry.State == EntityState.Unchanged && entity.Path == written && !FileReactorRuns(context))
                {
                    await fileService.RemoveFile(stored);
                }
            });
    }

    // whether an AttachmentFileReactor removes this attachment's replaced and deleted files once the save is committed
    private static bool FileReactorRuns(DbContext context)
        => context.HasReactor(typeof(AttachmentFileReactor<TAttachment, TKey>), typeof(TAttachment));

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