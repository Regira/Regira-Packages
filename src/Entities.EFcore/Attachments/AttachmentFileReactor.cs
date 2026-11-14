using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.EFcore.Reactors;
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
/// longer names it: the file new bytes replaced, and the file of a deleted attachment. It reads the rows of a commit by
/// key first — together, one query per 500 attachments, past query filters — and keeps the file a row still names: a
/// save an interceptor suppressed, or one a rollback to a savepoint undid, changed no row. A file that cannot be removed
/// does not keep the others: every file is tried, and the failures are reported together. A save the database refuses,
/// or a transaction rolled back, runs no reactor. A save that fails leaves the file it wrote, which no row names, and so
/// do a rollback and a context without the reactor wiring (<c>DbContextWiring.Reactors</c>, part of
/// <c>UseDefaults()</c>): an application that needs a clean store sweeps it against the stored <c>Path</c> values.
/// </summary>
public class AttachmentFileReactor<TAttachment, TKey> : EntityBatchReactorBase<TAttachment>
    where TAttachment : class, IAttachment<TKey>, new()
{
    private const int MaxFilesInMessage = 10;

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

    public override async Task ReactMany(IReadOnlyList<IEntityChange<TAttachment>> changes, CancellationToken token = default)
    {
        // a file once, however many changes of the commit left it behind
        var files = changes
            .Select(change => change.Kind == EntityChangeKind.Deleted ? change.Entity : change.Original!)
            .DistinctBy(file => (file.Id, Path: Normalize(file.Path)))
            .ToArray();
        var named = _dbContext != null ? await NamedFiles(_dbContext, files, token) : [];

        List<(TAttachment File, Exception Exception)>? failures = null;
        var unnamed = files.Where(file => !named.Contains((file.Id, Normalize(file.Path)))).ToArray();
        foreach (var file in unnamed)
        {
            try
            {
                await _fileService.RemoveFile(file, token);
            }
            catch (Exception ex)
            {
                (failures ??= []).Add((file, ex));
            }
        }
        if (failures != null)
        {
            // which files: a store's own message need not say
            var shown = string.Join(", ", failures.Take(MaxFilesInMessage).Select(failure => $"#{failure.File.Id} {failure.File.Path}"));
            throw new AggregateException(
                $"Could not remove {failures.Count} of the {unnamed.Length} attachment file(s) a commit left behind: {shown}{(failures.Count > MaxFilesInMessage ? ", …" : "")}",
                failures.Select(failure => failure.Exception));
        }
    }

    // the (key, path) pairs the stored rows still name
    private static async Task<HashSet<(TKey Id, string? Path)>> NamedFiles(DbContext dbContext, TAttachment[] files, CancellationToken token)
    {
        var named = new HashSet<(TKey Id, string? Path)>();
        foreach (var ids in files.Select(file => file.Id).Distinct().Chunk(StoredRowReader.BatchSize))
        {
            var rows = await dbContext.Set<TAttachment>().IgnoreQueryFilters().AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .Select(x => new { x.Id, x.Path })
                .ToListAsync(token);
            named.UnionWith(rows.Select(row => (row.Id, Normalize(row.Path))));
        }
        return named;
    }

    // Compared without case, as most databases and file stores do: a path the row names in another casing can be the
    // same file, and keeping one file too many costs less than removing one a row still names.
    private static string? Normalize(string? path) => path?.ToUpperInvariant();
}
