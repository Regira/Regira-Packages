using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Reactors.Abstractions;

namespace Regira.Entities.EFcore.Attachments;

public class AttachmentFileReactor : AttachmentFileReactor<Attachment, int>
{
    /// <summary>A reactor that removes the file without reading the stored row first.</summary>
    [Obsolete(AttachmentFileReactor<Attachment, int>.WithoutContext)]
    public AttachmentFileReactor(IAttachmentFileService<Attachment, int> fileService)
#pragma warning disable CS0618 // the obsolete constructor forwards to its obsolete base
        : base(fileService)
#pragma warning restore CS0618
    {
    }

    /// <summary>A reactor that reads the attachment's row in <paramref name="dbContext"/> before it removes a file.</summary>
    public AttachmentFileReactor(IAttachmentFileService<Attachment, int> fileService, DbContext dbContext)
        : base(fileService, dbContext)
    {
    }
}
/// <summary>
/// Removes an attachment's file once the save that replaced or deleted it is committed, when the attachment's own row no
/// longer names it: the file new bytes replaced, and the file of a deleted attachment. It reads that row by key first,
/// past query filters, and keeps the file the row still names — a save an interceptor suppressed, or one a rollback to a
/// savepoint undid, changed no row. A save the database refuses, or a transaction rolled back, runs no reactor. A save
/// that fails leaves the file it wrote, which no row names, and so do a rollback and a context without the reactor
/// wiring (<c>DbContextWiring.Reactors</c>, part of <c>UseDefaults()</c>): an application that needs a clean store sweeps
/// it against the stored <c>Path</c> values.
/// </summary>
public class AttachmentFileReactor<TAttachment, TKey> : EntityReactorBase<TAttachment>
    where TAttachment : class, IAttachment<TKey>, new()
{
    internal const string WithoutContext = "Removes the file without reading the stored row, so a save an interceptor suppressed, or a rollback to a savepoint, removes a file a row still names. Use the constructor taking the DbContext, which WithAttachments registers. Will be removed in v7.";

    private readonly IAttachmentFileService<TAttachment, TKey> _fileService;
    private readonly DbContext? _dbContext;

    /// <summary>
    /// A reactor that removes the file without reading the stored row first. Registered by type
    /// (<c>AddReactor&lt;TAttachment, AttachmentFileReactor&lt;TAttachment, TKey&gt;&gt;()</c>), the reactor is built with
    /// this constructor, since no <see cref="DbContext"/> is registered as one; <c>WithAttachments</c> registers it with the
    /// context.
    /// </summary>
    [Obsolete(WithoutContext)]
    public AttachmentFileReactor(IAttachmentFileService<TAttachment, TKey> fileService)
    {
        _fileService = fileService;
    }

    /// <summary>A reactor that reads the attachment's row in <paramref name="dbContext"/> before it removes a file.</summary>
    /// <param name="fileService">The store of the attachment's files</param>
    /// <param name="dbContext">A context of the reaction's scope holding <typeparamref name="TAttachment"/></param>
    public AttachmentFileReactor(IAttachmentFileService<TAttachment, TKey> fileService, DbContext dbContext)
    {
        _fileService = fileService;
        _dbContext = dbContext;
    }

    public override bool CanReact(IEntityChange<TAttachment> change) => change.Kind switch
    {
        EntityChangeKind.Modified => !string.IsNullOrWhiteSpace(change.Original?.Path) && change.Original.Path != change.Entity.Path,
        EntityChangeKind.Deleted => !string.IsNullOrWhiteSpace(change.Entity.Path),
        _ => false
    };

    public override async Task React(IEntityChange<TAttachment> change, CancellationToken token = default)
    {
        var file = change.Kind == EntityChangeKind.Deleted ? change.Entity : change.Original!;
        if (_dbContext != null && await IsNamed(_dbContext, file.Id, file.Path, token))
        {
            return;
        }
        await _fileService.RemoveFile(file, token);
    }

    private static Task<bool> IsNamed(DbContext dbContext, TKey id, string? path, CancellationToken token)
        => dbContext.Set<TAttachment>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(x => x.Id!.Equals(id) && x.Path == path, token);
}
